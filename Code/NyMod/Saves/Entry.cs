using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Infrastructure.Localization;

namespace NyMod.Saves;

[ModInitializer("Init")]
public class Entry
{
	public static void Init()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		ServiceRegistry.Initialize();
		Harmony val = new Harmony("nymod.saves");
		val.PatchAll();
		SaveUiLocalizationInstaller.Install();
		Log.Info("NyMod.Saves initialized", 2);
	}
}
