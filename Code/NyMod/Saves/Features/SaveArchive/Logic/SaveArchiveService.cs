using System;
using System.Collections.Generic;
using System.IO;
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

	public bool IsRestoring { get; set; }

	public bool CaptureCurrentSnapshot(SaveArchiveKind kind, string? note = null)
	{
		if (IsRestoring)
		{
			return false;
		}
		try
		{
			return CaptureSnapshot(kind, note);
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves could not capture {kind} snapshot: {ex}", 2);
			return false;
		}
	}

	private bool CaptureSnapshot(SaveArchiveKind kind, string? note)
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
		if (kind == SaveArchiveKind.Auto && saveManagerConfig.SkipDuplicateAutosaves && IsDuplicateOfLatest(metadata, payloadBytes))
		{
			Log.Info($"NyMod.Saves skipped duplicate autosave for run '{runId}' because it matched the latest saved state.", 2);
			return false;
		}
		if (!_archiveStore.SaveSnapshot(metadata, payloadBytes))
		{
			return false;
		}
		if (kind == SaveArchiveKind.Auto && saveManagerConfig.AutosaveRetentionMode == AutosaveRetentionMode.CapLatest)
		{
			try
			{
				_archiveStore.TrimAutosaves(runId, saveManagerConfig.AutosaveCapPerRun);
			}
			catch (Exception ex)
			{
				Log.Warn($"NyMod.Saves saved '{saveId}', but could not trim older autosaves: {ex}", 2);
			}
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
		var runs = new List<RunArchiveRecord>();
		foreach (string runId in _archiveStore.LoadRunIds())
		{
			var autosaves = _archiveStore.LoadSnapshotsForRun(runId, SaveArchiveKind.Auto);
			var manualSaves = _archiveStore.LoadSnapshotsForRun(runId, SaveArchiveKind.Manual);
			foreach (SaveArchiveMetadata snapshot in autosaves.Concat(manualSaves))
			{
				PopulateLegacyTotalFloor(snapshot);
			}
			SaveArchiveMetadata? latest = autosaves.Concat(manualSaves).MaxBy(snapshot => snapshot.CreatedUtc);
			if (latest != null)
			{
				runs.Add(new RunArchiveRecord
				{
					RunId = runId,
					AutoSnapshots = autosaves,
					ManualSnapshots = manualSaves,
					LatestSaveUtc = latest.CreatedUtc,
					LatestSummary = latest.Summary,
					Note = NormalizeNote(_archiveStore.LoadRunMetadata(runId)?.Note)
				});
			}
		}
		return runs.OrderByDescending(run => run.LatestSaveUtc).ToList();
	}

	private void PopulateLegacyTotalFloor(SaveArchiveMetadata snapshot)
	{
		if (!snapshot.Summary.SummaryAvailable || snapshot.Summary.TotalFloor.HasValue)
		{
			return;
		}
		try
		{
			byte[] payload = _archiveStore.ReadSnapshotPayload(snapshot);
			snapshot.Summary.TotalFloor = _summaryFactory.Create(payload).TotalFloor;
		}
		catch (IOException ex)
		{
			Log.Warn($"NyMod.Saves could not read the floor for snapshot '{snapshot.SaveId}': {ex.Message}", 2);
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warn($"NyMod.Saves could not read the floor for snapshot '{snapshot.SaveId}': {ex.Message}", 2);
		}
	}

	public bool HasSnapshots()
	{
		return _archiveStore.HasSnapshots();
	}

	public SnapshotRestore PrepareRestore(SaveArchiveMetadata metadata)
	{
		return _archiveStore.PrepareRestore(metadata);
	}

	public void MarkRestored(SaveArchiveMetadata metadata)
	{
		metadata.LastRestoredUtc = DateTimeOffset.UtcNow;
		try
		{
			_archiveStore.TryUpdateSnapshotMetadata(metadata);
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves loaded '{metadata.SaveId}', but could not update its metadata: {ex}", 2);
		}
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

	private bool IsDuplicateOfLatest(SaveArchiveMetadata candidate, byte[] payload)
	{
		SaveArchiveMetadata? latest = _archiveStore.LoadSnapshotsForRun(candidate.RunId, candidate.Kind).FirstOrDefault();
		if (latest == null)
		{
			return false;
		}
		try
		{
			return payload.AsSpan().SequenceEqual(_archiveStore.ReadSnapshotPayload(latest));
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves could not compare the previous autosave: {ex.Message}", 2);
			return false;
		}
	}

	private static string BuildSaveId(SaveArchiveKind kind, DateTimeOffset createdUtc)
	{
		string value = ((kind == SaveArchiveKind.Auto) ? "auto" : "manual");
		return $"{value}_{createdUtc:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}";
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
