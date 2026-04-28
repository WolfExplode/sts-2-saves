using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Features.SaveArchive.Models;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Walks every archived multiplayer snapshot one file at a time and yields the
/// distinct (NetId, DisplayLabel, LastSeen) tuples found. Designed for an opt-in
/// "Scan archived runs" UI flow — yields incrementally so the dialog can append
/// rows without freezing.
/// </summary>
internal sealed class PartnerNetIdHarvester
{
	private readonly SaveArchiveService _archiveService;

	public PartnerNetIdHarvester(SaveArchiveService archiveService)
	{
		_archiveService = archiveService;
	}

	public readonly struct PartnerInfo
	{
		public PartnerInfo(ulong netId, string label, DateTimeOffset lastSeen, string runId)
		{
			NetId = netId;
			Label = label;
			LastSeen = lastSeen;
			RunId = runId;
		}

		public ulong NetId { get; }
		public string Label { get; }
		public DateTimeOffset LastSeen { get; }
		public string RunId { get; }
	}

	/// <summary>
	/// Yield each distinct partner NetId encountered, scanning newest snapshots first per run.
	/// Cooperatively yields between every snapshot read so the UI thread can paint.
	/// </summary>
	public async IAsyncEnumerable<PartnerInfo> EnumeratePartnersAsync(
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
	{
		HashSet<ulong> seen = new HashSet<ulong>();

		IReadOnlyList<RunArchiveRecord> runs;
		try
		{
			runs = _archiveService.ListRuns(isMultiplayer: true);
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves PartnerNetIdHarvester: ListRuns failed: {ex.Message}");
			yield break;
		}

		foreach (RunArchiveRecord run in runs)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				yield break;
			}

			IReadOnlyList<SaveArchiveMetadata> snapshots;
			try
			{
				snapshots = _archiveService.ListSnapshots(true, run.RunId);
			}
			catch (Exception ex)
			{
				Log.Warn($"NyMod.Saves PartnerNetIdHarvester: LoadSnapshotsForRun('{run.RunId}') failed: {ex.Message}");
				continue;
			}

			foreach (SaveArchiveMetadata snapshot in snapshots)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					yield break;
				}

				await Task.Yield();

				if (!_archiveService.TryReadSnapshotPayload(snapshot, out byte[]? payload) || payload == null)
				{
					continue;
				}

				SerializableRun? saveData = null;
				try
				{
					string json = System.Text.Encoding.UTF8.GetString(payload);
					ReadSaveResult<SerializableRun> result = JsonSerializationUtility.FromJson<SerializableRun>(json);
					if (result.Success)
					{
						saveData = result.SaveData;
					}
				}
				catch (Exception ex)
				{
					Log.Warn($"NyMod.Saves PartnerNetIdHarvester: parse failed for '{snapshot.SaveId}': {ex.Message}");
					continue;
				}

				if (saveData == null || saveData.Players == null)
				{
					continue;
				}

				foreach (SerializablePlayer player in saveData.Players)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						yield break;
					}

					if (!seen.Add(player.NetId))
					{
						continue;
					}

					string charName = player.CharacterId?.Entry ?? "unknown";
					string label = $"{charName} (run {run.RunId})";
					yield return new PartnerInfo(player.NetId, label, snapshot.CreatedUtc, run.RunId);
				}
			}
		}
	}
}
