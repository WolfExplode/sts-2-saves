using System;
using System.IO;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using NyMod.Saves.Infrastructure.Persistence;

namespace NyMod.Saves.Infrastructure.Configuration;

internal sealed class SaveManagerConfigStore
{
	private readonly SaveArchivePathResolver _pathResolver;

	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public SaveManagerConfigStore(SaveArchivePathResolver pathResolver)
	{
		_pathResolver = pathResolver;
	}

	public SaveManagerConfig LoadOrDefault()
	{
		if (!_pathResolver.TryGetConfigPath(out string configPath) || string.IsNullOrEmpty(configPath))
		{
			return new SaveManagerConfig();
		}
		try
		{
			if (!File.Exists(configPath))
			{
				return new SaveManagerConfig();
			}
			string json = File.ReadAllText(configPath);
			return JsonSerializer.Deserialize<SaveManagerConfig>(json, _jsonOptions) ?? new SaveManagerConfig();
		}
		catch (Exception ex)
		{
			Log.Warn("NyMod.Saves failed to load config: " + ex.Message, 2);
			return new SaveManagerConfig();
		}
	}

	public void Save(SaveManagerConfig config)
	{
		if (!_pathResolver.TryGetConfigPath(out string configPath) || string.IsNullOrEmpty(configPath))
		{
			return;
		}
		try
		{
			string directoryName = Path.GetDirectoryName(configPath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string contents = JsonSerializer.Serialize(config, _jsonOptions);
			File.WriteAllText(configPath, contents);
		}
		catch (Exception ex)
		{
			Log.Warn("NyMod.Saves failed to save config: " + ex.Message, 2);
		}
	}
}
