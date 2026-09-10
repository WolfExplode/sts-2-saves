using System;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace NyMod.Saves.Infrastructure.Compat;

internal static class RunManagerCompat
{
	private static MethodInfo? _setUpSavedSinglePlayer;

	private static bool _returnsTask;

	private static bool _initialized;

	private static readonly object _gate = new object();

	private static void EnsureInit()
	{
		if (_initialized)
		{
			return;
		}
		lock (_gate)
		{
			if (!_initialized)
			{
				Type typeFromHandle = typeof(RunManager);
				_setUpSavedSinglePlayer = typeFromHandle.GetMethod("SetUpSavedSingleplayer", BindingFlags.Instance | BindingFlags.Public, null, new Type[2]
				{
					typeof(RunState),
					typeof(SerializableRun)
				}, null);
				_returnsTask = _setUpSavedSinglePlayer != null && typeof(Task).IsAssignableFrom(_setUpSavedSinglePlayer.ReturnType);
				_initialized = true;
			}
		}
	}

	public static async Task SetUpSavedSinglePlayer(RunManager instance, RunState state, SerializableRun save)
	{
		EnsureInit();
		if (_setUpSavedSinglePlayer == null)
		{
			throw new MissingMethodException("RunManager", "SetUpSavedSinglePlayer");
		}
		object result = _setUpSavedSinglePlayer.Invoke(instance, new object[2] { state, save });
		Task task = default(Task);
		int num;
		if (_returnsTask)
		{
			task = result as Task;
			num = ((task != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		if (num != 0)
		{
			await task.ConfigureAwait(continueOnCapturedContext: true);
		}
	}
}
