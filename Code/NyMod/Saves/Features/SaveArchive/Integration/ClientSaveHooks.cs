using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Logic;

namespace NyMod.Saves.Features.SaveArchive.Integration;

[HarmonyPatch(typeof(SaveManager))]
internal static class ClientSaveHooks
{
	[HarmonyPatch("SaveRun")]
	[HarmonyPostfix]
	private static void SaveRunPostfix()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		try
		{
			if ((int)RunManager.Instance.NetService.Type == 3)
			{
				ServiceRegistry.ArchiveService.CaptureFromMemory(SaveArchiveKind.Auto, "client_autosave");
			}
		}
		catch
		{
		}
	}
}
