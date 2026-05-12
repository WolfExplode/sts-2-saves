using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Builds <see cref="SerializableReward"/> instances for the supported reward types.
/// Defaults are tuned for "auto-roll on resume" — leave PredeterminedModelId as none and
/// the game will pick options for the player when they open the room.
/// </summary>
internal static class RewardFactory
{
	public static SerializableReward Build(RewardEdit edit)
	{
		ModelId predetermined = edit.PredeterminedModelId ?? ModelId.none;

		switch (edit.RewardType)
		{
			case RewardType.Card:
				return new SerializableReward
				{
					RewardType = RewardType.Card,
					OptionCount = edit.OptionCount > 0 ? edit.OptionCount : 3,
					RarityOdds = edit.RarityOdds == CardRarityOddsType.None ? CardRarityOddsType.RegularEncounter : edit.RarityOdds,
					Source = CardCreationSource.Encounter,
					PredeterminedModelId = predetermined,
					CardPoolIds = edit.CardPoolIds != null ? new List<ModelId>(edit.CardPoolIds) : new List<ModelId>(),
					CustomDescriptionEncounterSourceId = ModelId.none,
				};

			case RewardType.Gold:
				return new SerializableReward
				{
					RewardType = RewardType.Gold,
					GoldAmount = edit.GoldAmount > 0 ? edit.GoldAmount : 50,
					Source = CardCreationSource.None,
					PredeterminedModelId = ModelId.none,
					CardPoolIds = new List<ModelId>(),
					CustomDescriptionEncounterSourceId = ModelId.none,
				};

			case RewardType.Relic:
				return new SerializableReward
				{
					RewardType = RewardType.Relic,
					PredeterminedModelId = predetermined,
					Source = CardCreationSource.None,
					CardPoolIds = new List<ModelId>(),
					CustomDescriptionEncounterSourceId = ModelId.none,
				};

			case RewardType.Potion:
				return new SerializableReward
				{
					RewardType = RewardType.Potion,
					PredeterminedModelId = predetermined,
					Source = CardCreationSource.None,
					CardPoolIds = new List<ModelId>(),
					CustomDescriptionEncounterSourceId = ModelId.none,
				};

			case RewardType.RemoveCard:
				return new SerializableReward
				{
					RewardType = RewardType.RemoveCard,
					Source = CardCreationSource.None,
					PredeterminedModelId = ModelId.none,
					CardPoolIds = new List<ModelId>(),
					CustomDescriptionEncounterSourceId = ModelId.none,
				};

			default:
				return new SerializableReward
				{
					RewardType = RewardType.None,
					PredeterminedModelId = ModelId.none,
					Source = CardCreationSource.None,
					CardPoolIds = new List<ModelId>(),
					CustomDescriptionEncounterSourceId = ModelId.none,
				};
		}
	}
}
