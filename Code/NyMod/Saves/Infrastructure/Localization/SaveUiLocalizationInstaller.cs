using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using LocaleChangeCallback = MegaCrit.Sts2.Core.Localization.LocManager.LocaleChangeCallback;

namespace NyMod.Saves.Infrastructure.Localization;

internal static class SaveUiLocalizationInstaller
{
	[CompilerGenerated]
	private static class _003C_003EO
	{
		public static LocaleChangeCallback _003C0_003E__ApplyCurrentLanguage;
	}

	private static readonly object _gate = new object();

	private static readonly Dictionary<string, Dictionary<string, string>> _cache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

	private static bool _subscribed;

	public static void Install()
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		lock (_gate)
		{
			if (LocManager.Instance == null)
			{
				return;
			}
			if (!_subscribed)
			{
				object obj = _003C_003EO._003C0_003E__ApplyCurrentLanguage;
				if (obj == null)
				{
					LocaleChangeCallback val = ApplyCurrentLanguage;
					_003C_003EO._003C0_003E__ApplyCurrentLanguage = val;
					obj = (object)val;
				}
				LocString.SubscribeToLocaleChange((LocaleChangeCallback)obj);
				_subscribed = true;
			}
			ApplyCurrentLanguage();
		}
	}

	private static void ApplyCurrentLanguage()
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		LocManager instance = LocManager.Instance;
		if (instance == null)
		{
			return;
		}
		try
		{
			Dictionary<string, string> dictionary = LoadLanguage("eng");
			Dictionary<string, string> dictionary2 = (instance.Language.Equals("eng", StringComparison.OrdinalIgnoreCase) ? dictionary : LoadLanguage(instance.Language));
			LocTable val = new LocTable("sts2_saves_ui", new Dictionary<string, string>(dictionary, StringComparer.Ordinal), (LocTable)null);
			LocTable value = (LocTable)(instance.Language.Equals("eng", StringComparison.OrdinalIgnoreCase) ? ((object)val) : ((object)new LocTable("sts2_saves_ui", new Dictionary<string, string>(dictionary2, StringComparer.Ordinal), val)));
			Dictionary<string, LocTable> value2 = Traverse.Create((object)instance).Field("_tables").GetValue<Dictionary<string, LocTable>>();
			value2["sts2_saves_ui"] = value;
		}
		catch (Exception ex)
		{
			Log.Warn("NyMod.Saves failed to install localization table 'sts2_saves_ui': " + ex.Message, 2);
		}
	}

	private static Dictionary<string, string> LoadLanguage(string language)
	{
		if (_cache.TryGetValue(language, out Dictionary<string, string> value))
		{
			return value;
		}
		Assembly assembly = typeof(SaveUiLocalizationInstaller).Assembly;
		string suffix = ".localization." + language + ".sts2_saves_ui.json";
		string text = assembly.GetManifestResourceNames().FirstOrDefault((string name) => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
		if (text == null)
		{
			if (!language.Equals("eng", StringComparison.OrdinalIgnoreCase))
			{
				return LoadLanguage("eng");
			}
			throw new FileNotFoundException("Embedded localization resource not found for language '" + language + "'.");
		}
		using Stream stream = assembly.GetManifestResourceStream(text) ?? throw new FileNotFoundException("Embedded localization resource stream missing: " + text);
		using StreamReader streamReader = new StreamReader(stream);
		string json = streamReader.ReadToEnd();
		Dictionary<string, string> dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
		if (dictionary == null)
		{
			throw new InvalidOperationException("Embedded localization resource '" + text + "' deserialized to null.");
		}
		_cache[language] = dictionary;
		return dictionary;
	}
}
