using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
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

	internal static bool IsLoadInFlight => Volatile.Read(ref _loadInFlight) != 0;

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

	public static void OpenForMainMenu(NMainMenu mainMenu, bool isMultiplayer)
	{
		Open((NSubmenuStack)(object)mainMenu.SubmenuStack, new SaveBrowserRequest(isMultiplayer, LaunchedFromRun: false));
	}

	public static void OpenForPauseMenu(NPauseMenu pauseMenu, bool isMultiplayer)
	{
		Node parent = ((Node)pauseMenu).GetParent();
		NSubmenuStack val = (NSubmenuStack)(((object)((parent is NSubmenuStack) ? parent : null)) ?? ((object)Traverse.Create((object)pauseMenu).Field("_stack").GetValue<NSubmenuStack>()));
		if (val != null)
		{
			Open(val, new SaveBrowserRequest(isMultiplayer, LaunchedFromRun: true));
		}
	}

	public static async Task<bool> LoadSnapshotAsync(SaveArchiveMetadata metadata, bool launchedFromRun)
	{
		if (Interlocked.CompareExchange(ref _loadInFlight, 1, 0) != 0)
		{
			Log.Info("[NyMod.Saves] Ignoring duplicate LoadSnapshotAsync request; a load is already in progress.", 2);
			return false;
		}
		try
		{
			if (!ServiceRegistry.ArchiveService.RestoreSnapshot(metadata))
			{
				ShowPopup("POPUP.loadFailed.title", "POPUP.loadFailed.restoreFailed.body");
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
			Volatile.Write(ref _loadInFlight, 0);
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
		if (!ServiceRegistry.ArchiveService.TryGetSnapshotDirectory(metadata, out string snapshotDirectory) || string.IsNullOrEmpty(snapshotDirectory))
		{
			ShowPopup("POPUP.openFolderFailed.title", "POPUP.openFolderFailed.body", ("Path", metadata.SaveId));
			return false;
		}
		return OpenFolder(snapshotDirectory);
	}

	public static bool OpenFolderForRun(bool isMultiplayer, string runId)
	{
		if (!ServiceRegistry.ArchiveService.TryGetRunDirectory(isMultiplayer, runId, out string runDirectory) || string.IsNullOrEmpty(runDirectory))
		{
			ShowPopup("POPUP.openFolderFailed.title", "POPUP.openFolderFailed.body", ("Path", runId));
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
		string runId2;
		return ServiceRegistry.ArchiveService.TryGetCurrentRunId(isMultiplayer, out runId2) && string.Equals(runId2, runId, StringComparison.Ordinal);
	}

	private static void Open(NSubmenuStack stack, SaveBrowserRequest request)
	{
		ulong instanceId = ((GodotObject)stack).GetInstanceId();
		if (!_screens.TryGetValue(instanceId, out SaveBrowserScreen value) || !GodotObject.IsInstanceValid((GodotObject)(object)value))
		{
			value = SaveBrowserScreen.Create();
			((CanvasItem)value).Visible = false;
			((Node)stack).AddChild((Node)(object)value, false, (Node.InternalMode)0);
			_screens[instanceId] = value;
		}
		value.Configure(request);
		stack.Push((NSubmenu)(object)value);
	}

	private static async Task<bool> LoadSingleplayerAsync(bool launchedFromRun)
	{
		if (launchedFromRun)
		{
			await NGame.Instance.ReturnToMainMenu();
		}
		ReadSaveResult<SerializableRun> readSaveResult = SaveManager.Instance.LoadRunSave();
		if (!readSaveResult.Success || readSaveResult.SaveData == null)
		{
			ShowPopup("POPUP.loadFailed.title", "POPUP.loadFailed.singleplayerUnreadable.body");
			return false;
		}
		SerializableRun serializableRun = readSaveResult.SaveData;
		RunState runState = RunState.FromSerializable(serializableRun);
		await RunManagerCompat.SetUpSavedSinglePlayer(RunManager.Instance, runState, serializableRun);
		NAudioManager instance = NAudioManager.Instance;
		if (instance != null)
		{
			instance.StopMusic();
		}
		SfxCmd.Play(runState.Players[0].Character.CharacterTransitionSfx, 1f);
		await NGame.Instance.Transition.FadeOut(0.8f, runState.Players[0].Character.CharacterSelectTransitionPath, (CancellationToken?)null);
		NGame.Instance.ReactionContainer.InitializeNetworking((INetGameService)new NetSingleplayerGameService());
		await NGame.Instance.LoadRun(runState, serializableRun.PreFinishedRoom);
		await NGame.Instance.Transition.FadeIn(0.8f, "res://materials/transitions/fade_transition_mat.tres", (CancellationToken?)null);
		return true;
	}

	private static async Task<bool> LoadMultiplayerAsync(bool launchedFromRun)
	{
		if (launchedFromRun)
		{
			await NGame.Instance.ReturnToMainMenu();
		}
		PlatformType platformType = (PlatformType)((SteamInitializer.Initialized && !CommandLineHelper.HasArg("fastmp")) ? 1 : 0);
		ReadSaveResult<SerializableRun> readSaveResult = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(PlatformUtil.GetLocalPlayerId(platformType));
		if (!readSaveResult.Success || readSaveResult.SaveData == null)
		{
			ShowPopup("POPUP.loadFailed.title", "POPUP.loadFailed.multiplayerUnreadable.body");
			return false;
		}
		NMainMenu mainMenu = NGame.Instance.MainMenu;
		if (mainMenu == null)
		{
			ShowPopup("POPUP.loadFailed.title", "POPUP.loadFailed.multiplayerMainMenuMissing.body");
			return false;
		}
		NMultiplayerSubmenu submenu = mainMenu.OpenMultiplayerSubmenu();
		submenu.StartHost(readSaveResult.SaveData);
		return true;
	}

	private static bool OpenFolder(string path)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Invalid comparison between Unknown and I8
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
