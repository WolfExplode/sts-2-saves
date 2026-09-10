using System;
using MegaCrit.Sts2.Core.Logging;
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
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		try
		{
			bool isMultiplayer = (int)RunManager.Instance.NetService.Type == 2;
			ArchiveService.CaptureCurrentSnapshot(SaveArchiveKind.Auto, isMultiplayer);
		}
		catch (Exception ex)
		{
			Log.Warn("NyMod.Saves autosave capture failed: " + ex.Message, 2);
		}
	}
}
