using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Features.SaveBrowser.Logic;
using NyMod.Saves.Infrastructure.Localization;
using NyMod.Saves.Infrastructure.Diagnostics;

namespace NyMod.Saves.Features.SaveArchive.Integration;

[HarmonyPatch(typeof(NPauseMenu))]
internal static class PauseMenuHooks
{
	private static readonly Dictionary<ulong, PauseMenuHookState> _states = new Dictionary<ulong, PauseMenuHookState>();

	[HarmonyPatch("_Ready")]
	[HarmonyPostfix]
	private static void ReadyPostfix(NPauseMenu __instance)
	{
		ModDiagnostics.Trace("Pause menu Ready postfix entered");
		NPauseMenu __instance2 = __instance;
		ulong instanceId = ((GodotObject)__instance2).GetInstanceId();
		ModDiagnostics.Trace("Pause menu instance ID resolved");
		if (!_states.ContainsKey(instanceId))
		{
			Control node = ((Node)__instance2).GetNode<Control>((NodePath)("%ButtonContainer"));
			NPauseMenuButton node2 = ((Node)node).GetNode<NPauseMenuButton>((NodePath)("SaveAndQuit"));
			ModDiagnostics.Trace("Pause menu button template resolved");
			NPauseMenuButton val = CreateButton(node2, "NyManualSaveButton", "PAUSE_MENU.save");
			NPauseMenuButton val2 = CreateButton(node2, "NyLoadSaveButton", "PAUSE_MENU.load");
			((Node)node).AddChild((Node)(object)val, false, (Node.InternalMode)0);
			((Node)node).AddChild((Node)(object)val2, false, (Node.InternalMode)0);
			((GodotObject)val).Connect(NButton.SignalName.Released, Callable.From<NButton>((Action<NButton>)delegate
			{
				OnManualSavePressed();
			}), 0u);
			((GodotObject)val2).Connect(NButton.SignalName.Released, Callable.From<NButton>((Action<NButton>)delegate
			{
				OnLoadPressed(__instance2);
			}), 0u);
			_states[instanceId] = new PauseMenuHookState
			{
				SaveButton = val,
				LoadButton = val2
			};
			RefreshButtonState(__instance2);
			RefreshFocusNeighbors(node);
			ModDiagnostics.Trace("Pause menu Ready postfix complete");
		}
	}

	[HarmonyPatch("Initialize")]
	[HarmonyPostfix]
	private static void InitializePostfix(NPauseMenu __instance)
	{
		ModDiagnostics.Trace("Pause menu Initialize postfix entered");
		RefreshButtonState(__instance);
		Control node = ((Node)__instance).GetNode<Control>((NodePath)("%ButtonContainer"));
		RefreshFocusNeighbors(node);
		ModDiagnostics.Trace("Pause menu Initialize postfix complete");
	}

	private static NPauseMenuButton CreateButton(NPauseMenuButton template, string name, string textKey)
	{
		ModDiagnostics.Trace("Duplicating pause menu button: " + name);
		Node obj = ((Node)template).Duplicate(15);
		ModDiagnostics.Trace("Pause menu button duplicate returned: " + name);
		NPauseMenuButton val = (NPauseMenuButton)(object)(((obj is NPauseMenuButton) ? obj : null) ?? throw new InvalidOperationException("Failed to duplicate pause menu button template."));
		((Node)val).Name = (StringName)(name);
		((Node)val).GetNode<MegaLabel>((NodePath)("Label")).SetTextAutoSize(SaveUiText.Get(textKey));
		((NClickableControl)val).Disable();
		return val;
	}

	private static void RefreshButtonState(NPauseMenu pauseMenu)
	{
		if (_states.TryGetValue(((GodotObject)pauseMenu).GetInstanceId(), out PauseMenuHookState value))
		{
			bool isSingleplayer = IsSingleplayerRun();
			value.SaveButton.Visible = isSingleplayer;
			value.LoadButton.Visible = isSingleplayer;
			if (isSingleplayer)
			{
				value.SaveButton.Enable();
				value.LoadButton.Enable();
			}
			else
			{
				value.SaveButton.Disable();
				value.LoadButton.Disable();
			}
		}
	}

	private static void RefreshFocusNeighbors(Control buttonContainer)
	{
		List<NPauseMenuButton> list = (from button in ((IEnumerable)((Node)buttonContainer).GetChildren(false)).OfType<NPauseMenuButton>()
			where ((CanvasItem)button).Visible
			select button).ToList();
		for (int i = 0; i < list.Count; i++)
		{
			((Control)list[i]).FocusNeighborLeft = ((Node)list[i]).GetPath();
			((Control)list[i]).FocusNeighborRight = ((Node)list[i]).GetPath();
			((Control)list[i]).FocusNeighborTop = ((Node)((i > 0) ? list[i - 1] : list[i])).GetPath();
			((Control)list[i]).FocusNeighborBottom = ((Node)((i < list.Count - 1) ? list[i + 1] : list[i])).GetPath();
		}
	}

	private static bool IsSingleplayerRun()
	{
		return RunManager.Instance?.NetService?.Type == NetGameType.Singleplayer;
	}

	private static void OnManualSavePressed()
	{
		if (!IsSingleplayerRun())
		{
			return;
		}

		bool saved = ServiceRegistry.ArchiveService.CaptureCurrentSnapshot(SaveArchiveKind.Manual, "pause_menu_manual_save");
		string title = saved ? "PAUSE_MENU.manualSaveCreated.title" : "PAUSE_MENU.manualSaveFailed.title";
		string body = saved ? "PAUSE_MENU.manualSaveCreated.body" : "PAUSE_MENU.manualSaveFailed.body";
		NErrorPopup? popup = NErrorPopup.Create(SaveUiText.Get(title), SaveUiText.Get(body), false);
		if (popup != null && NModalContainer.Instance != null)
		{
			NModalContainer.Instance.Add(popup, true);
		}
	}

	private static void OnLoadPressed(NPauseMenu pauseMenu)
	{
		if (IsSingleplayerRun())
		{
			SaveBrowserCoordinator.OpenForPauseMenu(pauseMenu);
		}
	}
}
