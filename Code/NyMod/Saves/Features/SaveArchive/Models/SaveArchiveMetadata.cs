using System;
using System.Text.Json.Serialization;
using NyMod.Saves.Features.SaveArchive.Logic;

namespace NyMod.Saves.Features.SaveArchive.Models;

internal sealed class SaveArchiveMetadata
{
	[JsonPropertyName("schema_version")]
	public int SchemaVersion { get; set; } = 1;


	[JsonPropertyName("run_id")]
	public string RunId { get; set; } = string.Empty;


	[JsonPropertyName("save_id")]
	public string SaveId { get; set; } = string.Empty;


	[JsonPropertyName("kind")]
	public SaveArchiveKind Kind { get; set; }

	[JsonPropertyName("is_multiplayer")]
	public bool IsMultiplayer { get; set; }

	[JsonPropertyName("source_file_name")]
	public string SourceFileName { get; set; } = string.Empty;


	[JsonPropertyName("created_utc")]
	public DateTimeOffset CreatedUtc { get; set; }

	[JsonPropertyName("last_restored_utc")]
	public DateTimeOffset? LastRestoredUtc { get; set; }

	[JsonPropertyName("note")]
	public string? Note { get; set; }

	[JsonPropertyName("summary")]
	public SaveArchiveSummary Summary { get; set; } = new SaveArchiveSummary();

}
