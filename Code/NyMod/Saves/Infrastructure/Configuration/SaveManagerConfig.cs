using System.Text.Json.Serialization;

namespace NyMod.Saves.Infrastructure.Configuration;

internal sealed class SaveManagerConfig
{
	[JsonPropertyName("schema_version")]
	public int SchemaVersion { get; set; } = 1;


	[JsonPropertyName("autosave_retention_mode")]
	public AutosaveRetentionMode AutosaveRetentionMode { get; set; } = AutosaveRetentionMode.CapLatest;


	[JsonPropertyName("autosave_cap_per_run")]
	public int AutosaveCapPerRun { get; set; } = 100;


	[JsonPropertyName("skip_duplicate_autosaves")]
	public bool SkipDuplicateAutosaves { get; set; } = true;

}
