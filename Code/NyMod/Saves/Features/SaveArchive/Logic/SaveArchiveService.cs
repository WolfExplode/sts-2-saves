using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Infrastructure.Configuration;
using NyMod.Saves.Infrastructure.Diagnostics;

namespace NyMod.Saves.Features.SaveArchive.Logic;

internal sealed class SaveArchiveService
{
	private readonly SaveManagerConfigStore _configStore;

	private readonly SaveArchiveStore _archiveStore;

	private readonly SaveArchiveSummaryFactory _summaryFactory;

	private readonly RunIdentityService _runIdentityService;

	public SaveArchiveService(SaveManagerConfigStore configStore, SaveArchiveStore archiveStore, SaveArchiveSummaryFactory summaryFactory, RunIdentityService runIdentityService)
	{
		_configStore = configStore;
		_archiveStore = archiveStore;
		_summaryFactory = summaryFactory;
		_runIdentityService = runIdentityService;
	}

	public bool CaptureCurrentSnapshot(SaveArchiveKind kind, bool isMultiplayer, string? note = null)
	{
		SaveManagerConfig saveManagerConfig = _configStore.LoadOrDefault();
		if (kind == SaveArchiveKind.Auto && isMultiplayer && !saveManagerConfig.CaptureMultiplayerAutosaves)
		{
			Log.Info("NyMod.Saves skipped multiplayer autosave capture because config disabled it.", 2);
			return false;
		}
		if (!_archiveStore.TryReadActivePayload(isMultiplayer, out byte[] payloadBytes, out string sourceFileName) || payloadBytes == null || string.IsNullOrEmpty(sourceFileName))
		{
			Log.Warn($"NyMod.Saves failed to capture {(isMultiplayer ? "multiplayer" : "singleplayer")} {kind} snapshot because the active save payload was unavailable.", 2);
			return false;
		}
		SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
		ModDiagnostics.Trace("Existing save summary read; resolving archive identity");
		string text = _runIdentityService.ResolveRunId(summary, payloadBytes, isMultiplayer);
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		string text2 = BuildSaveId(kind, utcNow);
		SaveArchiveMetadata metadata = new SaveArchiveMetadata
		{
			RunId = text,
			SaveId = text2,
			Kind = kind,
			IsMultiplayer = isMultiplayer,
			SourceFileName = sourceFileName,
			CreatedUtc = utcNow,
			Note = note,
			Summary = summary
		};
		if (kind == SaveArchiveKind.Auto && saveManagerConfig.SkipDuplicateAutosaves && IsDuplicateOfLatest(metadata))
		{
			Log.Info($"NyMod.Saves skipped duplicate autosave for run '{text}' because it matched the latest saved state.", 2);
			return false;
		}
		_archiveStore.SaveSnapshot(metadata, payloadBytes);
		if (kind == SaveArchiveKind.Auto && saveManagerConfig.AutosaveRetentionMode == AutosaveRetentionMode.CapLatest)
		{
			_archiveStore.TrimAutosaves(text, isMultiplayer, saveManagerConfig.AutosaveCapPerRun);
		}
		Log.Info($"NyMod.Saves archived {(isMultiplayer ? "multiplayer" : "singleplayer")} {kind} snapshot '{text2}' under run '{text}'", 2);
		return true;
	}

	public bool CaptureManualSnapshot(bool isMultiplayer, string? note = null)
	{
		return CaptureCurrentSnapshot(SaveArchiveKind.Manual, isMultiplayer, note);
	}

