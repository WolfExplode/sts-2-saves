using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using NyMod.Saves.Bootstrap;

namespace NyMod.Saves.Features.SaveArchive.Integration;

[HarmonyPatch(typeof(NGame))]
internal static class RunStartArchiveHooks
{
	[HarmonyPatch("StartNewSingleplayerRun")]
	[HarmonyPrefix]
	private static void StartNewSingleplayerRunPrefix()
	{
		try
		{
			ServiceRegistry.ArchiveService.PreserveCurrentRunBeforeReplacement(isMultiplayer: false, "pre_new_singleplayer_run");
		}
		catch
		{
		}
	}

	[HarmonyPatch("StartNewMultiplayerRun")]
	[HarmonyPrefix]
	private static void StartNewMultiplayerRunPrefix()
	{
		try
		{
			ServiceRegistry.ArchiveService.PreserveCurrentRunBeforeReplacement(isMultiplayer: true, "pre_new_multiplayer_run");
		}
		catch
		{
		}
	}
}
