using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
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

	public bool TryGetModeRoot(bool isMultiplayer, out string? modeRoot)
	{
		modeRoot = null;
		if (!TryGetArchiveRoot(out string? archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}

		modeRoot = Path.Combine(archiveRoot, isMultiplayer ? "multiplayer" : "singleplayer", RunsDirectory);
		return true;
	}

	/// <summary>
	/// Resolves the mode directory <c>archiveRoot/&lt;multiplayer|singleplayer&gt;</c>
	/// (the parent of <c>runs/</c>). Used to detect whether the auto-capture sweep
	/// has ever run for this mode on this profile.
	/// </summary>
	public bool TryGetModeDirectory(bool isMultiplayer, out string? modeDirectory)
	{
		modeDirectory = null;
		if (!TryGetArchiveRoot(out string? archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}

		modeDirectory = Path.Combine(archiveRoot, isMultiplayer ? "multiplayer" : "singleplayer");
		return true;
	}

	public bool TryGetRunRoot(bool isMultiplayer, string runId, out string? runRoot)
	{
		runRoot = null;
		if (!TryGetModeRoot(isMultiplayer, out string? modeRoot) || string.IsNullOrEmpty(modeRoot))
		{
			return false;
		}

		runRoot = Path.Combine(modeRoot, runId);
		return true;
	}

	public bool TryGetRunMetadataPath(bool isMultiplayer, string runId, out string? metadataPath)
	{
		metadataPath = null;
		if (!TryGetRunRoot(isMultiplayer, runId, out string? runRoot) || string.IsNullOrEmpty(runRoot))
		{
			return false;
		}

		metadataPath = Path.Combine(runRoot, "run.json");
		return true;
	}

	public bool TryGetRollbackDirectory(bool isMultiplayer, out string? rollbackDirectory)
	{
		rollbackDirectory = null;
		if (!TryGetArchiveRoot(out string? archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}

		rollbackDirectory = Path.Combine(archiveRoot, isMultiplayer ? "multiplayer" : "singleplayer", "rollback");
		return true;
	}

	public bool TryGetExportsDirectory(bool isMultiplayer, out string? exportsDirectory)
	{
		exportsDirectory = null;
		if (!TryGetArchiveRoot(out string? archiveRoot) || string.IsNullOrEmpty(archiveRoot))
		{
			return false;
		}

		exportsDirectory = Path.Combine(archiveRoot, isMultiplayer ? "multiplayer" : "singleplayer", "exports");
		return true;
	}

	public bool TryGetSnapshotDirectory(bool isMultiplayer, string runId, SaveArchiveKind kind, out string? snapshotDirectory)
	{
		snapshotDirectory = null;
		if (!TryGetRunRoot(isMultiplayer, runId, out string? runRoot) || string.IsNullOrEmpty(runRoot))
		{
			return false;
		}

		snapshotDirectory = Path.Combine(runRoot, kind == SaveArchiveKind.Auto ? "autosaves" : "manual");
		return true;
	}

	public bool TryGetPayloadPath(bool isMultiplayer, string runId, SaveArchiveKind kind, string saveId, out string? payloadPath)
	{
		payloadPath = null;
		if (!TryGetSnapshotDirectory(isMultiplayer, runId, kind, out string? snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			return false;
		}

		payloadPath = Path.Combine(snapshotDirectory, saveId + ".save");
		return true;
	}

	public bool TryGetMetadataPath(bool isMultiplayer, string runId, SaveArchiveKind kind, string saveId, out string? metadataPath)
	{
		metadataPath = null;
		if (!TryGetSnapshotDirectory(isMultiplayer, runId, kind, out string? snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
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
			string fileName = isMultiplayer ? RunSaveManager.multiplayerRunSaveFileName : RunSaveManager.runSaveFileName;
			activeSavePath = Globalize(SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, fileName)));
			return !string.IsNullOrEmpty(activeSavePath);
		}
		catch
		{
			return false;
		}
	}

	private static string Globalize(string path)
	{
		return path.StartsWith("user://", StringComparison.OrdinalIgnoreCase)
			? ProjectSettings.GlobalizePath(path)
			: path;
	}
}