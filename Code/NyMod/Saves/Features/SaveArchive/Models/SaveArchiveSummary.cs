using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NyMod.Saves.Features.SaveArchive.Models;

internal sealed class SaveArchiveSummary
{
	[JsonPropertyName("summary_available")]
	public bool SummaryAvailable { get; set; }

	[JsonPropertyName("error_message")]
	public string? ErrorMessage { get; set; }

	[JsonPropertyName("character_ids")]
	public List<string> CharacterIds { get; set; } = new List<string>();


	[JsonPropertyName("player_count")]
	public int PlayerCount { get; set; }

	[JsonPropertyName("save_time")]
	public long SaveTime { get; set; }

	[JsonPropertyName("start_time")]
	public long StartTime { get; set; }

	[JsonPropertyName("run_time")]
	public long RunTime { get; set; }

	[JsonPropertyName("ascension")]
	public int Ascension { get; set; }

	[JsonPropertyName("current_act_index")]
	public int CurrentActIndex { get; set; }

	[JsonPropertyName("estimated_floor")]
	public int EstimatedFloor { get; set; }

	[JsonPropertyName("current_hp")]
	public int? CurrentHp { get; set; }

	[JsonPropertyName("max_hp")]
	public int? MaxHp { get; set; }

	[JsonPropertyName("gold")]
	public int? Gold { get; set; }

	[JsonPropertyName("game_mode")]
	public string? GameMode { get; set; }

	[JsonPropertyName("platform_type")]
	public string? PlatformType { get; set; }

	[JsonPropertyName("daily_time")]
	public DateTimeOffset? DailyTime { get; set; }
}
