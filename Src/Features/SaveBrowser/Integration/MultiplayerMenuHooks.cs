using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveBrowser.Logic;

namespace NyMod.Saves.Features.SaveBrowser.Integration;

[HarmonyPatch(typeof(NMultiplayerSubmenu))]
internal static class MultiplayerMenuHooks
{
	private static readonly Callable _hijackCallable = Callable.From<NButton>(static _ => OpenBrowser());

	[HarmonyPatch("StartLoad")]
	[HarmonyPrefix]
	private static bool StartLoadPrefix(NMultiplayerSubmenu __instance)
	{
		OpenBrowser();
		return false;
	}

	[HarmonyPatch("UpdateButtons")]
	[HarmonyPostfix]
	private static void UpdateButtonsPostfix(NMultiplayerSubmenu __instance)
	{
		bool hasArchivedRuns = ServiceRegistry.ArchiveService.ListRuns(isMultiplayer: true).Count > 0;
		Traverse traverse = Traverse.Create(__instance);
		NSubmenuButton hostButton = traverse.Field("_hostButton").GetValue<NSubmenuButton>();
		NSubmenuButton loadButton = traverse.Field("_loadButton").GetValue<NSubmenuButton>();
		NSubmenuButton abandonButton = traverse.Field("_abandonButton").GetValue<NSubmenuButton>();
		hostButton.Visible = true;
		loadButton.Visible = hasArchivedRuns;
		abandonButton.Visible = false;

		// Other mods (e.g. RemoveMultiplayerPlayerLimit) rewire the load button's
		// Released signal directly without using Harmony, often AFTER our _Ready
		// postfix runs. Strip everything and reinstall ours each pass.
		HijackLoadButton(loadButton);
	}

	[HarmonyPatch("OnSubmenuShown")]
	[HarmonyPostfix]
	private static void OnSubmenuShownPostfix(NMultiplayerSubmenu __instance)
	{
		// Re-hijack right before the user can click. By the time the submenu is
		// shown, every other mod's deferred init has already run.
		try
		{
			NSubmenuButton loadButton = Traverse.Create(__instance).Field("_loadButton").GetValue<NSubmenuButton>();
			if (loadButton == null)
			{
				return;
			}

			HijackLoadButton(loadButton);

			// RemoveMultiplayerPlayerLimit's HostBootstrapNode patches the button
			// from its own _Process tick (which only runs every other frame, and
			// only after the submenu instance is discoverable in the scene tree).
			// That means our OnSubmenuShown re-hijack typically wins the first
			// race but RMP overwrites it 1-2 frames later. RMP guards with a
			// HashSet of patched submenu instance ids, so it only patches each
			// submenu ONCE — meaning if we re-strip after that single patch,
			// RMP won't repatch.
			//
			// Fire several delayed re-strips covering the window in which RMP is
			// likely to act. We can't subclass NMultiplayerSubmenu to add a
			// _Process hook (Harmony can't patch a non-existent override), so
			// SceneTreeTimer is the cleanest option that doesn't require ticking
			// a custom Node.
			ScheduleDelayedHijack(__instance, loadButton, 0.05);
			ScheduleDelayedHijack(__instance, loadButton, 0.15);
			ScheduleDelayedHijack(__instance, loadButton, 0.4);
			ScheduleDelayedHijack(__instance, loadButton, 1.0);
		}
		catch (System.Exception ex)
		{
			Log.Warn($"NyMod.Saves OnSubmenuShownPostfix re-hijack failed: {ex.Message}");
		}
	}

	private static void ScheduleDelayedHijack(NMultiplayerSubmenu submenu, NSubmenuButton loadButton, double delaySeconds)
	{
		try
		{
			SceneTreeTimer timer = submenu.GetTree().CreateTimer(delaySeconds);
			timer.Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(loadButton))
				{
					HijackLoadButton(loadButton);
				}
			};
		}
		catch (System.Exception ex)
		{
			Log.Warn($"NyMod.Saves ScheduleDelayedHijack({delaySeconds}s) failed: {ex.Message}");
		}
	}

	private static void HijackLoadButton(NSubmenuButton loadButton)
	{
		try
		{
			StringName released = NClickableControl.SignalName.Released;
			bool ourConnected = loadButton.IsConnected(released, _hijackCallable);
			Array<Dictionary> connections = loadButton.GetSignalConnectionList(released);

			// Count and strip only "real" foreign connections. GetSignalConnectionList
			// also returns Godot's internal C#-event binding for the [Signal]; that
			// callable is not removable via Disconnect (logs a native ERROR) and
			// must not be counted as foreign either.
			int strippedReal = 0;
			foreach (Dictionary entry in connections)
			{
				Callable existing = entry["callable"].As<Callable>();
				if (!loadButton.IsConnected(released, existing))
				{
					continue; // Internal/phantom binding — skip silently.
				}
				if (ourConnected && existing.Equals(_hijackCallable))
				{
					continue; // Don't strip ourselves.
				}
				try
				{
					loadButton.Disconnect(released, existing);
					strippedReal++;
				}
				catch
				{
					// Swallow per-disconnect failures; we still try the rest.
				}
			}

			if (!ourConnected)
			{
				loadButton.Connect(released, _hijackCallable, 0U);
			}

			if (strippedReal > 0)
			{
				Log.Info($"NyMod.Saves stripped {strippedReal} foreign Released handler(s) from MP load button");
			}
		}
		catch (System.Exception ex)
		{
			Log.Warn($"NyMod.Saves HijackLoadButton failed: {ex.Message}");
		}
	}

	private static void OpenBrowser()
	{
		NMainMenu? mainMenu = NGame.Instance?.MainMenu;
		if (mainMenu != null)
		{
			SaveBrowserCoordinator.OpenForMainMenu(mainMenu, isMultiplayer: true);
		}
	}
}