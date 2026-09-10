using System;
using System.Text.Json.Serialization;

namespace NyMod.Saves.Features.SaveArchive.Models;

internal sealed class RunArchiveRecord
{
	[JsonPropertyName("run_id")]
	public string RunId { get; set; } = string.Empty;


	[JsonPropertyName("is_multiplayer")]
	public bool IsMultiplayer { get; set; }

	[JsonPropertyName("auto_save_count")]
	public int AutoSaveCount { get; set; }

	[JsonPropertyName("manual_save_count")]
	public int ManualSaveCount { get; set; }

	[JsonPropertyName("latest_save_utc")]
	public DateTimeOffset? LatestSaveUtc { get; set; }

	[JsonPropertyName("latest_summary")]
	public SaveArchiveSummary? LatestSummary { get; set; }

	[JsonPropertyName("note")]
	public string? Note { get; set; }
}
