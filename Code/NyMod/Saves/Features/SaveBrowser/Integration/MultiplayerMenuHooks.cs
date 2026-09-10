using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveBrowser.Logic;

namespace NyMod.Saves.Features.SaveBrowser.Integration;

[HarmonyPatch(typeof(NMultiplayerSubmenu))]
internal static class MultiplayerMenuHooks
{
	[HarmonyPatch("StartLoad")]
	[HarmonyPrefix]
	private static bool StartLoadPrefix(NMultiplayerSubmenu __instance)
	{
		NGame instance = NGame.Instance;
		NMainMenu val = ((instance != null) ? instance.MainMenu : null);
		if (val != null)
		{
			SaveBrowserCoordinator.OpenForMainMenu(val, isMultiplayer: true);
		}
		return false;
	}

	[HarmonyPatch("UpdateButtons")]
	[HarmonyPostfix]
	private static void UpdateButtonsPostfix(NMultiplayerSubmenu __instance)
	{
		bool visible = ServiceRegistry.ArchiveService.ListRuns(isMultiplayer: true).Count > 0;
		Traverse val = Traverse.Create((object)__instance);
		NSubmenuButton value = val.Field("_hostButton").GetValue<NSubmenuButton>();
		NSubmenuButton value2 = val.Field("_loadButton").GetValue<NSubmenuButton>();
		NSubmenuButton value3 = val.Field("_abandonButton").GetValue<NSubmenuButton>();
		((CanvasItem)value).Visible = true;
		((CanvasItem)value2).Visible = visible;
		((CanvasItem)value3).Visible = false;
	}
}
