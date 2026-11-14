using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Features.SaveBrowser.Presentation;
using NyMod.Saves.Infrastructure.Compat;
using NyMod.Saves.Infrastructure.Diagnostics;
using NyMod.Saves.Infrastructure.Localization;

namespace NyMod.Saves.Features.SaveBrowser.Logic;

internal static class SaveBrowserCoordinator
{
	private static readonly Dictionary<ulong, SaveBrowserScreen> _screens = new Dictionary<ulong, SaveBrowserScreen>();

	private static int _loadInFlight;


	internal static void RefreshMainMenuAfterArchiveChange()
	{
		NGame instance = NGame.Instance;
		if (instance != null)
		{
			NMainMenu mainMenu = instance.MainMenu;
			if (mainMenu != null)
			{
				mainMenu.RefreshButtons();
			}
		}
	}

	public static void OpenForMainMenu(NMainMenu mainMenu)
	{
		Open((NSubmenuStack)(object)mainMenu.SubmenuStack, launchedFromRun: false);
	}

	public static void OpenForPauseMenu(NPauseMenu pauseMenu)
	{
		Node parent = ((Node)pauseMenu).GetParent();
		NSubmenuStack val = (NSubmenuStack)(((object)((parent is NSubmenuStack) ? parent : null)) ?? ((object)Traverse.Create((object)pauseMenu).Field("_stack").GetValue<NSubmenuStack>()));
		if (val != null)
		{
			Open(val, launchedFromRun: true);
		}
	}

	public static async Task<bool> LoadSnapshotAsync(SaveArchiveMetadata metadata, bool launchedFromRun)
	{
		if (Interlocked.CompareExchange(ref _loadInFlight, 1, 0) != 0)
		{
			return false;
		}
		var archive = ServiceRegistry.ArchiveService;
		SnapshotRestore? restore = null;
		bool runWasCleanedUp = false;
		bool runSetupStarted = false;
		using var profile = new SnapshotLoadProfile(launchedFromRun ? "pause menu" : "main menu");
		bool loaded = false;
		archive.IsRestoring = true;
		try
		{
			if (RunManager.Instance.IsInProgress && RunManager.Instance.NetService.Type != NetGameType.Singleplayer)
			{
				throw new InvalidOperationException("Snapshots can only be loaded in singleplayer.");
			}
			profile.Begin("Wait for pending save");
			// Complete the game's current write before replacing either save file.
			if (SaveManager.Instance.CurrentRunSaveTask is Task pendingSave)
			{
				await pendingSave;
			}
			profile.Begin("Read snapshot and back up previous save");
			restore = archive.PrepareRestore(metadata);
			profile.Begin("Install save files");
			restore.Install();
			profile.Begin("Read and migrate save");
			// Use the game's reader so older snapshots receive its normal migrations.
			var read = SaveManager.Instance.LoadRunSave();
			if (!read.Success || read.SaveData == null || read.SaveData.Players.Count != 1)
			{
				throw new InvalidDataException("The snapshot could not be read as a singleplayer run.");
			}
			SerializableRun save = read.SaveData;
			profile.Begin("Stop audio and clean up old run");
			NRunMusicController.Instance?.StopMusic();
			NAudioManager.Instance?.StopMusic();
			if (RunManager.Instance.IsInProgress)
			{
				runWasCleanedUp = true;
				RunManager.Instance.CleanUp(true);
			}
			profile.Begin("Rebuild run state");
			RunState state = RunState.FromSerializable(save);
			profile.Begin("Set up saved run");
			runSetupStarted = true;
			await RunManagerCompat.SetUpSavedSinglePlayer(RunManager.Instance, state, save);
			profile.Begin("Initialize reaction service");
			NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
			profile.Begin("Game LoadRun");
			await NGame.Instance.LoadRun(state, save.PreFinishedRoom);
			profile.Begin("Update archive metadata");
			archive.MarkRestored(metadata);
			loaded = true;
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves failed to load '{metadata.SaveId}': {ex}", 2);
			profile.Begin("Recover failed load");
			bool recovered = await RecoverFailedLoadAsync(restore, runWasCleanedUp || runSetupStarted);
			ShowPopup("POPUP.loadFailed.title", recovered
				? "POPUP.loadFailed.restoreFailed.body"
				: "POPUP.loadFailed.recoveryFailed.body");
			return false;
		}
		finally
		{
			archive.IsRestoring = false;
			Volatile.Write(ref _loadInFlight, 0);
			profile.Report(loaded);
		}
	}

