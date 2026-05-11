using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace NyMod.Saves.Features.SaveEdit.Logic;

internal static class PlayerFactory
{
	/// <summary>
	/// Build a fresh <see cref="SerializablePlayer"/> from scratch using the chosen character's
	/// starting deck, relics, HP, gold etc. Uses the game's own <see cref="Player.CreateForNewRun"/>
	/// path, mirrors run-start ascension effects, and serializes the result so we don't have
	/// to construct sub-objects manually.
	/// Also pre-populates the per-player <c>RelicGrabBag</c> so downstream auto-reward logic
	/// (see <see cref="RelicPreroller"/>) can pull predetermined relic ids without hitting the
	/// fallback every time.
	/// </summary>
	public static SerializablePlayer BuildFromCharacter(ModelId characterId, ulong netId, int ascensionLevel)
	{
		if (characterId == null || characterId == ModelId.none)
		{
			throw new ArgumentException("characterId must be a valid character", nameof(characterId));
		}

		CharacterModel character = ModelDb.GetById<CharacterModel>(characterId);
		Player player = Player.CreateForNewRun(character, UnlockState.all, netId);

		// Game normally populates this lazily once the run starts; do it now so the bag is
		// already shuffled and present in the serialized payload for the auto-reward pre-roller.
		Rng grabBagRng = new Rng((uint)((long)StringHelper.GetDeterministicHashCode("relic_grab_bag") + (long)netId), 0);
		player.PopulateRelicGrabBagIfNecessary(grabBagRng);

		SerializablePlayer serializablePlayer = player.ToSerializable();
		ApplyRunStartAscensionEffects(serializablePlayer, ascensionLevel);
		return serializablePlayer;
	}

	private static void ApplyRunStartAscensionEffects(SerializablePlayer player, int ascensionLevel)
	{
		AscensionManager ascensionManager = new AscensionManager(ascensionLevel);

		if (ascensionManager.HasLevel(AscensionLevel.TightBelt))
		{
			player.MaxPotionSlotCount = Math.Max(0, player.MaxPotionSlotCount - 1);
			player.Potions.RemoveAll(p => p.SlotIndex >= player.MaxPotionSlotCount);
		}

		if (ascensionManager.HasLevel(AscensionLevel.AscendersBane))
		{
			ModelId ascendersBaneId = ModelDb.Card<AscendersBane>().Id;
			if (!player.Deck.Any(c => c.Id == ascendersBaneId))
			{
				CardModel ascendersBane = ModelDb.Card<AscendersBane>().ToMutable();
				ascendersBane.FloorAddedToDeck = 1;
				player.Deck.Add(ascendersBane.ToSerializable());
			}
		}
	}

	/// <summary>
	/// Clone an existing serialized player verbatim, replacing only the NetId. The caller is
	/// responsible for regenerating <see cref="SerializablePlayer.Rng"/> if independent rolls
	/// are desired (see <see cref="PlayerRngRegenerator"/>).
	/// </summary>
	public static SerializablePlayer ClonePlayer(SerializablePlayer source, ulong newNetId)
	{
		if (source == null)
		{
			throw new ArgumentNullException(nameof(source));
		}

		SerializablePlayer clone = new SerializablePlayer
		{
			CharacterId = source.CharacterId,
			CurrentHp = source.CurrentHp,
			MaxHp = source.MaxHp,
			MaxEnergy = source.MaxEnergy,
			MaxPotionSlotCount = source.MaxPotionSlotCount,
			Gold = source.Gold,
			BaseOrbSlotCount = source.BaseOrbSlotCount,
			NetId = newNetId,
			Deck = new List<SerializableCard>(source.Deck),
			Relics = new List<SerializableRelic>(source.Relics),
			Potions = new List<SerializablePotion>(source.Potions),
			Rng = PlayerRngRegenerator.Regenerate(source.Rng, newNetId),
			Odds = source.Odds,
			RelicGrabBag = source.RelicGrabBag,
			ExtraFields = source.ExtraFields,
			UnlockState = source.UnlockState,
			DiscoveredCards = new List<ModelId>(source.DiscoveredCards),
			DiscoveredEnemies = new List<ModelId>(source.DiscoveredEnemies),
			DiscoveredEpochs = new List<string>(source.DiscoveredEpochs),
			DiscoveredPotions = new List<ModelId>(source.DiscoveredPotions),
			DiscoveredRelics = new List<ModelId>(source.DiscoveredRelics)
		};

		return clone;
	}
}
