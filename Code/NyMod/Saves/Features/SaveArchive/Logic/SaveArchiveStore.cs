using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Infrastructure.Persistence;

namespace NyMod.Saves.Features.SaveArchive.Logic;

internal sealed class SaveArchiveStore
{
	private readonly SaveArchivePathResolver _pathResolver;

	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public SaveArchiveStore(SaveArchivePathResolver pathResolver)
	{
		_pathResolver = pathResolver;
	}

	public bool TryReadActivePayload(out byte[]? payloadBytes, out string? sourceFileName)
	{
		payloadBytes = null;
		sourceFileName = null;
		if (!_pathResolver.TryGetActiveSavePath(out string activeSavePath) || string.IsNullOrEmpty(activeSavePath))
		{
			Log.Warn("NyMod.Saves could not resolve the active save path.", 2);
			return false;
		}
		string path = activeSavePath;
		if (!File.Exists(path))
		{
			string text = activeSavePath + ".backup";
			if (!File.Exists(text))
			{
				Log.Warn($"NyMod.Saves found no active save file at '{activeSavePath}' or backup '{text}'.", 2);
				return false;
			}
			path = text;
		}
		payloadBytes = File.ReadAllBytes(path);
		sourceFileName = Path.GetFileName(path);
		return true;
	}

