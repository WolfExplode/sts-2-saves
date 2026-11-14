using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NyMod.Saves.Features.SaveArchive.Models;

internal sealed class RunArchiveRecord
{
	[JsonPropertyName("run_id")]
	public string RunId { get; set; } = string.Empty;


	[JsonPropertyName("auto_save_count")]
	public int AutoSaveCount => AutoSnapshots.Count;

	[JsonPropertyName("manual_save_count")]
	public int ManualSaveCount => ManualSnapshots.Count;

	[JsonIgnore]
	public IReadOnlyList<SaveArchiveMetadata> AutoSnapshots { get; init; } = Array.Empty<SaveArchiveMetadata>();

	[JsonIgnore]
	public IReadOnlyList<SaveArchiveMetadata> ManualSnapshots { get; init; } = Array.Empty<SaveArchiveMetadata>();

	[JsonPropertyName("latest_save_utc")]
	public DateTimeOffset? LatestSaveUtc { get; set; }

	[JsonPropertyName("latest_summary")]
	public SaveArchiveSummary? LatestSummary { get; set; }

	[JsonPropertyName("note")]
	public string? Note { get; set; }
}
