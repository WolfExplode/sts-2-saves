using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Features.SaveBrowser.Presentation;
using NyMod.Saves.Infrastructure.Compat;
using NyMod.Saves.Infrastructure.Localization;

namespace NyMod.Saves.Features.SaveBrowser.Logic;

internal static class SaveBrowserCoordinator
{
	private static readonly Dictionary<ulong, SaveBrowserScreen> _screens = new Dictionary<ulong, SaveBrowserScreen>();
	private static int _loadInFlight;

	internal static bool IsLoadInFlight => System.Threading.Volatile.Read(ref _loadInFlight) != 0;

	/// <summary>
	/// Re-runs <see cref="NMainMenu.RefreshButtons"/> so our postfix can hide Continue when the archive
	/// is empty. Vanilla does not always refresh the main menu when the save browser closes.
	/// </summary>
	internal static void RefreshMainMenuAfterArchiveChange()
	{
		NGame.Instance?.MainMenu?.RefreshButtons();
	}

	public static void OpenForMainMenu(NMainMenu mainMenu, bool isMultiplayer)
	{
		Open(mainMenu.SubmenuStack, new SaveBrowserRequest(isMultiplayer, false));
	}

	public static void OpenForPauseMenu(NPauseMenu pauseMenu, bool isMultiplayer)
	{
		NSubmenuStack? stack = pauseMenu.GetParent() as NSubmenuStack ?? Traverse.Create(pauseMenu).Field("_stack").GetValue<NSubmenuStack>();
		if (stack == null)
		{
			return;
		}

		Open(stack, new SaveBrowserRequest(isMultiplayer, true));
	}

	public static async Task<bool> LoadSnapshotAsync(SaveArchiveMetadata metadata, bool launchedFromRun)
	{
		// Re-entrancy guard: prevent double-click (or any concurrent caller) from
		// invoking RunManager.SetUpSavedSinglePlayer twice, which throws
		// InvalidOperationException("State is already set.").
		if (System.Threading.Interlocked.CompareExchange(ref _loadInFlight, 1, 0) != 0)
		{
			Log.Info("[NyMod.Saves] Ignoring duplicate LoadSnapshotAsync request; a load is already in progress.");
			return false;
		}

		try
		{
			if (!ServiceRegistry.ArchiveService.RestoreSnapshot(metadata))
			{
				ShowPopup(SaveUiText.Keys.Popup.LoadFailedTitle, SaveUiText.Keys.Popup.RestoreFailedBody);
				return false;
			}

			if (metadata.IsMultiplayer)
			{
				return await LoadMultiplayerAsync(launchedFromRun);
			}

			return await LoadSingleplayerAsync(launchedFromRun);
		}
		finally
		{
			System.Threading.Volatile.Write(ref _loadInFlight, 0);
		}
	}

	public static void DeleteSnapshot(SaveArchiveMetadata metadata)
	{
		ServiceRegistry.ArchiveService.DeleteSnapshot(metadata);
	}

	public static void DeleteRun(bool isMultiplayer, string runId)
	{
		ServiceRegistry.ArchiveService.DeleteRun(isMultiplayer, runId);
	}

	public static string? BackupSnapshot(SaveArchiveMetadata metadata)
	{
		return ServiceRegistry.ArchiveService.BackupSnapshot(metadata);
	}

	public static string? BackupRun(bool isMultiplayer, string runId)
	{
		return ServiceRegistry.ArchiveService.BackupRun(isMultiplayer, runId);
	}

