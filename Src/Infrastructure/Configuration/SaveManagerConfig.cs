using System.Text.Json.Serialization;

namespace NyMod.Saves.Infrastructure.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<AutosaveRetentionMode>))]
internal enum AutosaveRetentionMode
{
	All,
	CapLatest
}

internal sealed class SaveManagerConfig
{
	[JsonPropertyName("schema_version")]
	public int SchemaVersion { get; set; } = 1;

	[JsonPropertyName("autosave_retention_mode")]
	public AutosaveRetentionMode AutosaveRetentionMode { get; set; } = AutosaveRetentionMode.CapLatest;

	[JsonPropertyName("autosave_cap_per_run")]
	public int AutosaveCapPerRun { get; set; } = 100;

	[JsonPropertyName("capture_multiplayer_autosaves")]
	public bool CaptureMultiplayerAutosaves { get; set; } = true;

	[JsonPropertyName("manual_names_include_sequence")]
	public bool ManualNamesIncludeSequence { get; set; } = true;

	[JsonPropertyName("confirm_run_deletion")]
	public bool ConfirmRunDeletion { get; set; } = true;
}