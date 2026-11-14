using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using MegaCrit.Sts2.Core.Logging;

namespace NyMod.Saves.Infrastructure.Diagnostics;

internal sealed class SnapshotLoadProfile : IDisposable
{
    private static readonly AsyncLocal<SnapshotLoadProfile?> ActiveProfile = new();

    private readonly SnapshotLoadProfile? _previous;
    private readonly string _source;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly List<Measurement> _measurements = new();
    private string _stage = "Check load prerequisites";
    private long _stageStarted;

    internal static SnapshotLoadProfile? Current => ActiveProfile.Value;

    public SnapshotLoadProfile(string source)
    {
        _source = source;
        _stageStarted = _started;
        _previous = Current;
        ActiveProfile.Value = this;
    }

    public void Begin(string stage)
    {
        long now = Stopwatch.GetTimestamp();
        Record(_stage, _stageStarted, now, gameStage: false);
        _stage = stage;
        _stageStarted = now;
    }

    public void RecordGameStage(string name, long started)
    {
        Record(name, started, Stopwatch.GetTimestamp(), gameStage: true);
    }

    private void Record(string name, long started, long ended, bool gameStage)
    {
        var measurement = new Measurement(name, started, ended, gameStage);
        lock (_measurements)
        {
            _measurements.Add(measurement);
        }
    }

    public void Report(bool loaded)
    {
        long ended = Stopwatch.GetTimestamp();
        Record(_stage, _stageStarted, ended, gameStage: false);

        Measurement[] measurements;
        lock (_measurements)
        {
            measurements = _measurements.OrderBy(entry => entry.Started).ToArray();
        }
        string Format(Measurement entry) => string.Create(CultureInfo.InvariantCulture,
            $"{entry.Name}={Stopwatch.GetElapsedTime(entry.Started, entry.Ended).TotalMilliseconds:F1}ms @{Stopwatch.GetElapsedTime(_started, entry.Started).TotalMilliseconds:F1}ms");

        string total = Stopwatch.GetElapsedTime(_started, ended).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
        var stages = measurements.Where(entry => !entry.GameStage).Select(Format);
        var gameStages = measurements.Where(entry => entry.GameStage).Select(Format);
        Log.Info($"[STS2Saves load profile] source={_source}, result={(loaded ? "loaded" : "failed")}, total={total}ms | restore: {string.Join(", ", stages)} | game (nested): {string.Join(", ", gameStages)}", 2);
    }

    public void Dispose()
    {
        ActiveProfile.Value = _previous;
    }

    private readonly record struct Measurement(string Name, long Started, long Ended, bool GameStage);
}
