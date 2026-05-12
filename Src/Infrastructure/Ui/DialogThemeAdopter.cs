using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;

namespace NyMod.Saves.Infrastructure.Ui;

/// <summary>
/// Bakes a working default font into popup <see cref="Window"/>s (Confirmation
/// dialogs etc.) so Labels/Buttons/LineEdits inside them render with the
/// game's MSDF font instead of Godot's bitmap fallback.
///
/// Why: STS2's project.godot disables dynamic-font oversampling
/// (<c>fonts/dynamic_fonts/use_oversampling=false</c>). The game keeps its own
/// UI crisp by attaching MSDF fonts (Kreon for Latin, locale-specific CJK
/// fonts for jpn/zhs/kor/etc.) to every game-styled control. Plain Godot
/// controls inside our popups never get one, so they fall back to the engine
/// bitmap font which blurs when the main viewport upscales.
///
/// Strategy:
///   1. If the popup already has a Theme assigned, leave it alone.
///   2. Try to copy an explicit Theme from an ancestor Control.
///   3. Otherwise build a fresh Theme whose default font is:
///        - the locale-substitute font from <see cref="FontManager"/> when
///          the current language needs substitution (CJK etc.), or
///        - the game's Kreon-Regular MSDF resource for Latin locales.
/// </summary>
internal static class DialogThemeAdopter
{
	private const string KreonRegularPath = "res://themes/kreon_regular_shared.tres";
	private static Font? _cachedKreon;
	private static bool _kreonProbed;
	private static readonly Dictionary<string, Theme> _themesByLanguage = new();

	public static void AdoptParentTheme(Window window)
	{
		if (window == null || window.Theme != null)
		{
			return;
		}

		for (Node? n = window.GetParent(); n != null; n = n.GetParent())
		{
			if (n is Control c && c.Theme != null)
			{
				window.Theme = c.Theme;
				return;
			}
		}

		Theme? theme = BuildOrGetTheme();
		if (theme != null)
		{
			window.Theme = theme;
		}
	}

	private static Theme? BuildOrGetTheme()
	{
		string language = LocManager.Instance?.Language ?? "eng";
		if (_themesByLanguage.TryGetValue(language, out Theme? cached))
		{
			return cached;
		}

		Font? font = ResolveFontForLanguage(language);
		if (font == null)
		{
			return null;
		}

		Theme theme = new Theme
		{
			DefaultFont = font,
			DefaultFontSize = 16,
		};
		_themesByLanguage[language] = theme;
		return theme;
	}

	private static Font? ResolveFontForLanguage(string language)
	{
		// Always prefer Kreon (the game's Latin UI font) as the primary so
		// Latin/ASCII text matches the rest of the game UI. For locales where
		// Kreon can't render the glyphs (CJK etc.) chain the locale-specific
		// substitute font as a fallback so those code points still render.
		Font? kreon = LoadKreon();
		Font? localeFallback = null;
		try
		{
			if (FontManager.NeedsFontSubstitution(language))
			{
				localeFallback = FontManager.GetSubstituteFont(language, FontType.Regular);
			}
		}
		catch (System.Exception ex)
		{
			Log.Warn($"[DialogThemeAdopter] FontManager substitute lookup failed for '{language}': {ex.Message}");
		}

		if (kreon == null)
		{
			return localeFallback;
		}

		if (localeFallback == null)
		{
			return kreon;
		}

		// Wrap Kreon in a FontVariation so we can attach fallbacks without
		// mutating the shared Kreon resource itself (other game UI uses it
		// directly and would otherwise inherit our fallback chain).
		FontVariation variation = new FontVariation
		{
			BaseFont = kreon,
		};
		variation.SetFallbacks(new Godot.Collections.Array<Font> { localeFallback });
		return variation;
	}

	private static Font? LoadKreon()
	{
		if (_kreonProbed)
		{
			return _cachedKreon;
		}
		_kreonProbed = true;

		try
		{
			if (ResourceLoader.Exists(KreonRegularPath))
			{
				_cachedKreon = ResourceLoader.Load<Font>(KreonRegularPath);
			}
			else
			{
				Log.Warn($"[DialogThemeAdopter] {KreonRegularPath} not found; popup fonts will stay default");
			}
		}
		catch (System.Exception ex)
		{
			Log.Warn($"[DialogThemeAdopter] failed to load {KreonRegularPath}: {ex.Message}");
		}

		return _cachedKreon;
	}
}
