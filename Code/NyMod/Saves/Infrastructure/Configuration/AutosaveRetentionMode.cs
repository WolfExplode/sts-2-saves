using System.Text.Json.Serialization;

namespace NyMod.Saves.Infrastructure.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<AutosaveRetentionMode>))]
internal enum AutosaveRetentionMode
{
	All,
	CapLatest
}
