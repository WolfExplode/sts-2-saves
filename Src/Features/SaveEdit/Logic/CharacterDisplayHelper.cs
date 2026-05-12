using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Resolves a <see cref="ModelId"/> to a localized character display name
/// (e.g. "Watcher" / "观者") via <see cref="LocString.GetFormattedText"/>.
/// Falls back to the raw entry id when the model or LocString is unavailable.
/// </summary>
internal static class CharacterDisplayHelper
{
	public static string LocalizedName(ModelId? characterId)
	{
		if (characterId == null || characterId == ModelId.none)
		{
			return "?";
		}

		try
		{
			CharacterModel? cm = ModelDb.GetByIdOrNull<CharacterModel>(characterId);
			if (cm == null)
			{
				return characterId.Entry;
			}

			string formatted = cm.Title?.GetFormattedText() ?? string.Empty;
			return string.IsNullOrEmpty(formatted) ? characterId.Entry : formatted;
		}
		catch (System.Exception ex)
		{
			Log.Warn($"NyMod.Saves CharacterDisplayHelper.LocalizedName({characterId.Entry}) failed: {ex.Message}");
			return characterId.Entry;
		}
	}

	/// <summary>
	/// Convenience overload for serialized character ids ("CATEGORY.ENTRY").
	/// Falls back to the raw input on parse failure.
	/// </summary>
	public static string LocalizedName(string serializedId)
	{
		if (string.IsNullOrEmpty(serializedId))
		{
			return "?";
		}

		try
		{
			ModelId parsed = ModelId.Deserialize(serializedId);
			return LocalizedName(parsed);
		}
		catch
		{
			return serializedId;
		}
	}
}
