using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveBrowser.Logic;

namespace NyMod.Saves.Features.SaveBrowser.Integration;

[HarmonyPatch(typeof(NMainMenu))]
internal static class MainMenuHooks
{
	[HarmonyPatch("OnContinueButtonPressed")]
	[HarmonyPrefix]
	private static bool OnContinueButtonPressedPrefix(NMainMenu __instance)
	{
		SaveBrowserCoordinator.OpenForMainMenu(__instance, isMultiplayer: false);
		return false;
	}

	[HarmonyPatch("RefreshButtons")]
	[HarmonyPostfix]
	private static void RefreshButtonsPostfix(NMainMenu __instance)
	{
		bool visible = ServiceRegistry.ArchiveService.ListRuns(isMultiplayer: false).Count > 0;
		Traverse val = Traverse.Create((object)__instance);
		NMainMenuTextButton value = val.Field("_continueButton").GetValue<NMainMenuTextButton>();
		NMainMenuTextButton value2 = val.Field("_abandonRunButton").GetValue<NMainMenuTextButton>();
		NMainMenuTextButton value3 = val.Field("_singleplayerButton").GetValue<NMainMenuTextButton>();
		((CanvasItem)value).Visible = visible;
		((NClickableControl)value).SetEnabled(((CanvasItem)value).Visible);
		((CanvasItem)value3).Visible = true;
		((CanvasItem)value2).Visible = false;
	}
}
