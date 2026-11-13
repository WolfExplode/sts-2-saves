using System;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Infrastructure.Configuration;
using NyMod.Saves.Infrastructure.Persistence;

namespace NyMod.Saves.Bootstrap;

internal static class ServiceRegistry
{
	private static bool _initialized;

	public static SaveArchiveService ArchiveService { get; private set; }

	public static void Initialize()
	{
		if (!_initialized)
		{
			SaveArchivePathResolver pathResolver = new SaveArchivePathResolver();
			SaveManagerConfigStore configStore = new SaveManagerConfigStore(pathResolver);
			SaveArchiveSummaryFactory summaryFactory = new SaveArchiveSummaryFactory();
			RunIdentityService runIdentityService = new RunIdentityService();
			SaveArchiveStore archiveStore = new SaveArchiveStore(pathResolver);
			ArchiveService = new SaveArchiveService(configStore, archiveStore, summaryFactory, runIdentityService);
			try
			{
				SaveManager.Instance.Saved += OnGameSaved;
			}
			catch (Exception ex)
			{
				Log.Warn("NyMod.Saves failed to subscribe to SaveManager.Saved: " + ex.Message, 2);
			}
			_initialized = true;
		}
	}

	private static void OnGameSaved()
	{
		try
		{
			if (RunManager.Instance?.NetService?.Type == NetGameType.Singleplayer)
			{
				ArchiveService.CaptureCurrentSnapshot(SaveArchiveKind.Auto);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("NyMod.Saves autosave capture failed: " + ex.Message, 2);
		}
	}
}
