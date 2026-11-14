using System;
using System.IO;
using System.Threading;

namespace NyMod.Saves.Infrastructure.Diagnostics;

internal static class ModDiagnostics
{
    // Create STS2Saves.debug beside the DLL before launch to enable file tracing.
    private static readonly bool Enabled = File.Exists(Path.ChangeExtension(typeof(ModDiagnostics).Assembly.Location, ".debug"));

    private static readonly object Gate = new object();

    // Use managed file IO: Godot's logger may be unavailable during a native
    // interop failure. The file lives beside the installed mod DLL.
    internal static void Trace(string message)
    {
        if (!Enabled)
            return;
        try
        {
            var directory = Path.GetDirectoryName(typeof(ModDiagnostics).Assembly.Location);
            if (string.IsNullOrEmpty(directory))
                return;
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(directory, "STS2Saves.debug.log"),
                    $"{DateTime.UtcNow:O} [thread {Thread.CurrentThread.ManagedThreadId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must not prevent saving or starting a run.
        }
    }
}
