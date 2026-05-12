using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Saves;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Helper for producing a fresh <see cref="SerializablePlayerRngSet"/> when cloning a player.
/// The new seed is deterministic per (sourceSeed, newNetId) so applying the same edit twice
/// yields the same result, but the new player will roll an independent stream from the source.
/// </summary>
internal static class PlayerRngRegenerator
{
	/// <summary>
	/// Build a fresh RNG set with a brand-new seed derived from the source seed and the new
	/// NetId. All per-RNG counters start at zero.
	/// </summary>
	public static SerializablePlayerRngSet Regenerate(SerializablePlayerRngSet? source, ulong newNetId)
	{
		uint sourceSeed = source?.Seed ?? 0u;
		uint newSeed = MixSeed(sourceSeed, newNetId);

		SerializablePlayerRngSet rng = new SerializablePlayerRngSet
		{
			Seed = newSeed,
			Counters = new Dictionary<PlayerRngType, int>(),
		};

		foreach (PlayerRngType t in Enum.GetValues<PlayerRngType>())
		{
			rng.Counters[t] = 0;
		}

		return rng;
	}

	/// <summary>
	/// Mix the source seed and new NetId into a non-zero uint via splitmix64-style avalanche.
	/// </summary>
	private static uint MixSeed(uint sourceSeed, ulong newNetId)
	{
		ulong mixed = newNetId ^ ((ulong)sourceSeed << 32) ^ 0x9E3779B97F4A7C15UL;
		mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
		mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
		mixed ^= mixed >> 31;

		uint result = (uint)(mixed ^ (mixed >> 32));
		if (result == 0u) result = 0x9E3779B1u;
		return result;
	}
}
