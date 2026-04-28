using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Pre-rolls relic IDs at SaveEdit time using the new player's "Rewards" RNG seed/counter so
/// the auto-rolled relic rewards have a predetermined relic id baked in. Required because the
/// game's runtime relic roll path (<c>RelicFactory.PullNextRelicFromFront</c>) needs a live
/// <c>IRunState</c> we don't have at SaveEdit time, and because relic-only rewards with
/// <c>PredeterminedModelId == none</c> appear empty when surfaced to a player who joined
/// after the host loaded the save.
/// </summary>
internal static class RelicPreroller
{
	/// <summary>
	/// Roll <paramref name="regularCount"/> regular relic ids and <paramref name="ancientCount"/>
	/// ancient relic ids for <paramref name="player"/>, mutating the player's RngSet counters
	/// AND removing picked regular relics from both the player's grab bag and the shared bag.
	/// Each ancient relic at index <c>i</c> is drawn from
	/// <c>ancientPoolsPerVisit[i % ancientPoolsPerVisit.Count]</c> so multi-relic-per-visit
	/// configurations cycle through the visited ancients in order. Falls back to Circlet when
	/// no candidate pools are available.
	/// </summary>
	public static (List<ModelId> Regular, List<ModelId> Ancient) RollAll(
		SerializableRun run,
		SerializablePlayer player,
		int regularCount,
		int ancientCount,
		IReadOnlyList<IReadOnlyList<ModelId>> ancientPoolsPerVisit)
	{
		List<ModelId> regular = new List<ModelId>(regularCount > 0 ? regularCount : 0);
		List<ModelId> ancient = new List<ModelId>(ancientCount > 0 ? ancientCount : 0);
		if (regularCount <= 0 && ancientCount <= 0) return (regular, ancient);

		if (player.Rng == null) player.Rng = new SerializablePlayerRngSet { Seed = 0 };
		if (player.Rng.Counters == null) player.Rng.Counters = new System.Collections.Generic.Dictionary<PlayerRngType, int>();
		if (!player.Rng.Counters.TryGetValue(PlayerRngType.Rewards, out int counter)) counter = 0;

		string rngName = StringHelper.SnakeCase(PlayerRngType.Rewards.ToString());
		Rng rng = new Rng(player.Rng.Seed + (uint)StringHelper.GetDeterministicHashCode(rngName), counter);

		Dictionary<RelicRarity, List<ModelId>> bag = player.RelicGrabBag?.RelicIdLists ?? new Dictionary<RelicRarity, List<ModelId>>();
		Dictionary<RelicRarity, List<ModelId>> sharedBag = run.SerializableSharedRelicGrabBag?.RelicIdLists
			?? new Dictionary<RelicRarity, List<ModelId>>();

		for (int i = 0; i < regularCount; i++)
		{
			RelicRarity rarity = RollRarity(rng);
			regular.Add(PullFromBag(bag, sharedBag, rarity));
		}

		int poolCount = ancientPoolsPerVisit?.Count ?? 0;
		for (int i = 0; i < ancientCount; i++)
		{
			IReadOnlyList<ModelId>? pool = poolCount > 0 ? ancientPoolsPerVisit![i % poolCount] : null;
			if (pool != null && pool.Count > 0)
			{
				int idx = rng.NextInt(pool.Count);
				ancient.Add(pool[idx]);
			}
			else
			{
				ancient.Add(ModelDb.Relic<Circlet>().Id!);
			}
		}

		player.Rng.Counters[PlayerRngType.Rewards] = rng.Counter;
		return (regular, ancient);
	}

	/// <summary>
	/// Backwards-compatible shim: rolls only regular relics (no ancient pre-roll).
	/// </summary>
	public static List<ModelId> Roll(SerializableRun run, SerializablePlayer player, int count)
	{
		(List<ModelId> regular, _) = RollAll(run, player, count, 0, System.Array.Empty<IReadOnlyList<ModelId>>());
		return regular;
	}

	private static RelicRarity RollRarity(Rng rng)
	{
		float n = rng.NextFloat(1f);
		if (n >= 0.5f)
		{
			return n >= 0.83f ? RelicRarity.Rare : RelicRarity.Uncommon;
		}
		return RelicRarity.Common;
	}

	private static ModelId PullFromBag(
		Dictionary<RelicRarity, List<ModelId>> bag,
		Dictionary<RelicRarity, List<ModelId>> sharedBag,
		RelicRarity rarity)
	{
		if (bag.TryGetValue(rarity, out List<ModelId>? list) && list != null && list.Count > 0)
		{
			ModelId pick = list[0];
			list.RemoveAt(0);
			if (sharedBag.TryGetValue(rarity, out List<ModelId>? shared) && shared != null)
			{
				shared.Remove(pick);
			}
			return pick;
		}

		// Fallback: Circlet (matches RelicFactory.FallbackRelic).
		return ModelDb.Relic<Circlet>().Id!;
	}
}
