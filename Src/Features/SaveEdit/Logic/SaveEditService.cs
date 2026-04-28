using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Features.SaveArchive.Models;

namespace NyMod.Saves.Features.SaveEdit.Logic;

internal sealed class SaveEditService
{
	private readonly SaveArchiveService _archiveService;

	public SaveEditService(SaveArchiveService archiveService)
	{
		_archiveService = archiveService;
	}

	/// <summary>
	/// Load and parse the snapshot's payload into a mutable <see cref="SerializableRun"/>.
	/// Returns null on read or parse failure.
	/// </summary>
	public SerializableRun? LoadForEdit(SaveArchiveMetadata metadata, out string? errorMessage)
	{
		errorMessage = null;
		if (!_archiveService.TryReadSnapshotPayload(metadata, out byte[]? payload) || payload == null)
		{
			errorMessage = "snapshot payload not readable";
			return null;
		}

		try
		{
			string json = Encoding.UTF8.GetString(payload);
			ReadSaveResult<SerializableRun> result = JsonSerializationUtility.FromJson<SerializableRun>(json);
			if (!result.Success || result.SaveData == null)
			{
				errorMessage = result.ErrorMessage ?? result.Status.ToString();
				return null;
			}

			return result.SaveData;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			return null;
		}
	}

	/// <summary>
	/// Apply <paramref name="payload"/> to the snapshot at <paramref name="source"/> and write
	/// the result as a new manual snapshot under the same run. The original snapshot is left
	/// untouched. Returns the metadata of the newly-written snapshot, or null on failure.
	/// </summary>
	public SaveArchiveMetadata? ApplyEdits(SaveArchiveMetadata source, SaveEditPayload payload)
	{
		if (source == null) throw new ArgumentNullException(nameof(source));
		if (payload == null) throw new ArgumentNullException(nameof(payload));

		SerializableRun? run = LoadForEdit(source, out string? loadError);
		if (run == null)
		{
			Log.Warn($"NyMod.Saves SaveEditService: failed to load source '{source.SaveId}' for editing: {loadError}");
			return null;
		}

		List<string> noteParts = new List<string>();

		try
		{
			ApplyAscension(run, payload, noteParts);
			ApplyPlayerRemovals(run, payload, noteParts);
			ApplyPlayerAdditions(run, payload, noteParts);
			ApplyRewardEdits(run, payload, noteParts);
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves SaveEditService: edit application failed for '{source.SaveId}': {ex.Message}");
			return null;
		}

		byte[] newPayload;
		try
		{
			using MemoryStream stream = new MemoryStream();
			JsonSerializer.Serialize(stream, run, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
			newPayload = stream.ToArray();
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves SaveEditService: re-serialize failed for '{source.SaveId}': {ex.Message}");
			return null;
		}

		string note = noteParts.Count == 0 ? "save_edit (no-op)" : "save_edit: " + string.Join("; ", noteParts);
		return _archiveService.WriteEditedSnapshot(source, newPayload, note);
	}

	private static void ApplyAscension(SerializableRun run, SaveEditPayload payload, List<string> noteParts)
	{
		if (payload.NewAscension is int newAsc && newAsc != run.Ascension)
		{
			noteParts.Add($"ascension {run.Ascension}->{newAsc}");
			run.Ascension = newAsc;
		}
	}

	private static void ApplyPlayerRemovals(SerializableRun run, SaveEditPayload payload, List<string> noteParts)
	{
		if (payload.RemovedPlayerNetIds.Count == 0 || run.Players == null)
		{
			return;
		}

		HashSet<ulong> toRemove = new HashSet<ulong>(payload.RemovedPlayerNetIds);
		int before = run.Players.Count;
		run.Players = run.Players.Where(p => !toRemove.Contains(p.NetId)).ToList();
		int removed = before - run.Players.Count;
		if (removed == 0)
		{
			return;
		}

		// Strip rewards in PreFinishedRoom keyed by removed NetIds.
		if (run.PreFinishedRoom?.ExtraRewards != null)
		{
			foreach (ulong netId in toRemove)
			{
				run.PreFinishedRoom.ExtraRewards.Remove(netId);
			}
		}

		// Strip player_stats entries for removed players in MapPointHistory.
		if (run.MapPointHistory != null)
		{
			foreach (List<MapPointHistoryEntry> floor in run.MapPointHistory)
			{
				foreach (MapPointHistoryEntry entry in floor)
				{
					if (entry.PlayerStats == null) continue;
					entry.PlayerStats = entry.PlayerStats.Where(s => !toRemove.Contains(s.PlayerId)).ToList();
				}
			}
		}

		noteParts.Add($"removed {removed} player(s)");
	}

	private void ApplyPlayerAdditions(SerializableRun run, SaveEditPayload payload, List<string> noteParts)
	{
		if (payload.AddedPlayers.Count == 0)
		{
			return;
		}

		run.Players ??= new List<SerializablePlayer>();
		HashSet<ulong> existingNetIds = new HashSet<ulong>(run.Players.Select(p => p.NetId));
		int added = 0;

		foreach (AddPlayerSpec spec in payload.AddedPlayers)
		{
			if (spec.NetId == 0UL || existingNetIds.Contains(spec.NetId))
			{
				Log.Warn($"NyMod.Saves SaveEditService: skipping AddPlayer with NetId {spec.NetId} (zero or duplicate).");
				continue;
			}

			SerializablePlayer? newPlayer = null;
			try
			{
				switch (spec.Mode)
				{
					case AddPlayerMode.FromEmpty:
						if (spec.CharacterIdOverride == null || spec.CharacterIdOverride == ModelId.none)
						{
							Log.Warn("NyMod.Saves SaveEditService: FromEmpty requires CharacterIdOverride.");
							continue;
						}
						newPlayer = PlayerFactory.BuildFromCharacter(spec.CharacterIdOverride, spec.NetId);
						break;

					case AddPlayerMode.Clone:
					{
						SerializablePlayer? source = run.Players.FirstOrDefault(p => p.NetId == spec.SourcePlayerNetId);
						if (source == null)
						{
							Log.Warn($"NyMod.Saves SaveEditService: clone source NetId {spec.SourcePlayerNetId} not found.");
							continue;
						}
						newPlayer = PlayerFactory.ClonePlayer(source, spec.NetId);
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"NyMod.Saves SaveEditService: AddPlayer failed (NetId={spec.NetId}, mode={spec.Mode}): {ex.Message}");
				continue;
			}

			if (newPlayer == null)
			{
				continue;
			}

			run.Players.Add(newPlayer);
			existingNetIds.Add(spec.NetId);
			added++;

			// Auto-extra-rewards: only meaningful for FromEmpty with a non-null config and a
			// pre-finished room to attach rewards to.
			if (spec.Mode == AddPlayerMode.FromEmpty && spec.AutoRewards != null)
			{
				if (run.PreFinishedRoom == null)
				{
					Log.Warn($"NyMod.Saves SaveEditService: AutoRewards requested but run has no PreFinishedRoom; skipped for NetId {spec.NetId}.");
				}
				else
				{
					int relicCount = AutoRewardGenerator.CountRelicRewards(run.MapPointHistory, spec.AutoRewards);
					int ancientCount = AutoRewardGenerator.CountAncientRelicRewards(run.MapPointHistory, spec.AutoRewards);
					(System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<ModelId>> ancientPools, _) = AncientCandidateGatherer.Gather(run);
					(List<ModelId> prerolled, List<ModelId> prerolledAncient) = (relicCount > 0 || ancientCount > 0)
						? RelicPreroller.RollAll(run, newPlayer, relicCount, ancientCount, ancientPools)
						: (new List<ModelId>(), new List<ModelId>());
					List<SerializableReward> rolled = AutoRewardGenerator.Build(run.MapPointHistory, spec.AutoRewards, newPlayer.CharacterId ?? ModelId.none, prerolled, prerolledAncient);
					if (rolled.Count > 0)
					{
						run.PreFinishedRoom.ExtraRewards ??= new Dictionary<ulong, List<SerializableReward>>();
						if (!run.PreFinishedRoom.ExtraRewards.TryGetValue(spec.NetId, out List<SerializableReward>? list) || list == null)
						{
							list = new List<SerializableReward>();
							run.PreFinishedRoom.ExtraRewards[spec.NetId] = list;
						}
						list.AddRange(rolled);
						noteParts.Add($"+{rolled.Count} auto-reward(s) for {spec.NetId}");
					}
				}
			}

			// Mirror the new player into MapPointHistory so subsequent reads don't throw on GetEntry.
			if (run.MapPointHistory != null)
			{
				foreach (List<MapPointHistoryEntry> floor in run.MapPointHistory)
				{
					foreach (MapPointHistoryEntry entry in floor)
					{
						if (entry.PlayerStats == null)
						{
							entry.PlayerStats = new List<PlayerMapPointHistoryEntry>();
						}
						if (!entry.PlayerStats.Any(s => s.PlayerId == spec.NetId))
						{
							entry.PlayerStats.Add(new PlayerMapPointHistoryEntry { PlayerId = spec.NetId });
						}
					}
				}
			}
		}

		if (added > 0)
		{
			noteParts.Add($"added {added} player(s)");
		}
	}

	private static void ApplyRewardEdits(SerializableRun run, SaveEditPayload payload, List<string> noteParts)
	{
		if (payload.RewardEdits.Count == 0)
		{
			return;
		}

		if (run.PreFinishedRoom == null)
		{
			Log.Warn("NyMod.Saves SaveEditService: reward edits requested but run has no PreFinishedRoom.");
			return;
		}

		run.PreFinishedRoom.ExtraRewards ??= new Dictionary<ulong, List<SerializableReward>>();
		Dictionary<ulong, List<SerializableReward>> rewards = run.PreFinishedRoom.ExtraRewards;

		int adds = 0, dels = 0, edits = 0;

		foreach (RewardEdit edit in payload.RewardEdits)
		{
			if (!rewards.TryGetValue(edit.PlayerNetId, out List<SerializableReward>? list) || list == null)
			{
				if (edit.Action == RewardEditAction.Add)
				{
					list = new List<SerializableReward>();
					rewards[edit.PlayerNetId] = list;
				}
				else
				{
					Log.Warn($"NyMod.Saves SaveEditService: reward {edit.Action} on missing player NetId {edit.PlayerNetId}.");
					continue;
				}
			}

			switch (edit.Action)
			{
				case RewardEditAction.Add:
					list.Add(RewardFactory.Build(edit));
					adds++;
					break;

				case RewardEditAction.Delete:
					if (edit.RewardIndex >= 0 && edit.RewardIndex < list.Count)
					{
						list.RemoveAt(edit.RewardIndex);
						dels++;
					}
					break;

				case RewardEditAction.Edit:
					if (edit.RewardIndex >= 0 && edit.RewardIndex < list.Count)
					{
						list[edit.RewardIndex] = RewardFactory.Build(edit);
						edits++;
					}
					break;
			}
		}

		if (adds > 0) noteParts.Add($"+{adds} reward(s)");
		if (dels > 0) noteParts.Add($"-{dels} reward(s)");
		if (edits > 0) noteParts.Add($"~{edits} reward(s)");
	}
}