	public bool CaptureFromMemory(SaveArchiveKind kind, string? note = null)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			SerializableRun value = RunManager.Instance.ToSave((AbstractRoom)null);
			using MemoryStream memoryStream = new MemoryStream();
			JsonSerializer.Serialize(memoryStream, value, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
			byte[] payloadBytes = memoryStream.ToArray();
			bool flag = NetGameTypeExtensions.IsMultiplayer(RunManager.Instance.NetService.Type);
			SaveManagerConfig saveManagerConfig = _configStore.LoadOrDefault();
			if (kind == SaveArchiveKind.Auto && flag && !saveManagerConfig.CaptureMultiplayerAutosaves)
			{
				Log.Info("NyMod.Saves skipped client memory autosave capture because config disabled multiplayer autosaves.", 2);
				return false;
			}
			SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
			string text = _runIdentityService.ResolveRunId(summary, payloadBytes, flag);
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			string text2 = BuildSaveId(kind, utcNow);
			SaveArchiveMetadata metadata = new SaveArchiveMetadata
			{
				RunId = text,
				SaveId = text2,
				Kind = kind,
				IsMultiplayer = flag,
				SourceFileName = "memory_capture",
				CreatedUtc = utcNow,
				Note = note,
				Summary = summary
			};
			if (kind == SaveArchiveKind.Auto && saveManagerConfig.SkipDuplicateAutosaves && IsDuplicateOfLatest(metadata))
			{
				Log.Info($"NyMod.Saves skipped duplicate from-memory autosave for run '{text}' because it matched the latest saved state.", 2);
				return false;
			}
			_archiveStore.SaveSnapshot(metadata, payloadBytes);
			if (kind == SaveArchiveKind.Auto && saveManagerConfig.AutosaveRetentionMode == AutosaveRetentionMode.CapLatest)
			{
				_archiveStore.TrimAutosaves(text, flag, saveManagerConfig.AutosaveCapPerRun);
			}
			Log.Info($"NyMod.Saves archived from memory {(flag ? "multiplayer" : "singleplayer")} {kind} snapshot '{text2}' under run '{text}'", 2);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn("NyMod.Saves failed to capture from memory: " + ex.Message, 2);
			return false;
		}
	}

	public bool PreserveCurrentRunBeforeReplacement(bool isMultiplayer, string? note = null)
	{
		return CaptureCurrentSnapshot(SaveArchiveKind.Manual, isMultiplayer, note ?? "pre_new_run");
	}

	public bool TryGetCurrentRunId(bool isMultiplayer, out string? runId)
	{
		return TryResolveCurrentRunId(isMultiplayer, out runId);
	}

	public IReadOnlyList<RunArchiveRecord> ListRuns(bool isMultiplayer)
	{
		List<RunArchiveRecord> list = new List<RunArchiveRecord>();
		foreach (string item in _archiveStore.LoadRunIds(isMultiplayer))
		{
			RunArchiveMetadata runArchiveMetadata = _archiveStore.LoadRunMetadata(isMultiplayer, item);
			IReadOnlyList<SaveArchiveMetadata> readOnlyList = _archiveStore.LoadSnapshotsForRun(isMultiplayer, item, SaveArchiveKind.Auto);
			IReadOnlyList<SaveArchiveMetadata> readOnlyList2 = _archiveStore.LoadSnapshotsForRun(isMultiplayer, item, SaveArchiveKind.Manual);
			SaveArchiveMetadata saveArchiveMetadata = (from snapshot in readOnlyList.Concat(readOnlyList2)
				orderby snapshot.CreatedUtc descending
				select snapshot).FirstOrDefault();
			if (saveArchiveMetadata != null)
			{
				list.Add(new RunArchiveRecord
				{
					RunId = item,
					IsMultiplayer = isMultiplayer,
					AutoSaveCount = readOnlyList.Count,
					ManualSaveCount = readOnlyList2.Count,
					LatestSaveUtc = saveArchiveMetadata?.CreatedUtc,
					LatestSummary = saveArchiveMetadata?.Summary,
					Note = NormalizeNote(runArchiveMetadata?.Note)
				});
			}
		}
		return list.OrderByDescending((RunArchiveRecord record) => record.LatestSaveUtc).ToList();
	}

	public IReadOnlyList<SaveArchiveMetadata> ListSnapshots(bool isMultiplayer, string runId)
	{
		return (from snapshot in _archiveStore.LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Auto).Concat(_archiveStore.LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Manual))
			orderby snapshot.CreatedUtc descending
			select snapshot).ToList();
	}

	public bool RestoreSnapshot(SaveArchiveMetadata metadata)
	{
		bool flag = _archiveStore.TryRestoreSnapshot(metadata);
		if (flag)
		{
			metadata.LastRestoredUtc = DateTimeOffset.UtcNow;
			_archiveStore.TryUpdateSnapshotMetadata(metadata);
			Log.Info($"NyMod.Saves restored snapshot '{metadata.SaveId}' for run '{metadata.RunId}'", 2);
		}
		return flag;
	}

	public string? BackupSnapshot(SaveArchiveMetadata metadata)
	{
		return _archiveStore.ExportSnapshot(metadata);
	}

	public string? BackupRun(bool isMultiplayer, string runId)
	{
		return _archiveStore.ExportRun(isMultiplayer, runId);
	}

	public void DeleteSnapshot(SaveArchiveMetadata metadata)
	{
		_archiveStore.DeleteSnapshot(metadata);
	}

	public void DeleteRun(bool isMultiplayer, string runId)
	{
		_archiveStore.DeleteRun(isMultiplayer, runId);
	}

	public bool UpdateSnapshotNote(SaveArchiveMetadata metadata, string? note)
	{
		metadata.Note = NormalizeNote(note);
		return _archiveStore.TryUpdateSnapshotMetadata(metadata);
	}

	public bool UpdateRunNote(bool isMultiplayer, string runId, string? note)
	{
		RunArchiveMetadata runArchiveMetadata = _archiveStore.LoadRunMetadata(isMultiplayer, runId) ?? new RunArchiveMetadata
		{
			RunId = runId,
			IsMultiplayer = isMultiplayer
		};
		runArchiveMetadata.Note = NormalizeNote(note);
		return _archiveStore.TryUpdateRunMetadata(runArchiveMetadata);
	}

	public RunArchiveRecord? GetRun(bool isMultiplayer, string runId)
	{
		foreach (RunArchiveRecord item in ListRuns(isMultiplayer))
		{
			if (string.Equals(item.RunId, runId, StringComparison.Ordinal) && item.IsMultiplayer == isMultiplayer)
			{
				return item;
			}
		}
		return null;
	}

	public string? GetRunNote(bool isMultiplayer, string runId)
	{
		return NormalizeNote(_archiveStore.LoadRunMetadata(isMultiplayer, runId)?.Note);
	}

	public bool TryGetRunDirectory(bool isMultiplayer, string runId, out string? runDirectory)
	{
		return _archiveStore.TryGetRunDirectory(isMultiplayer, runId, out runDirectory);
	}

	public bool TryGetSnapshotDirectory(SaveArchiveMetadata metadata, out string? snapshotDirectory)
	{
		return _archiveStore.TryGetSnapshotDirectory(metadata, out snapshotDirectory);
	}

	private bool TryResolveCurrentRunId(bool isMultiplayer, out string? runId)
	{
		runId = null;
		if (!_archiveStore.TryReadActivePayload(isMultiplayer, out byte[] payloadBytes, out string _) || payloadBytes == null)
		{
			return false;
		}
		SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
		runId = _runIdentityService.ResolveRunId(summary, payloadBytes, isMultiplayer);
		return !string.IsNullOrEmpty(runId);
	}

	private bool IsDuplicateOfLatest(SaveArchiveMetadata candidate)
	{
		SaveArchiveMetadata saveArchiveMetadata = _archiveStore.LoadSnapshotsForRun(candidate.IsMultiplayer, candidate.RunId, candidate.Kind).FirstOrDefault();
		if (saveArchiveMetadata == null)
		{
			return false;
		}
		return AreSummariesEquivalent(saveArchiveMetadata.Summary, candidate.Summary);
	}

	private static bool AreSummariesEquivalent(SaveArchiveSummary? a, SaveArchiveSummary? b)
	{
		if (a == null || b == null || !a.SummaryAvailable || !b.SummaryAvailable)
		{
			return false;
		}
		return a.CurrentActIndex == b.CurrentActIndex && a.EstimatedFloor == b.EstimatedFloor && a.CurrentHp == b.CurrentHp && a.MaxHp == b.MaxHp && a.Gold == b.Gold && a.Ascension == b.Ascension && a.PlayerCount == b.PlayerCount && a.GameMode == b.GameMode && a.CharacterIds.SequenceEqual(b.CharacterIds);
	}

	private static string BuildSaveId(SaveArchiveKind kind, DateTimeOffset createdUtc)
	{
		string value = ((kind == SaveArchiveKind.Auto) ? "auto" : "manual");
		return $"{value}_{createdUtc:yyyyMMdd_HHmmss_fff}";
	}

	private static string? NormalizeNote(string? note)
	{
		if (string.IsNullOrWhiteSpace(note))
		{
			return null;
		}
		return note.Trim();
	}
}
