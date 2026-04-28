using System;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Features.SaveEdit.Logic;
using NyMod.Saves.Infrastructure.Configuration;
using NyMod.Saves.Infrastructure.Persistence;

namespace NyMod.Saves.Bootstrap;

internal static class ServiceRegistry
{
	private static bool _initialized;

	public static SaveArchiveService ArchiveService { get; private set; } = null!;
	public static SaveEditService EditService { get; private set; } = null!;
	public static PartnerNetIdHarvester PartnerHarvester { get; private set; } = null!;
	public static SteamFriendsProvider SteamFriendsProvider { get; private set; } = null!;

	public static void Initialize()
	{
		if (_initialized)
		{
			return;
		}

		var pathResolver = new SaveArchivePathResolver();
		var configStore = new SaveManagerConfigStore(pathResolver);
		var summaryFactory = new SaveArchiveSummaryFactory();
		var runIdentityService = new RunIdentityService();
		var archiveStore = new SaveArchiveStore(pathResolver);
		ArchiveService = new SaveArchiveService(configStore, archiveStore, summaryFactory, runIdentityService);
		EditService = new SaveEditService(ArchiveService);
		PartnerHarvester = new PartnerNetIdHarvester(ArchiveService);
		SteamFriendsProvider = new SteamFriendsProvider();

		try
		{
			SaveManager.Instance.Saved += OnGameSaved;
			// Profile selection finishes asynchronously after the title screen.
			// Hook ProfileIdChanged so we know the active save paths are resolvable.
			SaveManager.Instance.ProfileIdChanged += OnProfileIdChanged;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves failed to subscribe to SaveManager events: {ex.Message}");
		}

		// Best-effort early scan in case a profile is already initialized at mod load
		// (very rare, but cheap and idempotent). CurrentProfileId throws when no
		// profile is loaded yet, so probing it tells us whether to attempt the scan.
		try
		{
			_ = SaveManager.Instance.CurrentProfileId;
			ArchiveService.TryAutoCaptureExistingSavesOnDisk();
		}
		catch
		{
			// Profile not initialized yet; ProfileIdChanged will fire later.
		}

		_initialized = true;
	}

	private static void OnProfileIdChanged(int profileId)
	{
		Log.Info($"NyMod.Saves OnProfileIdChanged fired (profileId={profileId})");
		try
		{
			ArchiveService.TryAutoCaptureExistingSavesOnDisk();
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves on-profile-change auto-capture failed: {ex.Message}");
		}
	}

	private static void OnGameSaved()
	{
		try
		{
			bool isMultiplayer = RunManager.Instance.NetService.Type == NetGameType.Host;
			ArchiveService.CaptureCurrentSnapshot(SaveArchiveKind.Auto, isMultiplayer);
			// Cheap idempotent rescan: if the OPPOSITE-mode active save exists on disk
			// but isn't archived (e.g. mid-run install of the other mode), grab it now.
			ArchiveService.TryAutoCaptureExistingSavesOnDisk();
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves autosave capture failed: {ex.Message}");
		}
	}
}