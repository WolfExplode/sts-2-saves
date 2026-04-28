using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Describes the edits the user wants applied to a single archived snapshot.
/// All fields are optional; absent / null / empty means "no change".
/// </summary>
internal sealed class SaveEditPayload
{
	public int? NewAscension { get; set; }

	public IList<ulong> RemovedPlayerNetIds { get; } = new List<ulong>();

	public IList<AddPlayerSpec> AddedPlayers { get; } = new List<AddPlayerSpec>();

	/// <summary>
	/// Reward edits to apply against <c>SerializableRun.PreFinishedRoom.ExtraRewards</c>.
	/// Only meaningful when the snapshot has a non-null PreFinishedRoom.
	/// </summary>
	public IList<RewardEdit> RewardEdits { get; } = new List<RewardEdit>();
}

internal enum AddPlayerMode
{
	/// <summary>Clone an existing player's serialized record verbatim, swapping NetId and
	/// regenerating per-player RNG seeds so the new player rolls independently from the source.
	/// Deck, relics, potions, gold and HP are kept.</summary>
	Clone,

	/// <summary>Build a fresh player from <c>Player.CreateForNewRun</c> using the chosen
	/// character. No source player needed.</summary>
	FromEmpty
}

internal sealed class AddPlayerSpec
{
	public AddPlayerMode Mode { get; set; }
	public ulong NetId { get; set; }

	/// <summary>Required for <see cref="AddPlayerMode.Clone"/>. The NetId of an existing
	/// player in the snapshot whose record will be cloned.</summary>
	public ulong SourcePlayerNetId { get; set; }

	/// <summary>Required for <see cref="AddPlayerMode.FromEmpty"/>; ignored otherwise.</summary>
	public ModelId? CharacterIdOverride { get; set; }

	/// <summary>Optional auto-reward configuration for <see cref="AddPlayerMode.FromEmpty"/>.
	/// When non-null the apply path replays the source run's MapPointHistory and appends
	/// rolled rewards to <c>PreFinishedRoom.ExtraRewards[NetId]</c>.</summary>
	public AutoRewardConfig? AutoRewards { get; set; }
}

internal enum RewardEditAction { Add, Delete, Edit }

internal sealed class RewardEdit
{
	public RewardEditAction Action { get; set; }

	/// <summary>Player NetId whose pending-reward list is being modified.</summary>
	public ulong PlayerNetId { get; set; }

	/// <summary>For Edit/Delete: the index into that player's reward list.
	/// For Add: ignored.</summary>
	public int RewardIndex { get; set; }

	/// <summary>For Add/Edit: the reward type to write. Ignored for Delete.</summary>
	public RewardType RewardType { get; set; }

	/// <summary>For Add/Edit Card: number of card options the player will see (default 3).</summary>
	public int OptionCount { get; set; } = 3;

	/// <summary>For Add/Edit Card: which rarity-odds table to roll against.</summary>
	public CardRarityOddsType RarityOdds { get; set; } = CardRarityOddsType.RegularEncounter;

	/// <summary>For Add/Edit Gold: amount of gold to award.</summary>
	public int GoldAmount { get; set; }

	/// <summary>If non-null and non-none, the reward is "predetermined" to this model
	/// (specific card / relic). Default is none = auto-roll on resume.</summary>
	public ModelId? PredeterminedModelId { get; set; }

	/// <summary>For Add/Edit Card: card pool(s) to draw from. When empty the game will
	/// generate no card options. Caller should pass the player's character card pool id
	/// (e.g. <c>player.Character.CardPool.Id</c>).</summary>
	public List<ModelId>? CardPoolIds { get; set; }
}