	public static bool OpenFolderForSnapshot(SaveArchiveMetadata metadata)
	{
		if (!ServiceRegistry.ArchiveService.TryGetSnapshotDirectory(metadata, out string? snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			ShowPopup(SaveUiText.Keys.Popup.OpenFolderFailedTitle, SaveUiText.Keys.Popup.OpenFolderFailedBody, ("Path", metadata.SaveId));
			return false;
		}

		return OpenFolder(snapshotDirectory);
	}

	public static bool OpenFolderForRun(bool isMultiplayer, string runId)
	{
		if (!ServiceRegistry.ArchiveService.TryGetRunDirectory(isMultiplayer, runId, out string? runDirectory) || string.IsNullOrEmpty(runDirectory))
		{
			ShowPopup(SaveUiText.Keys.Popup.OpenFolderFailedTitle, SaveUiText.Keys.Popup.OpenFolderFailedBody, ("Path", runId));
			return false;
		}

		return OpenFolder(runDirectory);
	}

	public static bool UpdateSnapshotNote(SaveArchiveMetadata metadata, string? note)
	{
		return ServiceRegistry.ArchiveService.UpdateSnapshotNote(metadata, note);
	}

	public static bool UpdateRunNote(bool isMultiplayer, string runId, string? note)
	{
		return ServiceRegistry.ArchiveService.UpdateRunNote(isMultiplayer, runId, note);
	}

	public static bool IsCurrentRun(bool isMultiplayer, string runId)
	{
		return ServiceRegistry.ArchiveService.TryGetCurrentRunId(isMultiplayer, out string? currentRunId) && string.Equals(currentRunId, runId, StringComparison.Ordinal);
	}

	private static void Open(NSubmenuStack stack, SaveBrowserRequest request)
	{
		ulong key = stack.GetInstanceId();
		if (!_screens.TryGetValue(key, out SaveBrowserScreen? screen) || !GodotObject.IsInstanceValid(screen))
		{
			screen = SaveBrowserScreen.Create();
			screen.Visible = false;
			stack.AddChild(screen);
			_screens[key] = screen;
		}

		screen.Configure(request);
		stack.Push(screen);
	}

	private static async Task<bool> LoadSingleplayerAsync(bool launchedFromRun)
	{
		if (launchedFromRun)
		{
			await NGame.Instance!.ReturnToMainMenu();
		}

		ReadSaveResult<SerializableRun> readSaveResult = SaveManager.Instance.LoadRunSave();
		if (!readSaveResult.Success || readSaveResult.SaveData == null)
		{
			ShowPopup(SaveUiText.Keys.Popup.LoadFailedTitle, SaveUiText.Keys.Popup.SingleplayerUnreadableBody);
			return false;
		}

		SerializableRun serializableRun = readSaveResult.SaveData;
		RunState runState = RunState.FromSerializable(serializableRun);
		// Cross-branch shim: SetUpSavedSinglePlayer is `void` on public, `async Task` on beta.
		await RunManagerCompat.SetUpSavedSinglePlayer(RunManager.Instance, runState, serializableRun);
		NAudioManager.Instance?.StopMusic();
		SfxCmd.Play(runState.Players[0].Character.CharacterTransitionSfx);
		await NGame.Instance!.Transition.FadeOut(0.8f, runState.Players[0].Character.CharacterSelectTransitionPath);
		NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
		await NGame.Instance.LoadRun(runState, serializableRun.PreFinishedRoom);
		await NGame.Instance.Transition.FadeIn();
		return true;
	}

	private static async Task<bool> LoadMultiplayerAsync(bool launchedFromRun)
	{
		if (launchedFromRun)
		{
			await NGame.Instance!.ReturnToMainMenu();
		}

		PlatformType platformType = (SteamInitializer.Initialized && !CommandLineHelper.HasArg("fastmp")) ? PlatformType.Steam : PlatformType.None;
		ReadSaveResult<SerializableRun> readSaveResult = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(PlatformUtil.GetLocalPlayerId(platformType));
		if (!readSaveResult.Success || readSaveResult.SaveData == null)
		{
			ShowPopup(SaveUiText.Keys.Popup.LoadFailedTitle, SaveUiText.Keys.Popup.MultiplayerUnreadableBody);
			return false;
		}

		NMainMenu? mainMenu = NGame.Instance!.MainMenu;
		if (mainMenu == null)
		{
			ShowPopup(SaveUiText.Keys.Popup.LoadFailedTitle, SaveUiText.Keys.Popup.MultiplayerMainMenuMissingBody);
			return false;
		}

		NMultiplayerSubmenu submenu = mainMenu.OpenMultiplayerSubmenu();
		submenu.StartHost(readSaveResult.SaveData);
		return true;
	}

	private static bool OpenFolder(string path)
	{
		if (!Directory.Exists(path))
		{
			ShowPopup(SaveUiText.Keys.Popup.OpenFolderFailedTitle, SaveUiText.Keys.Popup.OpenFolderFailedBody, ("Path", path));
			return false;
		}

		Error result = OS.ShellOpen(new Uri(path).AbsoluteUri);
		if (result != Error.Ok)
		{
			ShowPopup(SaveUiText.Keys.Popup.OpenFolderFailedTitle, SaveUiText.Keys.Popup.OpenFolderFailedBody, ("Path", path));
			return false;
		}

		return true;
	}

	private static void ShowPopup(string titleKey, string bodyKey, params (string Name, object? Value)[] variables)
	{
		NErrorPopup? popup = NErrorPopup.Create(
			SaveUiText.Get(titleKey),
			variables.Length == 0 ? SaveUiText.Get(bodyKey) : SaveUiText.Format(bodyKey, variables),
			showReportBugButton: false);
		if (popup != null && NModalContainer.Instance != null)
		{
			NModalContainer.Instance.Add(popup);
		}
	}
}

internal readonly record struct SaveBrowserRequest(bool IsMultiplayer, bool LaunchedFromRun);