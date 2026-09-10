using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace NyMod.Saves.Infrastructure.Localization;

[HarmonyPatch(typeof(LocManager))]
internal static class SaveUiLocalizationInstallerHooks
{
	[HarmonyPatch("Initialize")]
	[HarmonyPostfix]
	private static void InitializePostfix()
	{
		SaveUiLocalizationInstaller.Install();
	}
}