	private static async Task<bool> RecoverFailedLoadAsync(SnapshotRestore? restore, bool returnToMainMenu)
	{
		bool recovered = true;
		if (RunManager.Instance.IsInProgress && returnToMainMenu)
		{
			try
			{
				RunManager.Instance.CleanUp(true);
			}
			catch (Exception ex)
			{
				Log.Warn($"NyMod.Saves cleanup after failed load failed: {ex}", 2);
			}
		}
		try
		{
			if (SaveManager.Instance.CurrentRunSaveTask is Task pendingSave)
			{
				await pendingSave;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves pending write failed during recovery: {ex}", 2);
		}
		try
		{
			restore?.RollBack();
		}
		catch (Exception ex)
		{
			recovered = false;
			Log.Warn($"NyMod.Saves could not recover the previous save files: {ex}", 2);
		}
		if (returnToMainMenu)
		{
			try
			{
				await NGame.Instance.ReturnToMainMenu();
			}
			catch (Exception ex)
			{
				recovered = false;
				Log.Warn($"NyMod.Saves could not return to the main menu: {ex}", 2);
			}
		}
		return recovered;
	}

	public static bool OpenFolderForSnapshot(SaveArchiveMetadata metadata)
	{
		if (!ServiceRegistry.ArchiveService.TryGetSnapshotDirectory(metadata, out string snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			ShowPopup("POPUP.openFolderFailed.title", "POPUP.openFolderFailed.body", ("Path", metadata.SaveId));
			return false;
		}
		return OpenFolder(snapshotDirectory);
	}

	public static bool OpenFolderForRun(string runId)
	{
		if (!ServiceRegistry.ArchiveService.TryGetRunDirectory(runId, out string runDirectory) || string.IsNullOrEmpty(runDirectory))
		{
			ShowPopup("POPUP.openFolderFailed.title", "POPUP.openFolderFailed.body", ("Path", runId));
			return false;
		}
		return OpenFolder(runDirectory);
	}

	public static bool IsCurrentRun(string runId)
	{
		string runId2;
		return ServiceRegistry.ArchiveService.TryGetCurrentRunId(out runId2) && string.Equals(runId2, runId, StringComparison.Ordinal);
	}

	private static void Open(NSubmenuStack stack, bool launchedFromRun)
	{
		ulong instanceId = ((GodotObject)stack).GetInstanceId();
		if (!_screens.TryGetValue(instanceId, out SaveBrowserScreen value) || !GodotObject.IsInstanceValid((GodotObject)(object)value))
		{
			value = SaveBrowserScreen.Create();
			((CanvasItem)value).Visible = false;
			((Node)stack).AddChild((Node)(object)value, false, (Node.InternalMode)0);
			_screens[instanceId] = value;
			value.TreeExited += () => _screens.Remove(instanceId);
		}
		value.Configure(launchedFromRun);
		stack.Push((NSubmenu)(object)value);
	}


	private static bool OpenFolder(string path)
	{
		if (!Directory.Exists(path))
		{
			ShowPopup("POPUP.openFolderFailed.title", "POPUP.openFolderFailed.body", ("Path", path));
			return false;
		}
		Error val = OS.ShellOpen(new Uri(path).AbsoluteUri);
		if ((long)val > 0L)
		{
			ShowPopup("POPUP.openFolderFailed.title", "POPUP.openFolderFailed.body", ("Path", path));
			return false;
		}
		return true;
	}

	private static void ShowPopup(string titleKey, string bodyKey, params (string Name, object? Value)[] variables)
	{
		NErrorPopup val = NErrorPopup.Create(SaveUiText.Get(titleKey), (variables.Length == 0) ? SaveUiText.Get(bodyKey) : SaveUiText.Format(bodyKey, variables), false);
		if (val != null && NModalContainer.Instance != null)
		{
			NModalContainer.Instance.Add((Node)(object)val, true);
		}
	}
}
