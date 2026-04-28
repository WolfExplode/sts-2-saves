using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveEdit.Logic;
using NyMod.Saves.Infrastructure.Localization;
using NyMod.Saves.Infrastructure.Ui;

namespace NyMod.Saves.Features.SaveEdit.Presentation;

/// <summary>
/// Picks a player NetId. Three sources, top-to-bottom:
///   1. Steam friends list (default, sorted "in STS2" -> online -> name).
///   2. Async "scan archived runs" button populating partner NetIds seen in any saved MP run.
///   3. Manual SteamID64 entry as last-resort fallback.
/// </summary>
internal sealed partial class PlayerPickerDialog : ConfirmationDialog
{
	private LineEdit _filter = null!;
	private ItemList _friendsList = null!;
	private ItemList _partnersList = null!;
	private Button _scanButton = null!;
	private Button _stopScanButton = null!;
	private Label _scanStatus = null!;
	private LineEdit _manualEntry = null!;
	private TaskCompletionSource<ulong?>? _pendingResult;

	// Snapshots used to re-render the lists when the search filter changes.
	private readonly List<(SteamFriendsProvider.FriendInfo info, string suffix, bool excluded)> _friendsSnapshot = new();
	private readonly List<(string label, ulong netId)> _partnersSnapshot = new();

	private readonly List<ulong> _friendsIds = new List<ulong>();
	private readonly List<ulong> _partnersIds = new List<ulong>();
	private readonly HashSet<ulong> _excludedIds = new HashSet<ulong>();

	private CancellationTokenSource? _scanCts;
	private bool _scanRunning;

