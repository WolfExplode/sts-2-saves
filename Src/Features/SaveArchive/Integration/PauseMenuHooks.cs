using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Features.SaveBrowser.Logic;
using NyMod.Saves.Infrastructure.Localization;

namespace NyMod.Saves.Features.SaveArchive.Integration;

internal sealed class PauseMenuHookState
{
	public required NPauseMenuButton SaveButton { get; init; }

	public required NPauseMenuButton LoadButton { get; init; }
}

[HarmonyPatch(typeof(NPauseMenu))]
internal static class PauseMenuHooks
{
	private static readonly Dictionary<ulong, PauseMenuHookState> _states = new Dictionary<ulong, PauseMenuHookState>();

	[HarmonyPatch("_Ready")]
	[HarmonyPostfix]
	private static void ReadyPostfix(NPauseMenu __instance)
	{
		ulong key = __instance.GetInstanceId();
		if (_states.ContainsKey(key))
		{
			return;
		}

		Control buttonContainer = __instance.GetNode<Control>("%ButtonContainer");
		NPauseMenuButton template = buttonContainer.GetNode<NPauseMenuButton>("SaveAndQuit");
		NPauseMenuButton saveButton = CreateButton(template, "NyManualSaveButton", SaveUiText.Keys.PauseMenu.SaveButton);
		NPauseMenuButton loadButton = CreateButton(template, "NyLoadSaveButton", SaveUiText.Keys.PauseMenu.LoadButton);

		buttonContainer.AddChild(saveButton);
		buttonContainer.AddChild(loadButton);
		saveButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OnManualSavePressed(__instance)));
		loadButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OnLoadPressed(__instance)));

		_states[key] = new PauseMenuHookState
		{
			SaveButton = saveButton,
			LoadButton = loadButton
		};

		RefreshButtonState(__instance);
		RefreshFocusNeighbors(buttonContainer);
	}

	[HarmonyPatch(nameof(NPauseMenu.Initialize))]
	[HarmonyPostfix]
	private static void InitializePostfix(NPauseMenu __instance)
	{
		RefreshButtonState(__instance);
		Control buttonContainer = __instance.GetNode<Control>("%ButtonContainer");
		RefreshFocusNeighbors(buttonContainer);
	}

	private static NPauseMenuButton CreateButton(NPauseMenuButton template, string name, string textKey)
	{
		NPauseMenuButton button = template.Duplicate() as NPauseMenuButton ?? throw new System.InvalidOperationException("Failed to duplicate pause menu button template.");
		button.Name = name;
		button.GetNode<MegaLabel>("Label").SetTextAutoSize(SaveUiText.Get(textKey));
		button.Disable();
		return button;
	}

	private static void RefreshButtonState(NPauseMenu pauseMenu)
	{
		if (!_states.TryGetValue(pauseMenu.GetInstanceId(), out PauseMenuHookState? state))
		{
			return;
		}

		bool isClient = RunManager.Instance.NetService.Type == NetGameType.Client;
		state.SaveButton.Visible = true;
		state.LoadButton.Visible = !isClient;
		state.SaveButton.Enable();
		state.LoadButton.Enable();
	}

	private static void RefreshFocusNeighbors(Control buttonContainer)
	{
		List<NPauseMenuButton> buttons = buttonContainer.GetChildren().OfType<NPauseMenuButton>().Where(static button => button.Visible).ToList();
		for (int i = 0; i < buttons.Count; i++)
		{
			buttons[i].FocusNeighborLeft = buttons[i].GetPath();
			buttons[i].FocusNeighborRight = buttons[i].GetPath();
			buttons[i].FocusNeighborTop = (i > 0 ? buttons[i - 1] : buttons[i]).GetPath();
			buttons[i].FocusNeighborBottom = (i < buttons.Count - 1 ? buttons[i + 1] : buttons[i]).GetPath();
		}
	}

	private static void OnManualSavePressed(NPauseMenu _)
	{
		bool isClient = RunManager.Instance.NetService.Type == NetGameType.Client;
		bool saved;
		if (isClient)
		{
			saved = ServiceRegistry.ArchiveService.CaptureFromMemory(SaveArchiveKind.Manual, note: "pause_menu_client_manual_save");
		}
		else
		{
			bool isMultiplayer = RunManager.Instance.NetService.Type == NetGameType.Host;
			saved = ServiceRegistry.ArchiveService.CaptureManualSnapshot(isMultiplayer, note: "pause_menu_manual_save");
		}
		if (saved)
		{
			NErrorPopup? popup = NErrorPopup.Create(
				SaveUiText.Get(SaveUiText.Keys.PauseMenu.ManualSaveCreatedTitle),
				SaveUiText.Get(SaveUiText.Keys.PauseMenu.ManualSaveCreatedBody),
				showReportBugButton: false);
			if (popup != null && NModalContainer.Instance != null)
			{
				NModalContainer.Instance.Add(popup);
			}
		}
		else
		{
			NErrorPopup? popup = NErrorPopup.Create(
				SaveUiText.Get(SaveUiText.Keys.PauseMenu.ManualSaveFailedTitle),
				SaveUiText.Get(SaveUiText.Keys.PauseMenu.ManualSaveFailedBody),
				showReportBugButton: false);
			if (popup != null && NModalContainer.Instance != null)
			{
				NModalContainer.Instance.Add(popup);
			}
		}
	}

	private static void OnLoadPressed(NPauseMenu pauseMenu)
	{
		bool isMultiplayer = RunManager.Instance.NetService.Type == NetGameType.Host;
		SaveBrowserCoordinator.OpenForPauseMenu(pauseMenu, isMultiplayer);
	}
}