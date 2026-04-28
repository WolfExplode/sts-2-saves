using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Walks <see cref="MegaCrit.Sts2.Core.Saves.SerializableRun.MapPointHistory"/> and produces a
/// list of <see cref="SerializableReward"/> entries for a freshly-added player based on the
/// per-room-type / per-kind multipliers in <see cref="AutoRewardConfig"/>.
///
/// Rewards are left "auto-roll" (PredeterminedModelId = none) so the game rolls actual cards
/// and relics when the player opens the room — the cloned player's regenerated RNG drives that.
/// Per-kind totals are summed across all rooms of a given type then ceil'd to an integer count.
/// </summary>
internal static class AutoRewardGenerator
{
	private const int MaxPerKindPerRoomType = 64;

	public static List<SerializableReward> Build(
		IEnumerable<List<MapPointHistoryEntry>>? mapPointHistory,
		AutoRewardConfig config,
		ModelId characterId)
	{
		return Build(mapPointHistory, config, characterId, prerolledRelicIds: null, prerolledAncientRelicIds: null);
	}

	public static List<SerializableReward> Build(
		IEnumerable<List<MapPointHistoryEntry>>? mapPointHistory,
		AutoRewardConfig config,
		ModelId characterId,
		List<ModelId>? prerolledRelicIds,
		List<ModelId>? prerolledAncientRelicIds)
	{
		List<SerializableReward> result = new List<SerializableReward>();
		if (mapPointHistory == null) return result;

		List<ModelId> cardPoolIds = ResolveCardPoolIds(characterId);

		(Dictionary<RoomType, int> roomCounts, int ancientCount) = CountRoomsByType(mapPointHistory);

		int relicCursor = 0;
		foreach (KeyValuePair<RoomType, int> kv in roomCounts)
		{
			RoomType type = kv.Key;
			int count = kv.Value;
			if (count <= 0) continue;

			(float card, float remove, float relic) = CellsFor(type, config);

			AddRewards(result, type, RewardType.Card, count * card, cardPoolIds, prerolledRelicIds, ref relicCursor);
			AddRewards(result, type, RewardType.RemoveCard, count * remove, cardPoolIds, prerolledRelicIds, ref relicCursor);
			AddRewards(result, type, RewardType.Relic, count * relic, cardPoolIds, prerolledRelicIds, ref relicCursor);
		}

		if (ancientCount > 0)
		{
			int ancientCursor = 0;
			AddRewards(result, RoomType.Event, RewardType.Card,       ancientCount * config.AncientCard,   cardPoolIds, prerolledRelicIds, ref relicCursor);
			AddRewards(result, RoomType.Event, RewardType.RemoveCard, ancientCount * config.AncientRemove, cardPoolIds, prerolledRelicIds, ref relicCursor);
			AddRewards(result, RoomType.Event, RewardType.Relic,      ancientCount * config.AncientRelic,  cardPoolIds, prerolledAncientRelicIds, ref ancientCursor);
		}

		return result;
	}

	/// <summary>
	/// Counts how many Relic rewards <see cref="Build"/> would emit so callers can pre-roll
	/// the relic ids before the second pass.
	/// </summary>
	public static int CountRelicRewards(
		IEnumerable<List<MapPointHistoryEntry>>? mapPointHistory,
		AutoRewardConfig config)
	{
		if (mapPointHistory == null) return 0;
		(Dictionary<RoomType, int> roomCounts, _) = CountRoomsByType(mapPointHistory);
		int total = 0;
		foreach (KeyValuePair<RoomType, int> kv in roomCounts)
		{
			(_, _, float relic) = CellsFor(kv.Key, config);
			total += CeilCount(kv.Value * relic);
		}
		return total;
	}

	/// <summary>
	/// Counts how many ancient-flavored relic rewards <see cref="Build"/> would emit so
	/// callers can pre-roll those ids from the visited ancients' option pools.
	/// </summary>
	public static int CountAncientRelicRewards(
		IEnumerable<List<MapPointHistoryEntry>>? mapPointHistory,
		AutoRewardConfig config)
	{
		if (mapPointHistory == null) return 0;
		(_, int ancientCount) = CountRoomsByType(mapPointHistory);
		return CeilCount(ancientCount * config.AncientRelic);
	}

