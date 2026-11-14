using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace NyMod.Saves.Infrastructure.Compat;

internal static class RunManagerCompat
{
	// Desktop and Android builds expose synchronous and asynchronous versions.
	private static readonly MethodInfo SetUpSavedRun = typeof(RunManager).GetMethod(
		"SetUpSavedSingleplayer", new[] { typeof(RunState), typeof(SerializableRun) })
		?? throw new MissingMethodException("RunManager", "SetUpSavedSingleplayer");

	public static async Task SetUpSavedSinglePlayer(RunManager instance, RunState state, SerializableRun save)
	{
		object? result;
		try
		{
			result = SetUpSavedRun.Invoke(instance, new object[] { state, save });
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
		if (result is Task task)
		{
			await task;
		}
	}
}