	public override void _Ready()
	{
		Title = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerTitle);
		Exclusive = true;
		Unresizable = false;
		MinSize = new Vector2I(560, 540);
		BuildUi();
		Confirmed += OnConfirmed;
		Canceled += OnCanceled;
		CloseRequested += OnCloseRequested;
	}

	public Task<ulong?> ShowAsync(IEnumerable<ulong> excludeNetIds)
	{
		if (_pendingResult != null && !_pendingResult.Task.IsCompleted)
		{
			return _pendingResult.Task;
		}

		_excludedIds.Clear();
		foreach (ulong id in excludeNetIds)
		{
			_excludedIds.Add(id);
		}

		_filter.Text = string.Empty;
		PopulateFriends();
		ResetPartners();
		_manualEntry.Text = string.Empty;

		_pendingResult = new TaskCompletionSource<ulong?>();
		DialogThemeAdopter.AdoptParentTheme(this);
		PopupCentered(MinSize);
		return _pendingResult.Task;
	}

	private void BuildUi()
	{
		GetOkButton().Text = SaveUiText.GetFromTable(SaveUiText.MainMenuTable, "GENERIC_POPUP.confirm");
		GetCancelButton().Text = SaveUiText.GetFromTable(SaveUiText.MainMenuTable, "GENERIC_POPUP.cancel");

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		AddChild(margin);

		VBoxContainer root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 10);
		root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(root);

		// --- Search filter ---
		_filter = new LineEdit
		{
			PlaceholderText = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerSearchPlaceholder),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_filter.TextChanged += _ => ApplyFilter();
		root.AddChild(_filter);

		// --- Steam friends section ---
		Label friendsHeader = new Label { Text = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerSteamHeader) };
		root.AddChild(friendsHeader);

		_friendsList = new ItemList
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 180f),
			AllowReselect = true,
		};
		root.AddChild(_friendsList);

		// --- Partner scan section ---
		Label partnersHeader = new Label { Text = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerPartnersHeader) };
		root.AddChild(partnersHeader);

		HBoxContainer scanRow = new HBoxContainer();
		scanRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(scanRow);

		_scanButton = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerScan) };
		_scanButton.Pressed += OnScanPressed;
		scanRow.AddChild(_scanButton);

		_stopScanButton = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerStopScan), Disabled = true };
		_stopScanButton.Pressed += OnStopScanPressed;
		scanRow.AddChild(_stopScanButton);

		_scanStatus = new Label();
		_scanStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scanRow.AddChild(_scanStatus);

		_partnersList = new ItemList
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 140f),
			AllowReselect = true,
		};
		root.AddChild(_partnersList);

		// --- Manual entry section ---
		Label manualHeader = new Label { Text = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerManualHeader) };
		root.AddChild(manualHeader);

		_manualEntry = new LineEdit
		{
			PlaceholderText = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerManualPlaceholder),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		root.AddChild(_manualEntry);
	}

	private void PopulateFriends()
	{
		_friendsSnapshot.Clear();

		if (!ServiceRegistry.SteamFriendsProvider.TryGetFriends(out IReadOnlyList<SteamFriendsProvider.FriendInfo> friends) || friends.Count == 0)
		{
			RenderFriends(string.Empty);
			return;
		}

		foreach (SteamFriendsProvider.FriendInfo f in friends)
		{
			bool excluded = _excludedIds.Contains(f.SteamId);
			string suffix = excluded
				? "  [" + SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerAlreadyInSave) + "]"
				: f.IsPlayingSts2
					? "  [" + SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerPlayingNow) + "]"
					: "  (" + f.State.ToString().Replace("k_EPersonaState", string.Empty) + ")";
			_friendsSnapshot.Add((f, suffix, excluded));
		}

		RenderFriends(_filter?.Text ?? string.Empty);
	}

	private void RenderFriends(string filter)
	{
		_friendsList.Clear();
		_friendsIds.Clear();

		if (_friendsSnapshot.Count == 0)
		{
			_friendsList.AddItem(SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerSteamUnavailable));
			_friendsList.SetItemDisabled(0, true);
			return;
		}

		string needle = filter?.Trim() ?? string.Empty;
		foreach ((SteamFriendsProvider.FriendInfo info, string suffix, bool excluded) in _friendsSnapshot)
		{
			if (needle.Length > 0 && info.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			int idx = _friendsList.AddItem(info.Name + suffix);
			if (excluded)
			{
				_friendsList.SetItemDisabled(idx, true);
			}
			_friendsIds.Add(info.SteamId);
		}
	}

	private void ApplyFilter()
	{
		string f = _filter?.Text ?? string.Empty;
		RenderFriends(f);
		RenderPartners(f);
	}

	private void RenderPartners(string filter)
	{
		_partnersList.Clear();
		_partnersIds.Clear();

		string needle = filter?.Trim() ?? string.Empty;
		foreach ((string label, ulong netId) in _partnersSnapshot)
		{
			if (needle.Length > 0 && label.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			_partnersIds.Add(netId);
			_partnersList.AddItem(label);
		}
	}

	private void ResetPartners()
	{
		StopScanIfRunning();
		_partnersSnapshot.Clear();
		_partnersList.Clear();
		_partnersIds.Clear();
		_scanStatus.Text = string.Empty;
		_scanButton.Disabled = false;
		_stopScanButton.Disabled = true;
	}

	private async void OnScanPressed()
	{
		if (_scanRunning) return;
		_scanRunning = true;
		_scanButton.Disabled = true;
		_stopScanButton.Disabled = false;
		_scanStatus.Text = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerScanning);
		_scanCts = new CancellationTokenSource();

		try
		{
			await foreach (PartnerNetIdHarvester.PartnerInfo info in ServiceRegistry.PartnerHarvester.EnumeratePartnersAsync(_scanCts.Token))
			{
				if (_excludedIds.Contains(info.NetId))
				{
					continue;
				}

				string label = $"{info.Label}  [{info.NetId}]";
				_partnersSnapshot.Add((label, info.NetId));
				RenderPartners(_filter?.Text ?? string.Empty);
			}
			_scanStatus.Text = SaveUiText.Format(SaveEditUiText.Keys.PlayerPickerScanDone, ("Count", _partnersSnapshot.Count));
		}
		catch (OperationCanceledException)
		{
			_scanStatus.Text = SaveUiText.Get(SaveEditUiText.Keys.PlayerPickerScanCanceled);
		}
		catch (Exception ex)
		{
			_scanStatus.Text = ex.Message;
		}
		finally
		{
			_scanRunning = false;
			_stopScanButton.Disabled = true;
			_scanButton.Disabled = false;
		}
	}

	private void OnStopScanPressed() => StopScanIfRunning();

	private void StopScanIfRunning()
	{
		if (_scanCts != null)
		{
			try { _scanCts.Cancel(); } catch { /* ignore */ }
			_scanCts = null;
		}
	}

	private void OnConfirmed()
	{
		ulong? chosen = null;

		// Manual entry wins if non-empty + parses.
		string manual = _manualEntry.Text?.Trim() ?? string.Empty;
		if (manual.Length > 0 && ulong.TryParse(manual, out ulong manualId) && manualId != 0UL)
		{
			chosen = manualId;
		}
		else
		{
			int[] friendIdx = _friendsList.GetSelectedItems();
			if (friendIdx.Length > 0 && friendIdx[0] >= 0 && friendIdx[0] < _friendsIds.Count
				&& !_friendsList.IsItemDisabled(friendIdx[0]))
			{
				chosen = _friendsIds[friendIdx[0]];
			}
			else
			{
				int[] partnerIdx = _partnersList.GetSelectedItems();
				if (partnerIdx.Length > 0 && partnerIdx[0] >= 0 && partnerIdx[0] < _partnersIds.Count)
				{
					chosen = _partnersIds[partnerIdx[0]];
				}
			}
		}

		if (chosen.HasValue && _excludedIds.Contains(chosen.Value))
		{
			chosen = null;
		}

		Complete(chosen);
	}

	private void OnCanceled() => Complete(null);
	private void OnCloseRequested() { Hide(); Complete(null); }

	private void Complete(ulong? value)
	{
		StopScanIfRunning();
		if (_pendingResult == null || _pendingResult.Task.IsCompleted)
		{
			return;
		}

		_pendingResult.SetResult(value);
	}
}
