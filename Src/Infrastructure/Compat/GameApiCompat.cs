using System;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace NyMod.Saves.Infrastructure.Compat;

/// <summary>
/// One-stop home for reflection-based shims that paper over signature drift
/// in the game's API between the public and beta branches of <c>sts2.dll</c>.
/// Add new compat classes here so this file is the single place to audit when
/// the game updates.
/// </summary>
internal static class GameApiCompat
{
	// no-op holder; sub-classes below carry the real surface area.
}

/// <summary>
/// <see cref="RunManager.SetUpSavedSinglePlayer"/> changed return type between branches:
///   public branch:  <c>void SetUpSavedSinglePlayer(RunState, SerializableRun)</c>
///   beta branch:    <c>async Task SetUpSavedSinglePlayer(RunState, SerializableRun)</c>
/// IL bound to the public-branch <c>void</c> signature throws <see cref="MissingMethodException"/>
/// on beta. We discover whichever overload exists at startup and await it correctly.
/// </summary>
internal static class RunManagerCompat
{
	private static MethodInfo? _setUpSavedSinglePlayer;
	private static bool _returnsTask;
	private static bool _initialized;
	private static readonly object _gate = new object();

	private static void EnsureInit()
	{
		if (_initialized) return;
		lock (_gate)
		{
			if (_initialized) return;
			Type t = typeof(RunManager);
			_setUpSavedSinglePlayer = t.GetMethod(
				nameof(RunManager.SetUpSavedSinglePlayer),
				BindingFlags.Public | BindingFlags.Instance,
				binder: null,
				types: new[] { typeof(RunState), typeof(SerializableRun) },
				modifiers: null);
			_returnsTask = _setUpSavedSinglePlayer != null
				&& typeof(Task).IsAssignableFrom(_setUpSavedSinglePlayer.ReturnType);
			_initialized = true;
		}
	}

	public static async Task SetUpSavedSinglePlayer(RunManager instance, RunState state, SerializableRun save)
	{
		EnsureInit();
		if (_setUpSavedSinglePlayer == null)
		{
			throw new MissingMethodException(nameof(RunManager), nameof(RunManager.SetUpSavedSinglePlayer));
		}
		object? result = _setUpSavedSinglePlayer.Invoke(instance, new object[] { state, save });
		if (_returnsTask && result is Task task)
		{
			await task.ConfigureAwait(true);
		}
	}
}
