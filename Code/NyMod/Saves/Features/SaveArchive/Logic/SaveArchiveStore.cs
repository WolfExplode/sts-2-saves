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

	public bool TryReadActivePayload(bool isMultiplayer, out byte[]? payloadBytes, out string? sourceFileName)
	{
		payloadBytes = null;
		sourceFileName = null;
		if (!_pathResolver.TryGetActiveSavePath(isMultiplayer, out string activeSavePath) || string.IsNullOrEmpty(activeSavePath))
		{
			Log.Warn("NyMod.Saves could not resolve the active " + (isMultiplayer ? "multiplayer" : "singleplayer") + " save path.", 2);
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

	public void SaveSnapshot(SaveArchiveMetadata metadata, byte[] payloadBytes)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string payloadPath) || string.IsNullOrEmpty(payloadPath))
		{
			Log.Warn("NyMod.Saves could not resolve payload path for snapshot '" + metadata.SaveId + "'.", 2);
			return;
		}
		if (!_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			Log.Warn("NyMod.Saves could not resolve metadata path for snapshot '" + metadata.SaveId + "'.", 2);
			return;
		}
		string directoryName = Path.GetDirectoryName(payloadPath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.WriteAllBytes(payloadPath, payloadBytes);
		File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, _jsonOptions));
		Log.Info($"NyMod.Saves wrote snapshot payload '{payloadPath}' and metadata '{metadataPath}'.", 2);
	}

	public bool TryUpdateSnapshotMetadata(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			Log.Warn("NyMod.Saves could not resolve metadata path for snapshot '" + metadata.SaveId + "'.", 2);
			return false;
		}
		string directoryName = Path.GetDirectoryName(metadataPath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, _jsonOptions));
		return true;
	}

	public IReadOnlyList<SaveArchiveMetadata> LoadSnapshotsForRun(bool isMultiplayer, string runId, SaveArchiveKind kind)
	{
		if (!_pathResolver.TryGetSnapshotDirectory(isMultiplayer, runId, kind, out string snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory) || !Directory.Exists(snapshotDirectory))
		{
			return Array.Empty<SaveArchiveMetadata>();
		}
		List<SaveArchiveMetadata> list = new List<SaveArchiveMetadata>();
		foreach (string item in Directory.EnumerateFiles(snapshotDirectory, "*.json", SearchOption.TopDirectoryOnly))
		{
			try
			{
				string json = File.ReadAllText(item);
				SaveArchiveMetadata saveArchiveMetadata = JsonSerializer.Deserialize<SaveArchiveMetadata>(json, _jsonOptions);
				if (saveArchiveMetadata != null)
				{
					list.Add(saveArchiveMetadata);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("NyMod.Saves failed to read metadata '" + item + "': " + ex.Message, 2);
			}
		}
		return list.OrderByDescending((SaveArchiveMetadata snapshot) => snapshot.CreatedUtc).ToList();
	}

	public IReadOnlyList<string> LoadRunIds(bool isMultiplayer)
	{
		if (!_pathResolver.TryGetModeRoot(isMultiplayer, out string modeRoot) || string.IsNullOrEmpty(modeRoot) || !Directory.Exists(modeRoot))
		{
			return Array.Empty<string>();
		}
		return (from name in Directory.EnumerateDirectories(modeRoot).Select(Path.GetFileName)
			where !string.IsNullOrEmpty(name)
			select name).OrderBy<string, string>((string name) => name, StringComparer.Ordinal).ToList();
	}

	public RunArchiveMetadata? LoadRunMetadata(bool isMultiplayer, string runId)
	{
		if (!_pathResolver.TryGetRunMetadataPath(isMultiplayer, runId, out string metadataPath) || string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
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
		if (!_pathResolver.TryGetRunMetadataPath(metadata.IsMultiplayer, metadata.RunId, out string metadataPath) || string.IsNullOrEmpty(metadataPath))
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
		File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, _jsonOptions));
		return true;
	}

	public bool TryGetRunDirectory(bool isMultiplayer, string runId, out string? runDirectory)
	{
		return _pathResolver.TryGetRunRoot(isMultiplayer, runId, out runDirectory);
	}

	public bool TryGetSnapshotDirectory(SaveArchiveMetadata metadata, out string? snapshotDirectory)
	{
		return _pathResolver.TryGetSnapshotDirectory(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, out snapshotDirectory);
	}

	public bool TryRestoreSnapshot(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string payloadPath) || string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
		{
			return false;
		}
		if (!_pathResolver.TryGetActiveSavePath(metadata.IsMultiplayer, out string activeSavePath) || string.IsNullOrEmpty(activeSavePath))
		{
			return false;
		}
		CreateRollback(activeSavePath, metadata.IsMultiplayer);
		string directoryName = Path.GetDirectoryName(activeSavePath);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.Copy(payloadPath, activeSavePath, overwrite: true);
		File.Copy(payloadPath, activeSavePath + ".backup", overwrite: true);
		return true;
	}

	public void DeleteRun(bool isMultiplayer, string runId)
	{
		if (_pathResolver.TryGetRunRoot(isMultiplayer, runId, out string runRoot) && !string.IsNullOrEmpty(runRoot) && Directory.Exists(runRoot))
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	public string? ExportSnapshot(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string payloadPath) || string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
		{
			return null;
		}
		if (!_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string metadataPath) || string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
		{
			return null;
		}
		if (!_pathResolver.TryGetExportsDirectory(metadata.IsMultiplayer, out string exportsDirectory) || string.IsNullOrEmpty(exportsDirectory))
		{
			return null;
		}
		string text = Path.Combine(exportsDirectory, $"snapshot_{metadata.RunId}_{metadata.SaveId}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
		Directory.CreateDirectory(text);
		File.Copy(payloadPath, Path.Combine(text, Path.GetFileName(payloadPath)), overwrite: true);
		File.Copy(metadataPath, Path.Combine(text, Path.GetFileName(metadataPath)), overwrite: true);
		return text;
	}

	public string? ExportRun(bool isMultiplayer, string runId)
	{
		if (!_pathResolver.TryGetRunRoot(isMultiplayer, runId, out string runRoot) || string.IsNullOrEmpty(runRoot) || !Directory.Exists(runRoot))
		{
			return null;
		}
		if (!_pathResolver.TryGetExportsDirectory(isMultiplayer, out string exportsDirectory) || string.IsNullOrEmpty(exportsDirectory))
		{
			return null;
		}
		string text = Path.Combine(exportsDirectory, $"run_{runId}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
		CopyDirectory(runRoot, text);
		return text;
	}

	public void TrimAutosaves(string runId, bool isMultiplayer, int maxCount)
	{
		if (maxCount < 1)
		{
			maxCount = 1;
		}
		IReadOnlyList<SaveArchiveMetadata> source = LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Auto);
		foreach (SaveArchiveMetadata item in source.Skip(maxCount))
		{
			DeleteSnapshot(item);
		}
	}

	public void DeleteSnapshot(SaveArchiveMetadata metadata)
	{
		if (_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string payloadPath) && !string.IsNullOrEmpty(payloadPath) && File.Exists(payloadPath))
		{
			File.Delete(payloadPath);
		}
		if (_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string metadataPath) && !string.IsNullOrEmpty(metadataPath) && File.Exists(metadataPath))
		{
			File.Delete(metadataPath);
		}
	}

	private void CreateRollback(string activeSavePath, bool isMultiplayer)
	{
		if ((File.Exists(activeSavePath) || File.Exists(activeSavePath + ".backup")) && _pathResolver.TryGetRollbackDirectory(isMultiplayer, out string rollbackDirectory) && !string.IsNullOrEmpty(rollbackDirectory))
		{
			Directory.CreateDirectory(rollbackDirectory);
			string text = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
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
