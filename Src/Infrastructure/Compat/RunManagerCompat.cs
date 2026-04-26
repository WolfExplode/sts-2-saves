using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace NyMod.Saves.Infrastructure.Compat;

/// <summary>
/// Per-version compat shim. With the per-version build pipeline each Impl DLL
/// is bound against its own sts2.dll, so this is a thin wrapper around direct
/// API calls. #if blocks switch on capability flags defined in STS2Saves.csproj.
/// </summary>
internal static class RunManagerCompat
{
    // RunManager.SetUpSavedSinglePlayer changed return type:
    //   v0.103.2: void
    //   v0.104.0: async Task
    public static async Task SetUpSavedSinglePlayer(RunManager instance, RunState state, SerializableRun save)
    {
#if STS2_V_GE_010400
        await instance.SetUpSavedSinglePlayer(state, save).ConfigureAwait(true);
#else
        instance.SetUpSavedSinglePlayer(state, save);
        await Task.CompletedTask;
#endif
    }
}
