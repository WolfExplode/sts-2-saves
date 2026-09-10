using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Features.SaveArchive.Logic;

namespace NyMod.Saves.Infrastructure.Persistence;

internal sealed class SaveArchivePathResolver
{
	private const string PluginDirectory = "nymod.saves";

	private const string ArchiveDirectory = "archive";

	private const string RunsDirectory = "runs";

	private const string ConfigFileName = "config.json";

	public bool TryGetArchiveRoot(out string? archiveRoot)
	{
		archiveRoot = null;
		try
		{
			archiveRoot = Globalize(SaveManager.Instance.GetProfileScopedPath(Path.Combine("nymod.saves", "archive")));
			return !string.IsNullOrEmpty(archiveRoot);
		}
		catch
		{
			return false;
		}
	}

	public bool TryGetConfigPath(out string? configPath)
	{
		configPath = null;
		try
		{
			configPath = Globalize(SaveManager.Instance.GetProfileScopedPath(Path.Combine("nymod.saves", "config.json")));
			return !string.IsNullOrEmpty(configPath);
		}
		catch
		{
			return false;
		}
	}

	public bool TryGetModeRoot(bool isMultiplayer, out string? modeRoot)
	{
		modeRoot = null;
		if (!TryGetArchiveRoot(out string archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}
		modeRoot = Path.Combine(archiveRoot, isMultiplayer ? "multiplayer" : "singleplayer", "runs");
		return true;
	}

	public bool TryGetRunRoot(bool isMultiplayer, string runId, out string? runRoot)
	{
		runRoot = null;
		if (!TryGetModeRoot(isMultiplayer, out string modeRoot) || string.IsNullOrEmpty(modeRoot))
		{
			return false;
		}
		runRoot = Path.Combine(modeRoot, runId);
		return true;
	}

	public bool TryGetRunMetadataPath(bool isMultiplayer, string runId, out string? metadataPath)
	{
		metadataPath = null;
		if (!TryGetRunRoot(isMultiplayer, runId, out string runRoot) || string.IsNullOrEmpty(runRoot))
		{
			return false;
		}
		metadataPath = Path.Combine(runRoot, "run.json");
		return true;
	}

	public bool TryGetRollbackDirectory(bool isMultiplayer, out string? rollbackDirectory)
	{
		rollbackDirectory = null;
		if (!TryGetArchiveRoot(out string archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}
		rollbackDirectory = Path.Combine(archiveRoot, isMultiplayer ? "multiplayer" : "singleplayer", "rollback");
		return true;
	}

	public bool TryGetExportsDirectory(bool isMultiplayer, out string? exportsDirectory)
	{
		exportsDirectory = null;
		if (!TryGetArchiveRoot(out string archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}
		exportsDirectory = Path.Combine(archiveRoot, isMultiplayer ? "multiplayer" : "singleplayer", "exports");
		return true;
	}

	public bool TryGetSnapshotDirectory(bool isMultiplayer, string runId, SaveArchiveKind kind, out string? snapshotDirectory)
	{
		snapshotDirectory = null;
		if (!TryGetRunRoot(isMultiplayer, runId, out string runRoot) || string.IsNullOrEmpty(runRoot))
		{
			return false;
		}
		snapshotDirectory = Path.Combine(runRoot, (kind == SaveArchiveKind.Auto) ? "autosaves" : "manual");
		return true;
	}

	public bool TryGetPayloadPath(bool isMultiplayer, string runId, SaveArchiveKind kind, string saveId, out string? payloadPath)
	{
		payloadPath = null;
		if (!TryGetSnapshotDirectory(isMultiplayer, runId, kind, out string snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			return false;
		}
		payloadPath = Path.Combine(snapshotDirectory, saveId + ".save");
		return true;
	}

	public bool TryGetMetadataPath(bool isMultiplayer, string runId, SaveArchiveKind kind, string saveId, out string? metadataPath)
	{
		metadataPath = null;
		if (!TryGetSnapshotDirectory(isMultiplayer, runId, kind, out string snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			return false;
		}
		metadataPath = Path.Combine(snapshotDirectory, saveId + ".json");
		return true;
	}

	public bool TryGetActiveSavePath(bool isMultiplayer, out string? activeSavePath)
	{
		activeSavePath = null;
		try
		{
			string path = (isMultiplayer ? "current_run_mp.save" : "current_run.save");
			activeSavePath = Globalize(SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, path)));
			return !string.IsNullOrEmpty(activeSavePath);
		}
		catch
		{
			return false;
		}
	}

	private static string Globalize(string path)
	{
		return path.StartsWith("user://", StringComparison.OrdinalIgnoreCase) ? ProjectSettings.GlobalizePath(path) : path;
	}
}
