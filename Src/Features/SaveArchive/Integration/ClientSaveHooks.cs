using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Logic;

namespace NyMod.Saves.Features.SaveArchive.Integration;

[HarmonyPatch(typeof(SaveManager))]
internal static class ClientSaveHooks
{
	[HarmonyPatch(nameof(SaveManager.SaveRun))]
	[HarmonyPostfix]
	private static void SaveRunPostfix()
	{
		try
		{
			if (RunManager.Instance.NetService.Type != NetGameType.Client)
			{
				return;
			}

			ServiceRegistry.ArchiveService.CaptureFromMemory(SaveArchiveKind.Auto, note: "client_autosave");
		}
		catch
		{
		}
	}
}
