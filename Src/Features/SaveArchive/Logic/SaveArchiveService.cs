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
using MegaCrit.Sts2.Core.Saves.Runs;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Infrastructure.Configuration;

namespace NyMod.Saves.Features.SaveArchive.Logic;

internal sealed class SaveArchiveService
{
	private readonly SaveManagerConfigStore _configStore;
	private readonly SaveArchiveStore _archiveStore;
	private readonly SaveArchiveSummaryFactory _summaryFactory;
	private readonly RunIdentityService _runIdentityService;

	public SaveArchiveService(
		SaveManagerConfigStore configStore,
		SaveArchiveStore archiveStore,
		SaveArchiveSummaryFactory summaryFactory,
		RunIdentityService runIdentityService)
	{
		_configStore = configStore;
		_archiveStore = archiveStore;
		_summaryFactory = summaryFactory;
		_runIdentityService = runIdentityService;
	}

	public bool CaptureCurrentSnapshot(SaveArchiveKind kind, bool isMultiplayer, string? note = null)
	{
		SaveManagerConfig config = _configStore.LoadOrDefault();
		if (kind == SaveArchiveKind.Auto && isMultiplayer && !config.CaptureMultiplayerAutosaves)
		{
			Log.Info("NyMod.Saves skipped multiplayer autosave capture because config disabled it.");
			return false;
		}

		if (!_archiveStore.TryReadActivePayload(isMultiplayer, out byte[]? payloadBytes, out string? sourceFileName) || payloadBytes == null || string.IsNullOrEmpty(sourceFileName))
		{
			Log.Warn($"NyMod.Saves failed to capture {(isMultiplayer ? "multiplayer" : "singleplayer")} {kind} snapshot because the active save payload was unavailable.");
			return false;
		}

		SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
		string runId = _runIdentityService.ResolveRunId(summary, payloadBytes, isMultiplayer);
		DateTimeOffset createdUtc = DateTimeOffset.UtcNow;
		string saveId = BuildSaveId(kind, createdUtc);
		SaveArchiveMetadata metadata = new SaveArchiveMetadata
		{
			RunId = runId,
			SaveId = saveId,
			Kind = kind,
			IsMultiplayer = isMultiplayer,
			SourceFileName = sourceFileName,
			CreatedUtc = createdUtc,
			Note = note,
			Summary = summary
		};

		_archiveStore.SaveSnapshot(metadata, payloadBytes);
		if (kind == SaveArchiveKind.Auto && config.AutosaveRetentionMode == AutosaveRetentionMode.CapLatest)
		{
			_archiveStore.TrimAutosaves(runId, isMultiplayer, config.AutosaveCapPerRun);
		}

		Log.Info($"NyMod.Saves archived {(isMultiplayer ? "multiplayer" : "singleplayer")} {kind} snapshot '{saveId}' under run '{runId}'");
		return true;
	}

	public bool CaptureManualSnapshot(bool isMultiplayer, string? note = null)
	{
		return CaptureCurrentSnapshot(SaveArchiveKind.Manual, isMultiplayer, note);
	}

