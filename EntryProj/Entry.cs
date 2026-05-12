using MegaCrit.Sts2.Core.Modding;
using NyMod.VersionedLoader;

namespace NyMod.Saves.EntryAsm;

/// <summary>
/// The entry-DLL initializer for STS2Saves. Forwards to the version-specific
/// implementation DLL chosen by <see cref="VersionedModLoader"/>.
/// </summary>
[ModInitializer(nameof(Init))]
public static class Entry
{
    public static void Init() => VersionedModLoader.LoadAndInit("STS2Saves");
}
