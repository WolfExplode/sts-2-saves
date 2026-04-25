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
		NMainMenu? mainMenu = NGame.Instance?.MainMenu;
		if (mainMenu != null)
		{
			SaveBrowserCoordinator.OpenForMainMenu(mainMenu, isMultiplayer: true);
		}
		return false;
	}

	[HarmonyPatch("UpdateButtons")]
	[HarmonyPostfix]
	private static void UpdateButtonsPostfix(NMultiplayerSubmenu __instance)
	{
		bool hasArchivedRuns = ServiceRegistry.ArchiveService.ListRuns(isMultiplayer: true).Count > 0;
		Traverse traverse = Traverse.Create(__instance);
		NSubmenuButton hostButton = traverse.Field("_hostButton").GetValue<NSubmenuButton>();
		NSubmenuButton loadButton = traverse.Field("_loadButton").GetValue<NSubmenuButton>();
		NSubmenuButton abandonButton = traverse.Field("_abandonButton").GetValue<NSubmenuButton>();
		hostButton.Visible = true;
		loadButton.Visible = hasArchivedRuns;
		abandonButton.Visible = false;
	}
}