	private static int CeilCount(float raw)
	{
		if (raw <= 0f) return 0;
		int count = (int)Math.Ceiling(raw);
		return count > MaxPerKindPerRoomType ? MaxPerKindPerRoomType : count;
	}

	private static List<ModelId> ResolveCardPoolIds(ModelId characterId)
	{
		List<ModelId> ids = new List<ModelId>();
		if (characterId == ModelId.none) return ids;
		try
		{
			CharacterModel character = ModelDb.GetById<CharacterModel>(characterId);
			if (character?.CardPool != null)
			{
				ids.Add(character.CardPool.Id);
			}
		}
		catch (Exception ex)
		{
			MegaCrit.Sts2.Core.Logging.Log.Warn($"NyMod.Saves AutoRewardGenerator: failed to resolve card pool for character {characterId}: {ex.Message}");
		}
		return ids;
	}

	private static (Dictionary<RoomType, int> RoomCounts, int AncientCount) CountRoomsByType(IEnumerable<List<MapPointHistoryEntry>> mapPointHistory)
	{
		Dictionary<RoomType, int> counts = new Dictionary<RoomType, int>();
		int ancientCount = 0;
		foreach (List<MapPointHistoryEntry> floor in mapPointHistory)
		{
			if (floor == null) continue;
			foreach (MapPointHistoryEntry entry in floor)
			{
				if (entry?.Rooms == null) continue;
				bool isAncient = entry.MapPointType == MapPointType.Ancient;
				foreach (MapPointRoomHistoryEntry room in entry.Rooms)
				{
					if (isAncient)
					{
						ancientCount++;
						continue;
					}
					RoomType t = room.RoomType;
					counts.TryGetValue(t, out int cur);
					counts[t] = cur + 1;
				}
			}
		}
		return (counts, ancientCount);
	}

	private static (float card, float remove, float relic) CellsFor(RoomType type, AutoRewardConfig c)
	{
		return type switch
		{
			RoomType.Monster  => (c.NormalCard,   c.NormalRemove,   c.NormalRelic),
			RoomType.Elite    => (c.EliteCard,    c.EliteRemove,    c.EliteRelic),
			RoomType.Boss     => (c.BossCard,     c.BossRemove,     c.BossRelic),
			RoomType.Event    => (c.EventCard,    c.EventRemove,    c.EventRelic),
			RoomType.Treasure => (c.TreasureCard, c.TreasureRemove, c.TreasureRelic),
			RoomType.Shop     => (c.ShopCard,     c.ShopRemove,     c.ShopRelic),
			_ => (0f, 0f, 0f),
		};
	}

	private static void AddRewards(List<SerializableReward> dest, RoomType room, RewardType kind, float total, List<ModelId> cardPoolIds, List<ModelId>? prerolledRelicIds, ref int relicCursor)
	{
		if (total <= 0f) return;
		int count = (int)Math.Ceiling(total);
		if (count > MaxPerKindPerRoomType) count = MaxPerKindPerRoomType;

		CardRarityOddsType odds = OddsFor(room);
		for (int i = 0; i < count; i++)
		{
			ModelId? predetermined = null;
			if (kind == RewardType.Relic && prerolledRelicIds != null && relicCursor < prerolledRelicIds.Count)
			{
				predetermined = prerolledRelicIds[relicCursor++];
			}

			dest.Add(RewardFactory.Build(new RewardEdit
			{
				Action = RewardEditAction.Add,
				RewardType = kind,
				OptionCount = 3,
				RarityOdds = odds,
				PredeterminedModelId = predetermined ?? ModelId.none,
				CardPoolIds = kind == RewardType.Card ? cardPoolIds : null,
			}));
		}
	}

	private static CardRarityOddsType OddsFor(RoomType room)
	{
		return room switch
		{
			RoomType.Elite => CardRarityOddsType.EliteEncounter,
			RoomType.Boss  => CardRarityOddsType.BossEncounter,
			_ => CardRarityOddsType.RegularEncounter,
		};
	}
}
