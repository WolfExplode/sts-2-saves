using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Bootstrap;

namespace NyMod.Saves.Features.SaveArchive.Integration;

[HarmonyPatch(typeof(NGame))]
internal static class RunStartArchiveHooks
{
	[HarmonyPatch(nameof(NGame.StartNewSingleplayerRun))]
	[HarmonyPrefix]
	private static void StartNewSingleplayerRunPrefix()
	{
		try
		{
			ServiceRegistry.ArchiveService.PreserveCurrentRunBeforeReplacement(isMultiplayer: false, note: "pre_new_singleplayer_run");
		}
		catch
		{
		}
	}

	[HarmonyPatch(nameof(NGame.StartNewMultiplayerRun))]
	[HarmonyPrefix]
	private static void StartNewMultiplayerRunPrefix()
	{
		try
		{
			ServiceRegistry.ArchiveService.PreserveCurrentRunBeforeReplacement(isMultiplayer: true, note: "pre_new_multiplayer_run");
		}
		catch
		{
		}
	}
}