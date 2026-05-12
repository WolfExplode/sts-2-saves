using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Infrastructure.Localization;

namespace NyMod.Saves;

/// <summary>
/// Real initializer for STS2Saves. Built once per supported game version into
/// <c>STS2Saves.&lt;version&gt;.dll</c>; invoked by the entry assembly's
/// VersionedModLoader after it picks the right impl. The <c>[ModInitializer]</c>
/// attribute is preserved so the loader can locate this method by reflection
/// (the attribute itself is the discovery anchor, not the game's mod manager).
/// </summary>
[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Init))]
public class Entry
{
    public static void Init()
    {
        ServiceRegistry.Initialize();
        var harmony = new Harmony("nymod.saves");
        harmony.PatchAll();
        SaveUiLocalizationInstaller.Install();
        Log.Info("NyMod.Saves initialized");
    }
}
