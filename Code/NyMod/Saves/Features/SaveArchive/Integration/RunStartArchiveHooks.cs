using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Infrastructure.Diagnostics;

namespace NyMod.Saves.Features.SaveArchive.Integration;

[HarmonyPatch(typeof(NGame))]
internal static class RunStartArchiveHooks
{
	[HarmonyPatch("StartNewSingleplayerRun")]
	[HarmonyPrefix]
	private static void StartNewSingleplayerRunPrefix()
	{
		ModDiagnostics.Trace("Singleplayer run-start prefix entered");
		try
		{
			ServiceRegistry.ArchiveService.PreserveCurrentRunBeforeReplacement(isMultiplayer: false, "pre_new_singleplayer_run");
			ModDiagnostics.Trace("Singleplayer pre-run archive complete");
		}
		catch (System.Exception ex)
		{
			ModDiagnostics.Trace("Singleplayer pre-run archive failed: " + ex);
		}
	}

	[HarmonyPatch("StartNewMultiplayerRun")]
	[HarmonyPrefix]
	private static void StartNewMultiplayerRunPrefix()
	{
		ModDiagnostics.Trace("Multiplayer run-start prefix entered");
		try
		{
			ServiceRegistry.ArchiveService.PreserveCurrentRunBeforeReplacement(isMultiplayer: true, "pre_new_multiplayer_run");
			ModDiagnostics.Trace("Multiplayer pre-run archive complete");
		}
		catch (System.Exception ex)
		{
			ModDiagnostics.Trace("Multiplayer pre-run archive failed: " + ex);
		}
	}
}
