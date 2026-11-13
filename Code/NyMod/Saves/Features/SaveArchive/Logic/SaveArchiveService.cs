using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Logging;
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

	public bool CaptureCurrentSnapshot(SaveArchiveKind kind, string? note = null)
	{
		SaveManagerConfig saveManagerConfig = _configStore.LoadOrDefault();
		if (!_archiveStore.TryReadActivePayload(out byte[] payloadBytes, out string sourceFileName) || payloadBytes == null || string.IsNullOrEmpty(sourceFileName))
		{
			Log.Warn($"NyMod.Saves failed to capture {kind} snapshot because the active save payload was unavailable.", 2);
			return false;
		}
		SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
		ModDiagnostics.Trace("Existing save summary read; resolving archive identity");
		string runId = _runIdentityService.ResolveRunId(summary, payloadBytes);
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		string saveId = BuildSaveId(kind, utcNow);
		SaveArchiveMetadata metadata = new SaveArchiveMetadata
		{
			RunId = runId,
			SaveId = saveId,
			Kind = kind,
			SourceFileName = sourceFileName,
			CreatedUtc = utcNow,
			Note = note,
			Summary = summary
		};
		if (kind == SaveArchiveKind.Auto && saveManagerConfig.SkipDuplicateAutosaves && IsDuplicateOfLatest(metadata))
		{
			Log.Info($"NyMod.Saves skipped duplicate autosave for run '{runId}' because it matched the latest saved state.", 2);
			return false;
		}
		_archiveStore.SaveSnapshot(metadata, payloadBytes);
		if (kind == SaveArchiveKind.Auto && saveManagerConfig.AutosaveRetentionMode == AutosaveRetentionMode.CapLatest)
		{
			_archiveStore.TrimAutosaves(runId, saveManagerConfig.AutosaveCapPerRun);
		}
		Log.Info($"NyMod.Saves archived {kind} snapshot '{saveId}' under run '{runId}'", 2);
		return true;
	}

	public bool TryGetCurrentRunId(out string? runId)
	{
		runId = null;
		if (!_archiveStore.TryReadActivePayload(out var payload, out _) || payload == null)
		{
			return false;
		}
		runId = _runIdentityService.ResolveRunId(_summaryFactory.Create(payload), payload);
		return true;
	}

	public IReadOnlyList<RunArchiveRecord> ListRuns()
	{
		List<RunArchiveRecord> list = new List<RunArchiveRecord>();
		foreach (string item in _archiveStore.LoadRunIds())
		{
			RunArchiveMetadata runArchiveMetadata = _archiveStore.LoadRunMetadata(item);
			IReadOnlyList<SaveArchiveMetadata> readOnlyList = _archiveStore.LoadSnapshotsForRun(item, SaveArchiveKind.Auto);
			IReadOnlyList<SaveArchiveMetadata> readOnlyList2 = _archiveStore.LoadSnapshotsForRun(item, SaveArchiveKind.Manual);
			SaveArchiveMetadata saveArchiveMetadata = (from snapshot in readOnlyList.Concat(readOnlyList2)
				orderby snapshot.CreatedUtc descending
				select snapshot).FirstOrDefault();
			if (saveArchiveMetadata != null)
			{
				list.Add(new RunArchiveRecord
				{
					RunId = item,
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

	public IReadOnlyList<SaveArchiveMetadata> ListSnapshots(string runId, SaveArchiveKind kind)
	{
		return _archiveStore.LoadSnapshotsForRun(runId, kind);
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

	public string? BackupRun(string runId)
	{
		return _archiveStore.ExportRun(runId);
	}

	public void DeleteSnapshot(SaveArchiveMetadata metadata)
	{
		_archiveStore.DeleteSnapshot(metadata);
	}

	public void DeleteRun(string runId)
	{
		_archiveStore.DeleteRun(runId);
	}

	public bool UpdateSnapshotNote(SaveArchiveMetadata metadata, string? note)
	{
		metadata.Note = NormalizeNote(note);
		return _archiveStore.TryUpdateSnapshotMetadata(metadata);
	}

	public bool UpdateRunNote(string runId, string? note)
	{
		RunArchiveMetadata runArchiveMetadata = _archiveStore.LoadRunMetadata(runId) ?? new RunArchiveMetadata
		{
			RunId = runId,
		};
		runArchiveMetadata.Note = NormalizeNote(note);
		return _archiveStore.TryUpdateRunMetadata(runArchiveMetadata);
	}

	public string? GetRunNote(string runId)
	{
		return NormalizeNote(_archiveStore.LoadRunMetadata(runId)?.Note);
	}

	public bool TryGetRunDirectory(string runId, out string? runDirectory)
	{
		return _archiveStore.TryGetRunDirectory(runId, out runDirectory);
	}

	public bool TryGetSnapshotDirectory(SaveArchiveMetadata metadata, out string? snapshotDirectory)
	{
		return _archiveStore.TryGetSnapshotDirectory(metadata, out snapshotDirectory);
	}

	private bool IsDuplicateOfLatest(SaveArchiveMetadata candidate)
	{
		SaveArchiveMetadata saveArchiveMetadata = _archiveStore.LoadSnapshotsForRun(candidate.RunId, candidate.Kind).FirstOrDefault();
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
