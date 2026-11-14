using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace NyMod.Saves.Infrastructure.Diagnostics;

internal static class GameLoadTimingHooks
{
    public static void Install(Harmony harmony)
    {
        Patch(typeof(PreloadManager), "LoadRunAssets", typeof(IEnumerable<CharacterModel>));
        Patch(typeof(PreloadManager), "LoadActAssets", typeof(ActModel));
        Patch(typeof(PreloadManager), "LoadRoomAssets", typeof(string), typeof(IEnumerable<string>));
        Patch(typeof(RunManager), "Launch");
        Patch(typeof(NRun), "Create", typeof(RunState));
        Patch(typeof(NSceneContainer), "SetCurrentScene", typeof(Control));
        Patch(typeof(RunManager), "GenerateMap");
        Patch(typeof(RunManager), "LoadIntoLatestMapCoord", typeof(AbstractRoom));

        void Patch(Type type, string name, params Type[] parameters)
        {
            try
            {
                var method = AccessTools.DeclaredMethod(type, name, parameters);
                if (method == null)
                {
                    Log.Warn($"[STS2Saves load profile] Timing unavailable for {type.Name}.{name}", 2);
                    return;
                }

                string postfix = method.ReturnType == typeof(Task) ? nameof(AfterAsync) : nameof(AfterSync);
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(GameLoadTimingHooks), nameof(Before)),
                    postfix: new HarmonyMethod(typeof(GameLoadTimingHooks), postfix));
            }
            catch (Exception ex)
            {
                Log.Warn($"[STS2Saves load profile] Could not instrument {type.Name}.{name}: {ex.Message}", 2);
            }
        }
    }

    private static void Before(out Timing? __state)
    {
        var profile = SnapshotLoadProfile.Current;
        __state = profile == null ? null : new Timing(profile, Stopwatch.GetTimestamp());
    }

    private static void AfterSync(MethodBase __originalMethod, Timing? __state)
    {
        if (__state is Timing timing)
            timing.Profile.RecordGameStage(StageName(__originalMethod), timing.Started);
    }

    private static void AfterAsync(MethodBase __originalMethod, Timing? __state, ref Task __result)
    {
        if (__state is not Timing timing)
            return;

        string name = StageName(__originalMethod);
        if (__result.IsCompleted)
        {
            timing.Profile.RecordGameStage(name, timing.Started);
            return;
        }
        __result = RecordCompletion(__result, timing, name);
    }

    // Record completion before the caller resumes, including time spent across awaits.
    private static async Task RecordCompletion(Task task, Timing timing, string name)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            timing.Profile.RecordGameStage(name, timing.Started);
        }
    }

    private static string StageName(MethodBase method) => $"{method.DeclaringType?.Name}.{method.Name}";

    private readonly record struct Timing(SnapshotLoadProfile Profile, long Started);
}