	public bool CaptureFromMemory(SaveArchiveKind kind, string? note = null)
	{
		if (!RunManager.Instance.IsInProgress)
		{
			Log.Warn("NyMod.Saves cannot capture from memory: no run in progress.");
			return false;
		}

		try
		{
			SerializableRun serializableRun = RunManager.Instance.ToSave(null);
			using MemoryStream stream = new MemoryStream();
			JsonSerializer.Serialize(stream, serializableRun, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
			byte[] payloadBytes = stream.ToArray();

			bool isMultiplayer = RunManager.Instance.NetService.Type.IsMultiplayer();
			SaveManagerConfig config = _configStore.LoadOrDefault();
			if (kind == SaveArchiveKind.Auto && isMultiplayer && !config.CaptureMultiplayerAutosaves)
			{
				Log.Info("NyMod.Saves skipped client memory autosave capture because config disabled multiplayer autosaves.");
				return false;
			}

			SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
			string runId = _runIdentityService.ResolveRunId(summary, payloadBytes, isMultiplayer);
			DateTimeOffset createdUtc = DateTimeOffset.UtcNow;
			string saveId = BuildSaveId(kind, createdUtc);
			SaveArchiveMetadata metadata = new SaveArchiveMetadata
			{
				RunId = runId,
				SaveId = saveId,
				Kind = kind,
				IsMultiplayer = isMultiplayer,
				SourceFileName = "memory_capture",
				CreatedUtc = createdUtc,
				Note = note,
				Summary = summary
			};

			_archiveStore.SaveSnapshot(metadata, payloadBytes);
			if (kind == SaveArchiveKind.Auto && config.AutosaveRetentionMode == AutosaveRetentionMode.CapLatest)
			{
				_archiveStore.TrimAutosaves(runId, isMultiplayer, config.AutosaveCapPerRun);
			}

			Log.Info($"NyMod.Saves archived from memory {(isMultiplayer ? "multiplayer" : "singleplayer")} {kind} snapshot '{saveId}' under run '{runId}'");
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves failed to capture from memory: {ex.Message}");
			return false;
		}
	}

	public bool PreserveCurrentRunBeforeReplacement(bool isMultiplayer, string? note = null)
	{
		return CaptureCurrentSnapshot(SaveArchiveKind.Manual, isMultiplayer, note ?? "pre_new_run");
	}

	/// <summary>
	/// Scans both the singleplayer and multiplayer active-save files on disk and
	/// archives any whose <c>runId</c> has no snapshots yet. Covers the case where
	/// the mod was installed after a save already existed on disk, or when an
	/// in-progress save belongs to a mode the player hasn't opened in-game since
	/// install.
	/// <para>
	/// Skips per-mode scanning when <c>nymod.saves/archive/&lt;multiplayer|singleplayer&gt;</c>
	/// already exists — once the mode directory has been created, the initial sweep
	/// is considered done and we never re-scan automatically. Users who want the
	/// scan to re-run can delete the mode directory.
	/// </para>
	/// </summary>
	public void TryAutoCaptureExistingSavesOnDisk()
	{
		foreach (bool isMultiplayer in new[] { false, true })
		{
			string tag = isMultiplayer ? "MP" : "SP";
			try
			{
				if (_archiveStore.ModeDirectoryExists(isMultiplayer))
				{
					Log.Info($"NyMod.Saves on-disk scan ({tag}): mode directory already exists; skipping initial sweep");
					continue;
				}

				if (!_archiveStore.TryReadActivePayload(isMultiplayer, out byte[]? payloadBytes, out string? sourceFileName)
					|| payloadBytes == null
					|| string.IsNullOrEmpty(sourceFileName))
				{
					Log.Info($"NyMod.Saves on-disk scan ({tag}): no active payload");
					continue;
				}

				Log.Info($"NyMod.Saves on-disk scan ({tag}): read {payloadBytes.Length} bytes from '{sourceFileName}'");
				SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
				string runId = _runIdentityService.ResolveRunId(summary, payloadBytes, isMultiplayer);
				if (string.IsNullOrEmpty(runId))
				{
					Log.Info($"NyMod.Saves on-disk scan ({tag}): could not resolve runId");
					continue;
				}

				IReadOnlyList<SaveArchiveMetadata> existing = ListSnapshots(isMultiplayer, runId);
				if (existing.Count > 0)
				{
					Log.Info($"NyMod.Saves on-disk scan ({tag}): runId '{runId}' already has {existing.Count} snapshot(s); skipping");
					continue;
				}

				DateTimeOffset createdUtc = DateTimeOffset.UtcNow;
				string saveId = BuildSaveId(SaveArchiveKind.Manual, createdUtc);
				SaveArchiveMetadata metadata = new SaveArchiveMetadata
				{
					RunId = runId,
					SaveId = saveId,
					Kind = SaveArchiveKind.Manual,
					IsMultiplayer = isMultiplayer,
					SourceFileName = sourceFileName,
					CreatedUtc = createdUtc,
					Note = "auto-captured: existing save discovered on disk",
					Summary = summary
				};

				_archiveStore.SaveSnapshot(metadata, payloadBytes);
				Log.Info($"NyMod.Saves auto-archived existing {(isMultiplayer ? "multiplayer" : "singleplayer")} save '{saveId}' under run '{runId}'");
			}
			catch (Exception ex)
			{
				Log.Warn($"NyMod.Saves TryAutoCaptureExistingSavesOnDisk ({(isMultiplayer ? "MP" : "SP")}) failed: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// If a run is currently in progress and no archived snapshot exists yet for that run
	/// (e.g. the mod was installed mid-run, or the player hasn't autosaved since install),
	/// captures the in-memory run as a Manual snapshot. Safe to call repeatedly — no-ops
	/// when the run already has any archived snapshot or when no run is in progress.
	/// Returns true if a new snapshot was captured.
	/// </summary>
	public bool TryAutoCaptureCurrentRunIfMissing()
	{
		try
		{
			if (!RunManager.Instance.IsInProgress)
			{
				return false;
			}

			bool isMultiplayer = RunManager.Instance.NetService.Type.IsMultiplayer();
			if (!TryResolveCurrentRunId(isMultiplayer, out string? runId) || string.IsNullOrEmpty(runId))
			{
				return false;
			}

			IReadOnlyList<SaveArchiveMetadata> existing = ListSnapshots(isMultiplayer, runId);
			if (existing.Count > 0)
			{
				return false;
			}

			return CaptureFromMemory(SaveArchiveKind.Manual, note: "auto-captured: first time mod saw this run");
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves TryAutoCaptureCurrentRunIfMissing failed: {ex.Message}");
			return false;
		}
	}

	public bool DeleteArchivedRunForCurrentActiveSave(bool isMultiplayer)
	{
		if (!TryResolveCurrentRunId(isMultiplayer, out string? runId) || string.IsNullOrEmpty(runId))
		{
			return false;
		}

		_archiveStore.DeleteRun(isMultiplayer, runId);
		Log.Info($"NyMod.Saves removed archived run '{runId}' for {(isMultiplayer ? "multiplayer" : "singleplayer")} active save cleanup");
		return true;
	}

	public bool TryGetCurrentRunId(bool isMultiplayer, out string? runId)
	{
		return TryResolveCurrentRunId(isMultiplayer, out runId);
	}

	public IReadOnlyList<RunArchiveRecord> ListRuns(bool isMultiplayer)
	{
		List<RunArchiveRecord> records = new List<RunArchiveRecord>();
		foreach (string runId in _archiveStore.LoadRunIds(isMultiplayer))
		{
			RunArchiveMetadata? runMetadata = _archiveStore.LoadRunMetadata(isMultiplayer, runId);
			IReadOnlyList<SaveArchiveMetadata> autos = _archiveStore.LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Auto);
			IReadOnlyList<SaveArchiveMetadata> manuals = _archiveStore.LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Manual);
			SaveArchiveMetadata? latest = autos.Concat(manuals)
				.OrderByDescending(static snapshot => snapshot.CreatedUtc)
				.FirstOrDefault();

			records.Add(new RunArchiveRecord
			{
				RunId = runId,
				IsMultiplayer = isMultiplayer,
				AutoSaveCount = autos.Count,
				ManualSaveCount = manuals.Count,
				LatestSaveUtc = latest?.CreatedUtc,
				LatestSummary = latest?.Summary,
				Note = NormalizeNote(runMetadata?.Note)
			});
		}

		return records
			.OrderByDescending(static record => record.LatestSaveUtc)
			.ToList();
	}

	public IReadOnlyList<SaveArchiveMetadata> ListSnapshots(bool isMultiplayer, string runId)
	{
		return _archiveStore.LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Auto)
			.Concat(_archiveStore.LoadSnapshotsForRun(isMultiplayer, runId, SaveArchiveKind.Manual))
			.OrderByDescending(static snapshot => snapshot.CreatedUtc)
			.ToList();
	}

	public bool RestoreSnapshot(SaveArchiveMetadata metadata)
	{
		bool restored = _archiveStore.TryRestoreSnapshot(metadata);
		if (restored)
		{
			metadata.LastRestoredUtc = DateTimeOffset.UtcNow;
			_ = _archiveStore.TryUpdateSnapshotMetadata(metadata);
			Log.Info($"NyMod.Saves restored snapshot '{metadata.SaveId}' for run '{metadata.RunId}'");
		}

		return restored;
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
		RunArchiveMetadata metadata = _archiveStore.LoadRunMetadata(isMultiplayer, runId) ?? new RunArchiveMetadata
		{
			RunId = runId,
			IsMultiplayer = isMultiplayer
		};

		metadata.Note = NormalizeNote(note);
		return _archiveStore.TryUpdateRunMetadata(metadata);
	}

	public RunArchiveRecord? GetRun(bool isMultiplayer, string runId)
	{
		foreach (RunArchiveRecord run in ListRuns(isMultiplayer))
		{
			if (string.Equals(run.RunId, runId, StringComparison.Ordinal) && run.IsMultiplayer == isMultiplayer)
			{
				return run;
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

	public bool TryReadSnapshotPayload(SaveArchiveMetadata metadata, out byte[]? payloadBytes)
	{
		return _archiveStore.TryReadSnapshotPayload(metadata, out payloadBytes);
	}

	internal SaveArchiveStore Store => _archiveStore;
	internal SaveArchiveSummaryFactory SummaryFactory => _summaryFactory;

	public SaveArchiveMetadata? WriteEditedSnapshot(SaveArchiveMetadata source, byte[] payloadBytes, string? note)
	{
		try
		{
			SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
			DateTimeOffset createdUtc = DateTimeOffset.UtcNow;
			string saveId = BuildSaveId(SaveArchiveKind.Manual, createdUtc);
			SaveArchiveMetadata metadata = new SaveArchiveMetadata
			{
				RunId = source.RunId,
				SaveId = saveId,
				Kind = SaveArchiveKind.Manual,
				IsMultiplayer = source.IsMultiplayer,
				SourceFileName = "save_edit",
				CreatedUtc = createdUtc,
				Note = NormalizeNote(note),
				Summary = summary
			};

			_archiveStore.SaveSnapshot(metadata, payloadBytes);
			Log.Info($"NyMod.Saves wrote edited snapshot '{saveId}' from source '{source.SaveId}' under run '{source.RunId}'.");
			return metadata;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves failed to write edited snapshot from '{source.SaveId}': {ex.Message}");
			return null;
		}
	}

	private bool TryResolveCurrentRunId(bool isMultiplayer, out string? runId)
	{
		runId = null;
		if (!_archiveStore.TryReadActivePayload(isMultiplayer, out byte[]? payloadBytes, out _) || payloadBytes == null)
		{
			return false;
		}

		SaveArchiveSummary summary = _summaryFactory.Create(payloadBytes);
		runId = _runIdentityService.ResolveRunId(summary, payloadBytes, isMultiplayer);
		return !string.IsNullOrEmpty(runId);
	}

	private static string BuildSaveId(SaveArchiveKind kind, DateTimeOffset createdUtc)
	{
		string prefix = kind == SaveArchiveKind.Auto ? "auto" : "manual";
		return $"{prefix}_{createdUtc:yyyyMMdd_HHmmss_fff}";
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