using System;
using System.Linq;
using System.Text;
using Godot;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Infrastructure.Diagnostics;

namespace NyMod.Saves.Features.SaveArchive.Logic;

internal sealed class RunIdentityService
{
	public string ResolveRunId(SaveArchiveSummary summary, byte[] payloadBytes, bool isMultiplayer)
	{
		string text = (isMultiplayer ? "mp" : "sp");
		if (summary.SummaryAvailable)
		{
			string text2 = string.Join(",", summary.CharacterIds.OrderBy<string, string>((string id) => id, StringComparer.Ordinal));
			string text3 = summary.DailyTime?.ToUnixTimeSeconds().ToString() ?? "none";
			string s = string.Join("|", text, summary.StartTime, summary.PlatformType ?? string.Empty, summary.GameMode ?? string.Empty, text3, text2, summary.PlayerCount);
			return "run_" + ComputeShortHash(Encoding.UTF8.GetBytes(s));
		}
		return "run_" + ComputeShortHash(payloadBytes);
	}

	private static string ComputeShortHash(byte[] bytes)
	{
		// Use Godot's bundled SHA-256 on every platform. The Android Mono host
		// cannot safely use the desktop .NET native cryptography provider.
		// Keep SHA-256 and the same eight-byte prefix to preserve archive IDs.
		ModDiagnostics.Trace("Hashing run identity with Godot SHA-256");
		using var context = new HashingContext();
		if (context.Start(HashingContext.HashType.Sha256) != Error.Ok
			|| (bytes.Length > 0 && context.Update(bytes) != Error.Ok))
		{
			throw new InvalidOperationException("Could not compute the save archive identity.");
		}
		byte[] array = context.Finish();
		if (array.Length != 32)
		{
			throw new InvalidOperationException("The save archive identity hash was incomplete.");
		}
		ModDiagnostics.Trace("Run identity hash complete");
		return Convert.ToHexString(array[..8]).ToLowerInvariant();
	}
}
