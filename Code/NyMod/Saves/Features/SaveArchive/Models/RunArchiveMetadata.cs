using System.Text.Json.Serialization;

namespace NyMod.Saves.Features.SaveArchive.Models;

internal sealed class RunArchiveMetadata
{
	[JsonPropertyName("run_id")]
	public string RunId { get; set; } = string.Empty;


	[JsonPropertyName("note")]
	public string? Note { get; set; }
}
