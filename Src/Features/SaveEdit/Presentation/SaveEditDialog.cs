using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Features.SaveEdit.Logic;
using NyMod.Saves.Infrastructure.Localization;
using NyMod.Saves.Infrastructure.Ui;

namespace NyMod.Saves.Features.SaveEdit.Presentation;

/// <summary>
/// Top-level dialog for editing an archived snapshot. Loads the snapshot via
/// <see cref="SaveEditService"/>, lets the user mutate ascension/players/rewards,
/// then writes the result as a brand-new manual snapshot under the same run.
/// </summary>
internal sealed partial class SaveEditDialog : ConfirmationDialog
{
	private CharacterPickerDialog _characterPicker = null!;
	private PlayerPickerDialog _playerPicker = null!;
	private AutoRewardConfigDialog _autoRewardDialog = null!;

	private SpinBox _ascensionSpin = null!;
	private VBoxContainer _playersList = null!;
	private VBoxContainer _rewardsList = null!;
	private Label _rewardsHeader = null!;
	private Label _statusLabel = null!;

	private SaveArchiveMetadata? _source;
	private SerializableRun? _editedRun;
	private SaveEditPayload? _payload;
	private TaskCompletionSource<bool>? _pendingResult;

	public override void _Ready()
	{
		Title = SaveUiText.Get(SaveEditUiText.Keys.Title);
		Exclusive = true;
		Unresizable = false;
		MinSize = new Vector2I(720, 620);
		BuildUi();
		Confirmed += OnConfirmed;
		Canceled += OnCanceled;
		CloseRequested += OnCloseRequested;
	}

	public Task<bool> ShowAsync(SaveArchiveMetadata snapshot)
	{
		if (_pendingResult != null && !_pendingResult.Task.IsCompleted)
		{
			return _pendingResult.Task;
		}

		_source = snapshot;
		_editedRun = ServiceRegistry.EditService.LoadForEdit(snapshot, out string? error);
		if (_editedRun == null)
		{
			_statusLabel.Text = SaveUiText.Format(SaveEditUiText.Keys.AppliedFailed, ("Error", error ?? "load failed"));
			_pendingResult = new TaskCompletionSource<bool>();
			DialogThemeAdopter.AdoptParentTheme(this);
			PopupCentered(MinSize);
			return _pendingResult.Task;
		}

		_payload = new SaveEditPayload();
		_statusLabel.Text = string.Empty;
		_ascensionSpin.Value = _editedRun.Ascension;
		RebuildPlayersUi();
		RebuildRewardsUi();

		_pendingResult = new TaskCompletionSource<bool>();
		DialogThemeAdopter.AdoptParentTheme(this);
		PopupCentered(MinSize);
		return _pendingResult.Task;
	}

