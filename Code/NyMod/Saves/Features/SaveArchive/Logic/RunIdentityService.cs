using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NyMod.Saves.Features.SaveArchive.Models;

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
		byte[] array = SHA256.HashData(bytes);
		return Convert.ToHexString(array[..8]).ToLowerInvariant();
	}
}