	public bool SaveSnapshot(SaveArchiveMetadata metadata, byte[] payloadBytes)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string? payloadPath) || string.IsNullOrEmpty(payloadPath)
			|| !_pathResolver.TryGetMetadataPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string? metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			return false;
		}
		try
		{
			// Publish metadata last. Readers only discover committed snapshots.
			AtomicFile.Write(payloadPath, payloadBytes);
			AtomicFile.Write(metadataPath, JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions));
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves could not write snapshot '{metadata.SaveId}': {ex}", 2);
			return false;
		}
	}

	public byte[] ReadSnapshotPayload(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string? path) || string.IsNullOrEmpty(path))
		{
			throw new IOException("Could not resolve the snapshot payload path.");
		}
		return File.ReadAllBytes(path);
	}

	public bool TryUpdateSnapshotMetadata(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetMetadataPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			Log.Warn("NyMod.Saves could not resolve metadata path for snapshot '" + metadata.SaveId + "'.", 2);
			return false;
		}
		string directoryName = Path.GetDirectoryName(metadataPath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		AtomicFile.Write(metadataPath, JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions));
		return true;
	}

	public IReadOnlyList<SaveArchiveMetadata> LoadSnapshotsForRun(string runId, SaveArchiveKind kind)
	{
		return ReadSnapshots(runId, kind).OrderByDescending(snapshot => snapshot.CreatedUtc).ToList();
	}

	public bool HasSnapshots()
	{
		return LoadRunIds().Any(runId => ReadSnapshots(runId, SaveArchiveKind.Auto).Any()
			|| ReadSnapshots(runId, SaveArchiveKind.Manual).Any());
	}

	private IEnumerable<SaveArchiveMetadata> ReadSnapshots(string runId, SaveArchiveKind kind)
	{
		if (!_pathResolver.TryGetSnapshotDirectory(runId, kind, out string? directory) || string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
		{
			yield break;
		}
		foreach (string path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
		{
			SaveArchiveMetadata? metadata;
			try
			{
				metadata = JsonSerializer.Deserialize<SaveArchiveMetadata>(File.ReadAllText(path), _jsonOptions);
			}
			catch (Exception ex)
			{
				Log.Warn($"NyMod.Saves failed to read metadata '{path}': {ex.Message}", 2);
				continue;
			}
			if (metadata != null && metadata.RunId == runId && metadata.Kind == kind
				&& Path.GetFileNameWithoutExtension(path) == metadata.SaveId
				&& metadata.Summary != null && File.Exists(Path.ChangeExtension(path, ".save")))
			{
				yield return metadata;
			}
		}
	}

	public IReadOnlyList<string> LoadRunIds()
	{
		if (!_pathResolver.TryGetRunsDirectory(out string runsDirectory) || string.IsNullOrEmpty(runsDirectory) || !Directory.Exists(runsDirectory))
		{
			return Array.Empty<string>();
		}
		return (from name in Directory.EnumerateDirectories(runsDirectory).Select(Path.GetFileName)
			where !string.IsNullOrEmpty(name)
			select name).OrderBy<string, string>((string name) => name, StringComparer.Ordinal).ToList();
	}

	public RunArchiveMetadata? LoadRunMetadata(string runId)
	{
		if (!_pathResolver.TryGetRunMetadataPath(runId, out string metadataPath) || string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
		{
			return null;
		}
		try
		{
			string json = File.ReadAllText(metadataPath);
			return JsonSerializer.Deserialize<RunArchiveMetadata>(json, _jsonOptions);
		}
		catch (Exception ex)
		{
			Log.Warn("NyMod.Saves failed to read run metadata '" + metadataPath + "': " + ex.Message, 2);
			return null;
		}
	}

	public bool TryUpdateRunMetadata(RunArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetRunMetadataPath(metadata.RunId, out string metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			Log.Warn("NyMod.Saves could not resolve run metadata path for run '" + metadata.RunId + "'.", 2);
			return false;
		}
		if (string.IsNullOrWhiteSpace(metadata.Note))
		{
			if (File.Exists(metadataPath))
			{
				File.Delete(metadataPath);
			}
			return true;
		}
		string directoryName = Path.GetDirectoryName(metadataPath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		AtomicFile.Write(metadataPath, JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions));
		return true;
	}

	public bool TryGetRunDirectory(string runId, out string? runDirectory)
	{
		return _pathResolver.TryGetRunRoot(runId, out runDirectory);
	}

	public bool TryGetSnapshotDirectory(SaveArchiveMetadata metadata, out string? snapshotDirectory)
	{
		return _pathResolver.TryGetSnapshotDirectory(metadata.RunId, metadata.Kind, out snapshotDirectory);
	}

	public SnapshotRestore PrepareRestore(SaveArchiveMetadata metadata)
	{
		byte[] payload = ReadSnapshotPayload(metadata);
		if (!_pathResolver.TryGetActiveSavePath(out string? activeSavePath) || string.IsNullOrEmpty(activeSavePath))
		{
			throw new IOException("Could not resolve the active save path.");
		}
		var restore = new SnapshotRestore(activeSavePath, payload);
		CreateRollback(activeSavePath);
		return restore;
	}

	public void DeleteRun(string runId)
	{
		if (_pathResolver.TryGetRunRoot(runId, out string runRoot) && !string.IsNullOrEmpty(runRoot) && Directory.Exists(runRoot))
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	public string? ExportSnapshot(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string payloadPath) || string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
		{
			return null;
		}
		if (!_pathResolver.TryGetMetadataPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string metadataPath) || string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
		{
			return null;
		}
		if (!_pathResolver.TryGetExportsDirectory(out string exportsDirectory) || string.IsNullOrEmpty(exportsDirectory))
		{
			return null;
		}
		string text = Path.Combine(exportsDirectory, $"snapshot_{metadata.RunId}_{metadata.SaveId}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
		Directory.CreateDirectory(text);
		File.Copy(payloadPath, Path.Combine(text, Path.GetFileName(payloadPath)), overwrite: true);
		File.Copy(metadataPath, Path.Combine(text, Path.GetFileName(metadataPath)), overwrite: true);
		return text;
	}

	public string? ExportRun(string runId)
	{
		if (!_pathResolver.TryGetRunRoot(runId, out string runRoot) || string.IsNullOrEmpty(runRoot) || !Directory.Exists(runRoot))
		{
			return null;
		}
		if (!_pathResolver.TryGetExportsDirectory(out string exportsDirectory) || string.IsNullOrEmpty(exportsDirectory))
		{
			return null;
		}
		string text = Path.Combine(exportsDirectory, $"run_{runId}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
		CopyDirectory(runRoot, text);
		return text;
	}

	public void TrimAutosaves(string runId, int maxCount)
	{
		if (maxCount < 1)
		{
			maxCount = 1;
		}
		IReadOnlyList<SaveArchiveMetadata> source = LoadSnapshotsForRun(runId, SaveArchiveKind.Auto);
		foreach (SaveArchiveMetadata item in source.Skip(maxCount))
		{
			DeleteSnapshot(item);
		}
	}

	public void DeleteSnapshot(SaveArchiveMetadata metadata)
	{
		if (_pathResolver.TryGetPayloadPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string payloadPath) && !string.IsNullOrEmpty(payloadPath) && File.Exists(payloadPath))
		{
			File.Delete(payloadPath);
		}
		if (_pathResolver.TryGetMetadataPath(metadata.RunId, metadata.Kind, metadata.SaveId, out string metadataPath) && !string.IsNullOrEmpty(metadataPath) && File.Exists(metadataPath))
		{
			File.Delete(metadataPath);
		}
	}

	private void CreateRollback(string activeSavePath)
	{
		if ((File.Exists(activeSavePath) || File.Exists(activeSavePath + ".backup")) && _pathResolver.TryGetRollbackDirectory(out string rollbackDirectory) && !string.IsNullOrEmpty(rollbackDirectory))
		{
			Directory.CreateDirectory(rollbackDirectory);
			string text = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N");
			if (File.Exists(activeSavePath))
			{
				File.Copy(activeSavePath, Path.Combine(rollbackDirectory, text + "_" + Path.GetFileName(activeSavePath)), overwrite: true);
			}
			string text2 = activeSavePath + ".backup";
			if (File.Exists(text2))
			{
				File.Copy(text2, Path.Combine(rollbackDirectory, text + "_" + Path.GetFileName(text2)), overwrite: true);
			}
		}
	}

	private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
	{
		Directory.CreateDirectory(destinationDirectory);
		foreach (string item in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
		{
			File.Copy(item, Path.Combine(destinationDirectory, Path.GetFileName(item)), overwrite: true);
		}
		foreach (string item2 in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
		{
			CopyDirectory(item2, Path.Combine(destinationDirectory, Path.GetFileName(item2)));
		}
	}
}