	private void BuildUi()
	{
		GetOkButton().Text = SaveUiText.Get(SaveEditUiText.Keys.Apply);
		GetCancelButton().Text = SaveUiText.Get(SaveEditUiText.Keys.Cancel);

		_characterPicker = new CharacterPickerDialog();
		AddChild(_characterPicker);
		_playerPicker = new PlayerPickerDialog();
		AddChild(_playerPicker);
		_autoRewardDialog = new AutoRewardConfigDialog();
		AddChild(_autoRewardDialog);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		AddChild(margin);

		ScrollContainer scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		margin.AddChild(scroll);

		VBoxContainer root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(root);

		// --- Ascension row ---
		HBoxContainer ascRow = new HBoxContainer();
		ascRow.AddThemeConstantOverride("separation", 12);
		root.AddChild(ascRow);

		Label ascLabel = new Label { Text = SaveUiText.Get(SaveEditUiText.Keys.AscensionLabel) };
		ascRow.AddChild(ascLabel);

		_ascensionSpin = new SpinBox { MinValue = 0, MaxValue = 20, Step = 1 };
		_ascensionSpin.ValueChanged += OnAscensionChanged;
		ascRow.AddChild(_ascensionSpin);

		// --- Players section ---
		Label playersHeader = new Label { Text = SaveUiText.Get(SaveEditUiText.Keys.PlayersHeader) };
		root.AddChild(playersHeader);

		_playersList = new VBoxContainer();
		_playersList.AddThemeConstantOverride("separation", 4);
		root.AddChild(_playersList);

		Button addPlayerBtn = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.AddPlayer) };
		addPlayerBtn.Pressed += OnAddPlayerPressed;
		root.AddChild(addPlayerBtn);

		// --- Rewards section ---
		_rewardsHeader = new Label { Text = SaveUiText.Get(SaveEditUiText.Keys.RewardsHeader) };
		root.AddChild(_rewardsHeader);

		_rewardsList = new VBoxContainer();
		_rewardsList.AddThemeConstantOverride("separation", 4);
		root.AddChild(_rewardsList);

		Button addRewardBtn = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.AddReward) };
		addRewardBtn.Pressed += OnAddRewardPressed;
		root.AddChild(addRewardBtn);

		_statusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		root.AddChild(_statusLabel);
	}

	private void OnAscensionChanged(double value)
	{
		if (_payload == null || _editedRun == null) return;
		int v = (int)value;
		_payload.NewAscension = v == _editedRun.Ascension ? null : v;
	}

	private void RebuildPlayersUi()
	{
		foreach (Node child in _playersList.GetChildren())
		{
			child.QueueFree();
		}

		if (_editedRun == null) return;

		// Apply current pending removals/additions to compute the displayed list.
		HashSet<ulong> removed = _payload != null ? new HashSet<ulong>(_payload.RemovedPlayerNetIds) : new HashSet<ulong>();

		foreach (SerializablePlayer player in _editedRun.Players)
		{
			if (removed.Contains(player.NetId)) continue;
			AddPlayerRow(player.NetId, player.CharacterId, isPending: false, suffix: null);
		}

		if (_payload != null)
		{
			foreach (AddPlayerSpec spec in _payload.AddedPlayers)
			{
				ModelId? charId = spec.CharacterIdOverride
					?? _editedRun.Players.FirstOrDefault(p => p.NetId == spec.SourcePlayerNetId)?.CharacterId;
				AddPlayerRow(spec.NetId, charId, isPending: true, suffix: " (added)");
			}
		}
	}

	private void AddPlayerRow(ulong netId, ModelId? characterId, bool isPending, string? suffix)
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		// SerializablePlayer has no per-player username field, so the SteamID64
		// (NetId) is the only identity persisted in the save itself. Resolve it
		// against the local user / friends cache and fall back to the raw id.
		string username = ServiceRegistry.SteamFriendsProvider.TryResolveName(netId, out string n)
			? n
			: netId.ToString();
		string charName = CharacterDisplayHelper.LocalizedName(characterId);

		Label l = new Label { Text = $"{charName} ({username}){suffix}" };
		l.TooltipText = netId.ToString();
		l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(l);

		Button rm = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.RemovePlayer) };
		rm.Pressed += () => OnRemovePlayer(netId, isPending);
		row.AddChild(rm);

		_playersList.AddChild(row);
	}

	private void OnRemovePlayer(ulong netId, bool wasPending)
	{
		if (_payload == null) return;
		if (wasPending)
		{
			AddPlayerSpec? match = _payload.AddedPlayers.FirstOrDefault(s => s.NetId == netId);
			if (match != null) _payload.AddedPlayers.Remove(match);
		}
		else
		{
			if (!_payload.RemovedPlayerNetIds.Contains(netId))
			{
				_payload.RemovedPlayerNetIds.Add(netId);
			}
		}

		// Removing a player invalidates any reward edits keyed to that NetId.
		var orphaned = _payload.RewardEdits.Where(r => r.PlayerNetId == netId).ToList();
		foreach (var r in orphaned) _payload.RewardEdits.Remove(r);

		RebuildPlayersUi();
		RebuildRewardsUi();
	}

	private async void OnAddPlayerPressed()
	{
		if (_editedRun == null || _payload == null) return;

		// Step 1: pick mode.
		AddPlayerMode? mode = await PickAddPlayerMode();
		if (mode == null) return;

		// Step 2: pick character (FromEmpty) or pick source player (Clone).
		ModelId? characterId = null;
		ulong sourceNetId = 0UL;
		AutoRewardConfig? autoRewards = null;

		if (mode == AddPlayerMode.FromEmpty)
		{
			characterId = await _characterPicker.ShowAsync();
			if (characterId == null) return;

			// Optional auto-reward configuration form.
			autoRewards = await _autoRewardDialog.ShowAsync();
		}
		else
		{
			// Clone needs a source player from the current snapshot.
			sourceNetId = await PickSourcePlayer();
			if (sourceNetId == 0UL) return;
		}

		// Step 3: pick NetId for the new player.
		HashSet<ulong> excluded = new HashSet<ulong>(_editedRun.Players.Select(p => p.NetId));
		foreach (AddPlayerSpec s in _payload.AddedPlayers) excluded.Add(s.NetId);

		ulong? netId = await _playerPicker.ShowAsync(excluded);
		if (netId == null || netId.Value == 0UL || excluded.Contains(netId.Value)) return;

		_payload.AddedPlayers.Add(new AddPlayerSpec
		{
			Mode = mode.Value,
			NetId = netId.Value,
			SourcePlayerNetId = sourceNetId,
			CharacterIdOverride = characterId,
			AutoRewards = autoRewards,
		});

		RebuildPlayersUi();
		RebuildRewardsUi();
	}

	private Task<AddPlayerMode?> PickAddPlayerMode()
	{
		TaskCompletionSource<AddPlayerMode?> tcs = new TaskCompletionSource<AddPlayerMode?>();
		AcceptDialog dlg = new AcceptDialog
		{
			Title = SaveUiText.Get(SaveEditUiText.Keys.AddPlayerModeLabel),
			Exclusive = true,
		};
		dlg.GetOkButton().Visible = false;
		AddChild(dlg);

		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		dlg.AddChild(box);

		void Make(string textKey, AddPlayerMode mode)
		{
			Button b = new Button { Text = SaveUiText.Get(textKey) };
			b.Pressed += () =>
			{
				dlg.Hide();
				if (!tcs.Task.IsCompleted) tcs.SetResult(mode);
			};
			box.AddChild(b);
		}

		Make(SaveEditUiText.Keys.AddPlayerModeClone, AddPlayerMode.Clone);
		Make(SaveEditUiText.Keys.AddPlayerModeFromEmpty, AddPlayerMode.FromEmpty);

		dlg.Canceled += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(null); };
		dlg.CloseRequested += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(null); };
		dlg.PopupCentered(new Vector2I(360, 220));
		return tcs.Task;
	}

	private Task<ulong> PickSourcePlayer()
	{
		TaskCompletionSource<ulong> tcs = new TaskCompletionSource<ulong>();
		if (_editedRun == null || _editedRun.Players.Count == 0)
		{
			tcs.SetResult(0UL);
			return tcs.Task;
		}

		AcceptDialog dlg = new AcceptDialog
		{
			Title = SaveUiText.Get(SaveEditUiText.Keys.AddPlayerSourceLabel),
			Exclusive = true,
		};
		dlg.GetOkButton().Visible = false;
		AddChild(dlg);

		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		dlg.AddChild(box);

		foreach (SerializablePlayer p in _editedRun.Players)
		{
			ulong netId = p.NetId;
			string username = ServiceRegistry.SteamFriendsProvider.TryResolveName(netId, out string n) ? n : netId.ToString();
			string charName = CharacterDisplayHelper.LocalizedName(p.CharacterId);
			Button b = new Button { Text = $"{charName} ({username})", TooltipText = netId.ToString() };
			b.Pressed += () =>
			{
				dlg.Hide();
				if (!tcs.Task.IsCompleted) tcs.SetResult(netId);
			};
			box.AddChild(b);
		}

		dlg.Canceled += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(0UL); };
		dlg.CloseRequested += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(0UL); };
		dlg.PopupCentered(new Vector2I(420, 320));
		return tcs.Task;
	}

	// ===== Rewards =====

	private void RebuildRewardsUi()
	{
		foreach (Node child in _rewardsList.GetChildren())
		{
			child.QueueFree();
		}

		if (_editedRun == null || _payload == null)
		{
			_rewardsHeader.Text = SaveUiText.Get(SaveEditUiText.Keys.RewardsHeader);
			return;
		}

		if (_editedRun.PreFinishedRoom == null)
		{
			_rewardsHeader.Text = SaveUiText.Get(SaveEditUiText.Keys.RewardsHeaderEmpty);
			return;
		}

		_rewardsHeader.Text = SaveUiText.Get(SaveEditUiText.Keys.RewardsHeader);

		// Compose the displayed reward list = original ExtraRewards minus deletes plus adds.
		Dictionary<ulong, List<SerializableReward>> rewards = _editedRun.PreFinishedRoom.ExtraRewards
			?? new Dictionary<ulong, List<SerializableReward>>();

		HashSet<ulong> removedPlayers = new HashSet<ulong>(_payload.RemovedPlayerNetIds);

		foreach (var kv in rewards)
		{
			if (removedPlayers.Contains(kv.Key)) continue;
			SerializablePlayer? owner = _editedRun.Players.FirstOrDefault(p => p.NetId == kv.Key);
			string ownerUsername = ServiceRegistry.SteamFriendsProvider.TryResolveName(kv.Key, out string ownerN) ? ownerN : kv.Key.ToString();
			string ownerChar = CharacterDisplayHelper.LocalizedName(owner?.CharacterId);
			Label header = new Label { Text = $"-- {ownerChar} ({ownerUsername}) --", TooltipText = kv.Key.ToString() };
			_rewardsList.AddChild(header);

			for (int i = 0; i < kv.Value.Count; i++)
			{
				int rewardIndex = i;
				ulong owningId = kv.Key;
				SerializableReward existing = kv.Value[i];
				bool deletedPending = _payload.RewardEdits.Any(e => e.Action == RewardEditAction.Delete && e.PlayerNetId == owningId && e.RewardIndex == rewardIndex);
				if (deletedPending) continue;

				HBoxContainer row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 6);
				Label l = new Label { Text = DescribeReward(existing) };
				l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				row.AddChild(l);
				Button rm = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.RemoveReward) };
				rm.Pressed += () =>
				{
					_payload.RewardEdits.Add(new RewardEdit { Action = RewardEditAction.Delete, PlayerNetId = owningId, RewardIndex = rewardIndex });
					RebuildRewardsUi();
				};
				row.AddChild(rm);
				_rewardsList.AddChild(row);
			}
		}

		// Pending Add edits.
		foreach (RewardEdit e in _payload.RewardEdits.Where(r => r.Action == RewardEditAction.Add))
		{
			HBoxContainer row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			SerializablePlayer? owner = _editedRun.Players.FirstOrDefault(p => p.NetId == e.PlayerNetId);
			string addOwnerUsername = ServiceRegistry.SteamFriendsProvider.TryResolveName(e.PlayerNetId, out string addOwnerN) ? addOwnerN : e.PlayerNetId.ToString();
			string addOwnerChar = CharacterDisplayHelper.LocalizedName(owner?.CharacterId);
			Label l = new Label { Text = $"+ {addOwnerChar} ({addOwnerUsername})  {e.RewardType} {(e.RewardType == RewardType.Gold ? e.GoldAmount.ToString() : string.Empty)}", TooltipText = e.PlayerNetId.ToString() };
			l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			row.AddChild(l);
			Button rm = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.RemoveReward) };
			RewardEdit captured = e;
			rm.Pressed += () => { _payload.RewardEdits.Remove(captured); RebuildRewardsUi(); };
			row.AddChild(rm);
			_rewardsList.AddChild(row);
		}
	}

	private static string DescribeReward(SerializableReward r)
	{
		return r.RewardType switch
		{
			RewardType.Card => $"Card x{r.OptionCount} {r.RarityOdds}",
			RewardType.Gold => $"Gold {r.GoldAmount}",
			RewardType.Relic => $"Relic {(r.PredeterminedModelId != null && r.PredeterminedModelId != ModelId.none ? r.PredeterminedModelId.Entry : "(auto)")}",
			RewardType.Potion => $"Potion {(r.PredeterminedModelId != null && r.PredeterminedModelId != ModelId.none ? r.PredeterminedModelId.Entry : "(auto)")}",
			RewardType.RemoveCard => "RemoveCard",
			_ => r.RewardType.ToString(),
		};
	}

	private async void OnAddRewardPressed()
	{
		if (_editedRun == null || _payload == null) return;
		if (_editedRun.PreFinishedRoom == null)
		{
			_statusLabel.Text = SaveUiText.Get(SaveEditUiText.Keys.RewardsHeaderEmpty);
			return;
		}

		ulong owner = await PickSourcePlayer();
		if (owner == 0UL) return;

		(RewardType type, int gold, int optionCount)? choice = await PickRewardType();
		if (choice == null) return;

		_payload.RewardEdits.Add(new RewardEdit
		{
			Action = RewardEditAction.Add,
			PlayerNetId = owner,
			RewardType = choice.Value.type,
			GoldAmount = choice.Value.gold,
			OptionCount = choice.Value.optionCount,
		});
		RebuildRewardsUi();
	}

	private Task<(RewardType, int, int)?> PickRewardType()
	{
		TaskCompletionSource<(RewardType, int, int)?> tcs = new TaskCompletionSource<(RewardType, int, int)?>();
		AcceptDialog dlg = new AcceptDialog
		{
			Title = SaveUiText.Get(SaveEditUiText.Keys.AddReward),
			Exclusive = true,
		};
		dlg.GetOkButton().Visible = false;
		AddChild(dlg);

		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		dlg.AddChild(box);

		void Quick(string text, RewardType type, int gold = 0, int optionCount = 3)
		{
			Button b = new Button { Text = text };
			b.Pressed += () =>
			{
				dlg.Hide();
				if (!tcs.Task.IsCompleted) tcs.SetResult((type, gold, optionCount));
			};
			box.AddChild(b);
		}

		Quick("Card x3 (auto)", RewardType.Card, 0, 3);
		Quick("Gold 50", RewardType.Gold, 50, 0);
		Quick("Gold 100", RewardType.Gold, 100, 0);
		Quick("Relic (auto)", RewardType.Relic);
		Quick("Potion (auto)", RewardType.Potion);
		Quick("Remove Card", RewardType.RemoveCard);

		dlg.Canceled += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(null); };
		dlg.CloseRequested += () => { if (!tcs.Task.IsCompleted) tcs.SetResult(null); };
		dlg.PopupCentered(new Vector2I(320, 320));
		return tcs.Task;
	}

	// ===== Apply =====

	private void OnConfirmed()
	{
		if (_source == null || _editedRun == null || _payload == null)
		{
			Complete(false);
			return;
		}

		try
		{
			SaveArchiveMetadata? newMeta = ServiceRegistry.EditService.ApplyEdits(_source, _payload);
			if (newMeta == null)
			{
				_statusLabel.Text = SaveUiText.Format(SaveEditUiText.Keys.AppliedFailed, ("Error", "see log"));
				Complete(false);
				return;
			}

			_statusLabel.Text = SaveUiText.Format(SaveEditUiText.Keys.AppliedSuccess, ("SaveId", newMeta.SaveId));
			Complete(true);
		}
		catch (System.Exception ex)
		{
			Log.Warn($"NyMod.Saves SaveEditDialog: ApplyEdits failed: {ex.Message}");
			_statusLabel.Text = SaveUiText.Format(SaveEditUiText.Keys.AppliedFailed, ("Error", ex.Message));
			Complete(false);
		}
	}

	private void OnCanceled() => Complete(false);
	private void OnCloseRequested() { Hide(); Complete(false); }

	private void Complete(bool result)
	{
		if (_pendingResult == null || _pendingResult.Task.IsCompleted) return;
		_pendingResult.SetResult(result);
	}
}
