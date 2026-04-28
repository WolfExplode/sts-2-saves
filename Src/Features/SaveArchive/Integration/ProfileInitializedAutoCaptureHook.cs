using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Bootstrap;

namespace NyMod.Saves.Features.SaveArchive.Integration;

/// <summary>
/// SaveManager.InitProfileId is the first point at which CurrentProfileId is set,
/// so the active save paths (singleplayer/multiplayer current_run files) become
/// resolvable. SaveManager.ProfileIdChanged is only raised by SwitchProfileId, not
/// by the initial profile load, so we hook InitProfileId directly to trigger the
/// on-disk auto-archive scan once per session as soon as the profile is ready.
/// </summary>
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.InitProfileId))]
internal static class ProfileInitializedAutoCaptureHook
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		Log.Info("NyMod.Saves ProfileInitializedAutoCaptureHook fired");
		try
		{
			ServiceRegistry.ArchiveService.TryAutoCaptureExistingSavesOnDisk();
		}
		catch (System.Exception ex)
		{
			Log.Warn($"NyMod.Saves ProfileInitializedAutoCaptureHook failed: {ex.Message}");
		}
	}
}
