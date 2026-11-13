using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Infrastructure.Diagnostics;

namespace NyMod.Saves.Infrastructure.Persistence;

internal sealed class SaveArchivePathResolver
{
	private const string PluginDirectory = "nymod.saves";

	private const string ArchiveDirectory = "archive";
	private const string SingleplayerDirectory = "singleplayer";

	private const string RunsDirectory = "runs";

	private const string ConfigFileName = "config.json";

	public bool TryGetArchiveRoot(out string? archiveRoot)
	{
		archiveRoot = null;
		try
		{
			archiveRoot = Globalize(SaveManager.Instance.GetProfileScopedPath(Path.Combine(PluginDirectory, ArchiveDirectory)));
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
			configPath = Globalize(SaveManager.Instance.GetProfileScopedPath(Path.Combine(PluginDirectory, ConfigFileName)));
			return !string.IsNullOrEmpty(configPath);
		}
		catch
		{
			return false;
		}
	}

	public bool TryGetRunsDirectory(out string? runsDirectory)
	{
		runsDirectory = null;
		if (!TryGetArchiveRoot(out string archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}
		runsDirectory = Path.Combine(archiveRoot, SingleplayerDirectory, RunsDirectory);
		return true;
	}

	public bool TryGetRunRoot(string runId, out string? runRoot)
	{
		runRoot = null;
		if (!TryGetRunsDirectory(out string runsDirectory) || string.IsNullOrEmpty(runsDirectory))
		{
			return false;
		}
		runRoot = Path.Combine(runsDirectory, runId);
		return true;
	}

	public bool TryGetRunMetadataPath(string runId, out string? metadataPath)
	{
		metadataPath = null;
		if (!TryGetRunRoot(runId, out string runRoot) || string.IsNullOrEmpty(runRoot))
		{
			return false;
		}
		metadataPath = Path.Combine(runRoot, "run.json");
		return true;
	}

	public bool TryGetRollbackDirectory(out string? rollbackDirectory)
	{
		rollbackDirectory = null;
		if (!TryGetArchiveRoot(out string archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}
		rollbackDirectory = Path.Combine(archiveRoot, SingleplayerDirectory, "rollback");
		return true;
	}

	public bool TryGetExportsDirectory(out string? exportsDirectory)
	{
		exportsDirectory = null;
		if (!TryGetArchiveRoot(out string archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}
		exportsDirectory = Path.Combine(archiveRoot, SingleplayerDirectory, "exports");
		return true;
	}

	public bool TryGetSnapshotDirectory(string runId, SaveArchiveKind kind, out string? snapshotDirectory)
	{
		snapshotDirectory = null;
		if (!TryGetRunRoot(runId, out string runRoot) || string.IsNullOrEmpty(runRoot))
		{
			return false;
		}
		snapshotDirectory = Path.Combine(runRoot, (kind == SaveArchiveKind.Auto) ? "autosaves" : "manual");
		return true;
	}

	public bool TryGetPayloadPath(string runId, SaveArchiveKind kind, string saveId, out string? payloadPath)
	{
		payloadPath = null;
		if (!TryGetSnapshotDirectory(runId, kind, out string snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			return false;
		}
		payloadPath = Path.Combine(snapshotDirectory, saveId + ".save");
		return true;
	}

	public bool TryGetMetadataPath(string runId, SaveArchiveKind kind, string saveId, out string? metadataPath)
	{
		metadataPath = null;
		if (!TryGetSnapshotDirectory(runId, kind, out string snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			return false;
		}
		metadataPath = Path.Combine(snapshotDirectory, saveId + ".json");
		return true;
	}

	public bool TryGetActiveSavePath(out string? activeSavePath)
	{
		ModDiagnostics.Trace("Resolving active save path");
		activeSavePath = null;
		try
		{
			activeSavePath = Globalize(SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, "current_run.save")));
			return !string.IsNullOrEmpty(activeSavePath);
		}
		catch
		{
			return false;
		}
	}

	private static string Globalize(string path)
	{
		ModDiagnostics.Trace("Globalizing save path: " + path);
		var result = path.StartsWith("user://", StringComparison.OrdinalIgnoreCase) ? ProjectSettings.GlobalizePath(path) : path;
		ModDiagnostics.Trace("Save path resolved");
		return result;
	}
}
