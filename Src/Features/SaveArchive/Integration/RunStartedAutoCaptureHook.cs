using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using NyMod.Saves.Bootstrap;

namespace NyMod.Saves.Features.SaveArchive.Integration;

/// <summary>
/// When a run starts (or resumes from disk), opportunistically capture it into the
/// NyMod.Saves archive if no snapshot for that run exists yet. Covers the
/// "mod installed mid-run" scenario without waiting for the next autosave.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class RunStartedAutoCaptureHook
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			ServiceRegistry.ArchiveService.TryAutoCaptureCurrentRunIfMissing();
		}
		catch (System.Exception ex)
		{
			Log.Warn($"NyMod.Saves RunStartedAutoCaptureHook failed: {ex.Message}");
		}
	}
}
