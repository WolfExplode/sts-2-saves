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
		if (!_pathResolver.TryGetActiveSavePath(isMultiplayer, out string? activeSavePath) || string.IsNullOrEmpty(activeSavePath))
		{
			Log.Warn($"NyMod.Saves could not resolve the active {(isMultiplayer ? "multiplayer" : "singleplayer")} save path.");
			return false;
		}

		string resolvedPath = activeSavePath;
		if (!File.Exists(resolvedPath))
		{
			string backupPath = activeSavePath + ".backup";
			if (!File.Exists(backupPath))
			{
				Log.Warn($"NyMod.Saves found no active save file at '{activeSavePath}' or backup '{backupPath}'.");
				return false;
			}

			resolvedPath = backupPath;
		}

		payloadBytes = File.ReadAllBytes(resolvedPath);
		sourceFileName = Path.GetFileName(resolvedPath);
		return true;
	}

	public void SaveSnapshot(SaveArchiveMetadata metadata, byte[] payloadBytes)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? payloadPath) || string.IsNullOrEmpty(payloadPath))
		{
			Log.Warn($"NyMod.Saves could not resolve payload path for snapshot '{metadata.SaveId}'.");
			return;
		}

		if (!_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			Log.Warn($"NyMod.Saves could not resolve metadata path for snapshot '{metadata.SaveId}'.");
			return;
		}

		string? directory = Path.GetDirectoryName(payloadPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllBytes(payloadPath, payloadBytes);
		File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, _jsonOptions));
		Log.Info($"NyMod.Saves wrote snapshot payload '{payloadPath}' and metadata '{metadataPath}'.");
	}

	public bool TryUpdateSnapshotMetadata(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			Log.Warn($"NyMod.Saves could not resolve metadata path for snapshot '{metadata.SaveId}'.");
			return false;
		}

		string? directory = Path.GetDirectoryName(metadataPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, _jsonOptions));
		return true;
	}

	public IReadOnlyList<SaveArchiveMetadata> LoadSnapshotsForRun(bool isMultiplayer, string runId, SaveArchiveKind kind)
	{
		if (!_pathResolver.TryGetSnapshotDirectory(isMultiplayer, runId, kind, out string? snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory) || !Directory.Exists(snapshotDirectory))
		{
			return Array.Empty<SaveArchiveMetadata>();
		}

		List<SaveArchiveMetadata> snapshots = new List<SaveArchiveMetadata>();
		foreach (string metadataFile in Directory.EnumerateFiles(snapshotDirectory, "*.json", SearchOption.TopDirectoryOnly))
		{
			try
			{
				string json = File.ReadAllText(metadataFile);
				SaveArchiveMetadata? snapshot = JsonSerializer.Deserialize<SaveArchiveMetadata>(json, _jsonOptions);
				if (snapshot != null)
				{
					snapshots.Add(snapshot);
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"NyMod.Saves failed to read metadata '{metadataFile}': {ex.Message}");
			}
		}

		return snapshots
			.OrderByDescending(static snapshot => snapshot.CreatedUtc)
			.ToList();
	}

	public IReadOnlyList<string> LoadRunIds(bool isMultiplayer)
	{
		if (!_pathResolver.TryGetModeRoot(isMultiplayer, out string? modeRoot) || string.IsNullOrEmpty(modeRoot) || !Directory.Exists(modeRoot))
		{
			return Array.Empty<string>();
		}

		return Directory.EnumerateDirectories(modeRoot)
			.Select(Path.GetFileName)
			.Where(static name => !string.IsNullOrEmpty(name))
			.OrderBy(static name => name, StringComparer.Ordinal)
			.ToList()!;
	}

	public RunArchiveMetadata? LoadRunMetadata(bool isMultiplayer, string runId)
	{
		if (!_pathResolver.TryGetRunMetadataPath(isMultiplayer, runId, out string? metadataPath) || string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
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
			Log.Warn($"NyMod.Saves failed to read run metadata '{metadataPath}': {ex.Message}");
			return null;
		}
	}

	public bool TryUpdateRunMetadata(RunArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetRunMetadataPath(metadata.IsMultiplayer, metadata.RunId, out string? metadataPath) || string.IsNullOrEmpty(metadataPath))
		{
			Log.Warn($"NyMod.Saves could not resolve run metadata path for run '{metadata.RunId}'.");
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

		string? directory = Path.GetDirectoryName(metadataPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, _jsonOptions));
		return true;
	}

	public bool TryGetRunDirectory(bool isMultiplayer, string runId, out string? runDirectory)
	{
		return _pathResolver.TryGetRunRoot(isMultiplayer, runId, out runDirectory);
	}

	/// <summary>
	/// Returns true when the per-mode archive directory
	/// (<c>nymod.saves/archive/&lt;multiplayer|singleplayer&gt;</c>) already exists,
	/// indicating the auto-capture sweep has run at least once for this mode.
	/// </summary>
	public bool ModeDirectoryExists(bool isMultiplayer)
	{
		if (!_pathResolver.TryGetModeDirectory(isMultiplayer, out string? modeDirectory) || string.IsNullOrEmpty(modeDirectory))
		{
			return false;
		}

		return Directory.Exists(modeDirectory);
	}

	public bool TryGetSnapshotDirectory(SaveArchiveMetadata metadata, out string? snapshotDirectory)
	{
		return _pathResolver.TryGetSnapshotDirectory(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, out snapshotDirectory);
	}

	public bool TryReadSnapshotPayload(SaveArchiveMetadata metadata, out byte[]? payloadBytes)
	{
		payloadBytes = null;
		if (!_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? payloadPath) || string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
		{
			return false;
		}

		try
		{
			payloadBytes = File.ReadAllBytes(payloadPath);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves failed to read snapshot payload '{payloadPath}': {ex.Message}");
			return false;
		}
	}

	public bool TryRestoreSnapshot(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? payloadPath) || string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
		{
			return false;
		}

		if (!_pathResolver.TryGetActiveSavePath(metadata.IsMultiplayer, out string? activeSavePath) || string.IsNullOrEmpty(activeSavePath))
		{
			return false;
		}

		CreateRollback(activeSavePath, metadata.IsMultiplayer);
		string? targetDirectory = Path.GetDirectoryName(activeSavePath);
		if (!string.IsNullOrEmpty(targetDirectory))
		{
			Directory.CreateDirectory(targetDirectory);
		}

		File.Copy(payloadPath, activeSavePath, overwrite: true);
		File.Copy(payloadPath, activeSavePath + ".backup", overwrite: true);
		return true;
	}

	public void DeleteRun(bool isMultiplayer, string runId)
	{
		if (!_pathResolver.TryGetRunRoot(isMultiplayer, runId, out string? runRoot) || string.IsNullOrEmpty(runRoot) || !Directory.Exists(runRoot))
		{
			return;
		}

		Directory.Delete(runRoot, recursive: true);
	}

	public string? ExportSnapshot(SaveArchiveMetadata metadata)
	{
		if (!_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? payloadPath) || string.IsNullOrEmpty(payloadPath) || !File.Exists(payloadPath))
		{
			return null;
		}

		if (!_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? metadataPath) || string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
		{
			return null;
		}

		if (!_pathResolver.TryGetExportsDirectory(metadata.IsMultiplayer, out string? exportsDirectory) || string.IsNullOrEmpty(exportsDirectory))
		{
			return null;
		}

		string exportDirectory = Path.Combine(exportsDirectory, $"snapshot_{metadata.RunId}_{metadata.SaveId}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
		Directory.CreateDirectory(exportDirectory);
		File.Copy(payloadPath, Path.Combine(exportDirectory, Path.GetFileName(payloadPath)), overwrite: true);
		File.Copy(metadataPath, Path.Combine(exportDirectory, Path.GetFileName(metadataPath)), overwrite: true);
		return exportDirectory;
	}

	public string? ExportRun(bool isMultiplayer, string runId)
	{
		if (!_pathResolver.TryGetRunRoot(isMultiplayer, runId, out string? runRoot) || string.IsNullOrEmpty(runRoot) || !Directory.Exists(runRoot))
		{
			return null;
		}

		if (!_pathResolver.TryGetExportsDirectory(isMultiplayer, out string? exportsDirectory) || string.IsNullOrEmpty(exportsDirectory))
		{
			return null;
		}

		string exportDirectory = Path.Combine(exportsDirectory, $"run_{runId}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
		CopyDirectory(runRoot, exportDirectory);
		return exportDirectory;
	}

	public void TrimAutosaves(string runId, bool isMultiplayer, int maxCount)
	{
		if (maxCount < 1)
		{
			maxCount = 1;
		}

		IReadOnlyList<SaveArchiveMetadata> snapshots = LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Auto);
		foreach (SaveArchiveMetadata snapshot in snapshots.Skip(maxCount))
		{
			DeleteSnapshot(snapshot);
		}
	}

	public void DeleteSnapshot(SaveArchiveMetadata metadata)
	{
		if (_pathResolver.TryGetPayloadPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? payloadPath) && !string.IsNullOrEmpty(payloadPath) && File.Exists(payloadPath))
		{
			File.Delete(payloadPath);
		}

		if (_pathResolver.TryGetMetadataPath(metadata.IsMultiplayer, metadata.RunId, metadata.Kind, metadata.SaveId, out string? metadataPath) && !string.IsNullOrEmpty(metadataPath) && File.Exists(metadataPath))
		{
			File.Delete(metadataPath);
		}
	}

	private void CreateRollback(string activeSavePath, bool isMultiplayer)
	{
		if (!File.Exists(activeSavePath) && !File.Exists(activeSavePath + ".backup"))
		{
			return;
		}

		if (!_pathResolver.TryGetRollbackDirectory(isMultiplayer, out string? rollbackDirectory) || string.IsNullOrEmpty(rollbackDirectory))
		{
			return;
		}

		Directory.CreateDirectory(rollbackDirectory);
		string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
		if (File.Exists(activeSavePath))
		{
			File.Copy(activeSavePath, Path.Combine(rollbackDirectory, $"{stamp}_{Path.GetFileName(activeSavePath)}"), overwrite: true);
		}

		string backupPath = activeSavePath + ".backup";
		if (File.Exists(backupPath))
		{
			File.Copy(backupPath, Path.Combine(rollbackDirectory, $"{stamp}_{Path.GetFileName(backupPath)}"), overwrite: true);
		}
	}

	private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
	{
		Directory.CreateDirectory(destinationDirectory);
		foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
		{
			File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
		}

		foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
		{
			CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
		}
	}
}