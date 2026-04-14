using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using NyMod.Saves.Bootstrap;
using NyMod.Saves.Features.SaveArchive.Logic;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Features.SaveBrowser.Logic;
using NyMod.Saves.Infrastructure.Localization;

namespace NyMod.Saves.Features.SaveBrowser.Presentation;

internal sealed partial class SaveBrowserScreen : NSubmenu
{
	private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";

	private readonly Dictionary<string, SaveArchiveMetadata> _snapshotsByKey = new Dictionary<string, SaveArchiveMetadata>(StringComparer.Ordinal);
	private readonly Dictionary<string, RunArchiveRecord> _runsByKey = new Dictionary<string, RunArchiveRecord>(StringComparer.Ordinal);
	private readonly Dictionary<string, RunArchiveRecord> _runsById = new Dictionary<string, RunArchiveRecord>(StringComparer.Ordinal);

	private SaveBrowserRequest _request;
	private Tree _tree = null!;
	private RichTextLabel _details = null!;
	private Label _title = null!;
	private Button _loadButton = null!;
	private Button _backupButton = null!;
	private Button _openFolderButton = null!;
	private Button _editNoteButton = null!;
	private Button _deleteSaveButton = null!;
	private Button _deleteRunButton = null!;
	private Button _abandonRunButton = null!;
	private SaveNoteEditDialog _noteDialog = null!;
	private string? _selectedKey;

	protected override Control? InitialFocusedControl => _tree;

	public static SaveBrowserScreen Create()
	{
		return new SaveBrowserScreen();
	}

	public void Configure(SaveBrowserRequest request)
	{
		_request = request;
		if (IsNodeReady())
		{
			Refresh();
		}
	}

	public override void _Ready()
	{
		TopLevel = true;
		BuildUi();
		ConnectSignals();
		ConnectViewportSignals();
		ApplyViewportLayout();
		Refresh();
	}

	protected override void ConnectSignals()
	{
		base.ConnectSignals();
		_tree.Connect(Tree.SignalName.ItemSelected, Callable.From(OnItemSelected));
		_loadButton.Connect(BaseButton.SignalName.Pressed, Callable.From(OnLoadPressed));
		_backupButton.Connect(BaseButton.SignalName.Pressed, Callable.From(OnBackupPressed));
		_openFolderButton.Connect(BaseButton.SignalName.Pressed, Callable.From(OnOpenFolderPressed));
		_editNoteButton.Connect(BaseButton.SignalName.Pressed, Callable.From(OnEditNotePressed));
		_deleteSaveButton.Connect(BaseButton.SignalName.Pressed, Callable.From(OnDeleteSavePressed));
		_deleteRunButton.Connect(BaseButton.SignalName.Pressed, Callable.From(OnDeleteRunPressed));
		_abandonRunButton.Connect(BaseButton.SignalName.Pressed, Callable.From(OnAbandonRunPressed));
	}

	public override void OnSubmenuOpened()
	{
		ApplyViewportLayout();
		Refresh();
	}

	private void ConnectViewportSignals()
	{
		Viewport? viewport = GetViewport();
		if (viewport != null)
		{
			viewport.Connect(Viewport.SignalName.SizeChanged, Callable.From(ApplyViewportLayout));
		}
	}

	private void ApplyViewportLayout()
	{
		SetAnchorsPreset(LayoutPreset.TopLeft);
		Position = Vector2.Zero;
		Size = GetViewportRect().Size;
	}

	private void BuildUi()
	{
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Stop;

		ColorRect backdrop = new ColorRect
		{
			Color = new Color(0.03f, 0.03f, 0.04f, 0.95f)
		};
		backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);

		MarginContainer margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 56);
		margin.AddThemeConstantOverride("margin_top", 56);
		margin.AddThemeConstantOverride("margin_right", 56);
		margin.AddThemeConstantOverride("margin_bottom", 48);
		AddChild(margin);

		VBoxContainer root = new VBoxContainer();
		root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		root.AddThemeConstantOverride("separation", 16);
		margin.AddChild(root);

		_title = new Label();
		_title.AddThemeFontSizeOverride("font_size", 28);
		root.AddChild(_title);

		HBoxContainer content = new HBoxContainer();
		content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		content.AddThemeConstantOverride("separation", 20);
		root.AddChild(content);

		MarginContainer treeColumn = new MarginContainer();
		treeColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		treeColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		treeColumn.CustomMinimumSize = new Vector2(420f, 0f);
		content.AddChild(treeColumn);

		_tree = new Tree();
		_tree.Columns = 1;
		_tree.HideRoot = true;
		_tree.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_tree.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_tree.AddThemeColorOverride("font_color", Colors.White);
		_tree.AddThemeColorOverride("font_hovered_color", Colors.White);
		_tree.AddThemeColorOverride("font_selected_color", Colors.White);
		_tree.AddThemeColorOverride("guide_color", new Color(0.55f, 0.55f, 0.6f, 0.8f));
		treeColumn.AddChild(_tree);

		VBoxContainer detailColumn = new VBoxContainer();
		detailColumn.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		detailColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		detailColumn.CustomMinimumSize = new Vector2(360f, 0f);
		detailColumn.AddThemeConstantOverride("separation", 12);
		content.AddChild(detailColumn);

		_details = new RichTextLabel();
		_details.BbcodeEnabled = true;
		_details.FitContent = false;
		_details.ScrollActive = true;
		_details.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_details.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_details.AddThemeColorOverride("default_color", Colors.White);
		detailColumn.AddChild(_details);

		GridContainer actions = new GridContainer();
		actions.Columns = 2;
		actions.AddThemeConstantOverride("h_separation", 8);
		actions.AddThemeConstantOverride("v_separation", 8);
		detailColumn.AddChild(actions);

		_loadButton = CreateActionButton(SaveUiText.Get(SaveUiText.Keys.SaveBrowser.LoadSnapshot));
		_backupButton = CreateActionButton(SaveUiText.Get(SaveUiText.Keys.SaveBrowser.Backup));
		_openFolderButton = CreateActionButton(SaveUiText.Get(SaveUiText.Keys.SaveBrowser.OpenFolder));
		_editNoteButton = CreateActionButton(SaveUiText.Get(SaveUiText.Keys.SaveBrowser.EditNote));
		_deleteSaveButton = CreateActionButton(SaveUiText.Get(SaveUiText.Keys.SaveBrowser.DeleteSave));
		_deleteRunButton = CreateActionButton(SaveUiText.Get(SaveUiText.Keys.SaveBrowser.DeleteRun));
		_abandonRunButton = CreateActionButton(SaveUiText.GetFromTable(SaveUiText.MainMenuTable, "ABANDON_RUN"));

		actions.AddChild(_loadButton);
		actions.AddChild(_backupButton);
		actions.AddChild(_openFolderButton);
		actions.AddChild(_editNoteButton);
		actions.AddChild(_deleteSaveButton);
		actions.AddChild(_deleteRunButton);
		actions.AddChild(_abandonRunButton);

		_noteDialog = new SaveNoteEditDialog();
		AddChild(_noteDialog);

		PackedScene backButtonScene = PreloadManager.Cache.GetScene(BackButtonScenePath);
		NBackButton backButton = backButtonScene.Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
		backButton.Name = "BackButton";
		AddChild(backButton);
	}

	private static Button CreateActionButton(string text)
	{
		return new Button
		{
			Text = text,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
	}

	private void Refresh(string? preferredKey = null)
	{
		_snapshotsByKey.Clear();
		_runsByKey.Clear();
		_runsById.Clear();
		_title.Text = SaveUiText.Get(_request.IsMultiplayer
			? SaveUiText.Keys.SaveBrowser.TitleMultiplayer
			: SaveUiText.Keys.SaveBrowser.TitleSingleplayer);
		_tree.Clear();
		TreeItem root = _tree.CreateItem();
		TreeItem? firstSnapshotItem = null;
		TreeItem? preferredItem = null;
		foreach (RunArchiveRecord run in ServiceRegistry.ArchiveService.ListRuns(_request.IsMultiplayer))
		{
			string runKey = $"run:{run.RunId}";
			_runsByKey[runKey] = run;
			_runsById[run.RunId] = run;
			TreeItem runItem = _tree.CreateItem(root);
			runItem.SetText(0, BuildRunLabel(run));
			runItem.SetMetadata(0, runKey);
			runItem.Collapsed = false;
			if (string.Equals(runKey, preferredKey, StringComparison.Ordinal))
			{
				preferredItem = runItem;
			}

			AddSaveGroup(runItem, run.RunId, SaveArchiveKind.Auto, SaveUiText.Keys.SaveBrowser.GroupAuto, preferredKey, ref firstSnapshotItem, ref preferredItem);
			AddSaveGroup(runItem, run.RunId, SaveArchiveKind.Manual, SaveUiText.Keys.SaveBrowser.GroupManual, preferredKey, ref firstSnapshotItem, ref preferredItem);
		}

		TreeItem? itemToSelect = preferredItem ?? firstSnapshotItem;
		if (itemToSelect == null)
		{
			_selectedKey = null;
			_details.Text = SaveUiText.Get(SaveUiText.Keys.SaveBrowser.Empty);
			UpdateActionButtons();
			return;
		}

		itemToSelect.Select(0);
		_selectedKey = itemToSelect.GetMetadata(0).AsString();
		UpdateDetailsForSelection();
		UpdateActionButtons();
	}

	private void AddSaveGroup(TreeItem runItem, string runId, SaveArchiveKind kind, string labelKey, string? preferredKey, ref TreeItem? firstSnapshotItem, ref TreeItem? preferredItem)
	{
		IReadOnlyList<SaveArchiveMetadata> saves = ServiceRegistry.ArchiveService.ListSnapshots(_request.IsMultiplayer, runId)
			.Where(save => save.Kind == kind)
			.ToList();
		TreeItem groupItem = _tree.CreateItem(runItem);
		groupItem.SetText(0, SaveUiText.Format(labelKey, ("Count", saves.Count)));
		groupItem.SetSelectable(0, false);
		groupItem.Collapsed = false;
		foreach (SaveArchiveMetadata save in saves)
		{
			string key = $"save:{save.RunId}:{save.Kind}:{save.SaveId}";
			_snapshotsByKey[key] = save;
			TreeItem saveItem = _tree.CreateItem(groupItem);
			saveItem.SetText(0, BuildSaveLabel(save));
			saveItem.SetMetadata(0, key);
			firstSnapshotItem ??= saveItem;
			if (string.Equals(key, preferredKey, StringComparison.Ordinal))
			{
				preferredItem = saveItem;
			}
		}
	}

	private void OnItemSelected()
	{
		TreeItem? selected = _tree.GetSelected();
		_selectedKey = selected?.GetMetadata(0).AsString();
		UpdateDetailsForSelection();
		UpdateActionButtons();
	}

	private void UpdateDetailsForSelection()
	{
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata? save))
		{
			string? runNote = _runsById.TryGetValue(save.RunId, out RunArchiveRecord? run) ? run.Note : ServiceRegistry.ArchiveService.GetRunNote(_request.IsMultiplayer, save.RunId);
			_details.Text = BuildSnapshotDetails(save, runNote);
			return;
		}

		if (_selectedKey != null && _runsByKey.TryGetValue(_selectedKey, out RunArchiveRecord? selectedRun))
		{
			_details.Text = BuildRunDetails(selectedRun);
			return;
		}

		_details.Text = SaveUiText.Get(SaveUiText.Keys.SaveBrowser.SelectHint);
	}

	private void UpdateActionButtons()
	{
		bool hasSnapshot = _selectedKey != null && _snapshotsByKey.ContainsKey(_selectedKey);
		bool hasRun = _selectedKey != null && (_runsByKey.ContainsKey(_selectedKey) || hasSnapshot);
		_loadButton.Disabled = !hasSnapshot;
		_deleteSaveButton.Disabled = !hasSnapshot;
		_backupButton.Disabled = !hasRun;
		_openFolderButton.Disabled = !hasRun;
		_editNoteButton.Disabled = !hasRun;
		_deleteRunButton.Disabled = !hasRun;
		_abandonRunButton.Disabled = !hasRun;
	}

	private async void OnLoadPressed()
	{
		if (_selectedKey == null || !_snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata? save))
		{
			return;
		}

		await SaveBrowserCoordinator.LoadSnapshotAsync(save, _request.LaunchedFromRun);
	}

	private void OnBackupPressed()
	{
		string? result = null;
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata? save))
		{
			result = SaveBrowserCoordinator.BackupSnapshot(save);
		}
		else if (_selectedKey != null && TryGetSelectedRunId(out string? runId))
		{
			result = SaveBrowserCoordinator.BackupRun(_request.IsMultiplayer, runId!);
		}

		_details.Text = result != null
			? SaveUiText.Format(SaveUiText.Keys.SaveBrowser.BackupCreated, ("Path", result))
			: SaveUiText.Get(SaveUiText.Keys.SaveBrowser.BackupFailed);
	}

	private void OnOpenFolderPressed()
	{
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata? save))
		{
			SaveBrowserCoordinator.OpenFolderForSnapshot(save);
			return;
		}

		if (TryGetSelectedRunId(out string? runId) && !string.IsNullOrEmpty(runId))
		{
			SaveBrowserCoordinator.OpenFolderForRun(_request.IsMultiplayer, runId);
		}
	}

	private async void OnEditNotePressed()
	{
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata? save))
		{
			SaveNoteEditDialog.SaveNoteEditResult result = await _noteDialog.ShowAsync(
				SaveUiText.Get(SaveUiText.Keys.SaveBrowser.NoteDialogTitleSnapshot),
				SaveUiText.Get(SaveUiText.Keys.SaveBrowser.NoteDialogLabelSnapshot),
				save.Note);
			if (result.Confirmed && SaveBrowserCoordinator.UpdateSnapshotNote(save, result.Note))
			{
				Refresh(_selectedKey);
			}

			return;
		}

		if (TryGetSelectedRunId(out string? runId) && !string.IsNullOrEmpty(runId))
		{
			string? currentNote = _selectedKey != null && _runsByKey.TryGetValue(_selectedKey, out RunArchiveRecord? run) ? run.Note : ServiceRegistry.ArchiveService.GetRunNote(_request.IsMultiplayer, runId);
			SaveNoteEditDialog.SaveNoteEditResult result = await _noteDialog.ShowAsync(
				SaveUiText.Get(SaveUiText.Keys.SaveBrowser.NoteDialogTitleRun),
				SaveUiText.Get(SaveUiText.Keys.SaveBrowser.NoteDialogLabelRun),
				currentNote);
			if (result.Confirmed && SaveBrowserCoordinator.UpdateRunNote(_request.IsMultiplayer, runId, result.Note))
			{
				Refresh(_selectedKey);
			}
		}
	}

	private void OnDeleteSavePressed()
	{
		if (_selectedKey == null || !_snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata? save))
		{
			return;
		}

		string? preferredKey = _selectedKey;
		SaveBrowserCoordinator.DeleteSnapshot(save);
		Refresh(preferredKey);
	}

	private void OnDeleteRunPressed()
	{
		if (!TryGetSelectedRunId(out string? runId) || string.IsNullOrEmpty(runId))
		{
			return;
		}

		string? preferredKey = _selectedKey;
		SaveBrowserCoordinator.DeleteRun(_request.IsMultiplayer, runId);
		Refresh(preferredKey);
	}

	private async void OnAbandonRunPressed()
	{
		if (!TryGetSelectedRunId(out string? runId) || string.IsNullOrEmpty(runId))
		{
			return;
		}

		if (SaveBrowserCoordinator.IsCurrentRun(_request.IsMultiplayer, runId))
		{
			if (_request.LaunchedFromRun)
			{
				await NGame.Instance!.ReturnToMainMenu();
			}

			if (_request.IsMultiplayer)
			{
				SaveManager.Instance.DeleteCurrentMultiplayerRun();
			}
			else
			{
				SaveManager.Instance.DeleteCurrentRun();
			}
		}

		SaveBrowserCoordinator.DeleteRun(_request.IsMultiplayer, runId);
		Refresh();
	}

	private bool TryGetSelectedRunId(out string? runId)
	{
		runId = null;
		if (_selectedKey == null)
		{
			return false;
		}

		if (_runsByKey.TryGetValue(_selectedKey, out RunArchiveRecord? run))
		{
			runId = run.RunId;
			return true;
		}

		if (_snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata? save))
		{
			runId = save.RunId;
			return true;
		}

		return false;
	}

	private static string BuildRunLabel(RunArchiveRecord run)
	{
		string latest = run.LatestSaveUtc?.LocalDateTime.ToString("g") ?? SaveUiText.Get(SaveUiText.Keys.Common.Unknown);
		string mode = SaveUiText.Get(run.IsMultiplayer ? SaveUiText.Keys.Common.MultiplayerShort : SaveUiText.Keys.Common.SingleplayerShort);
		return SaveUiText.Format(
			SaveUiText.Keys.SaveBrowser.RunLabel,
			("Mode", mode),
			("RunId", run.RunId),
			("AutoCount", run.AutoSaveCount),
			("ManualCount", run.ManualSaveCount),
			("Latest", latest));
	}

	private static string BuildSaveLabel(SaveArchiveMetadata save)
	{
		string kind = SaveUiText.Get(save.Kind == SaveArchiveKind.Auto ? SaveUiText.Keys.Common.Auto : SaveUiText.Keys.Common.Manual);
		string summary = save.Summary.SummaryAvailable
			? SaveUiText.Format(
				SaveUiText.Keys.SaveBrowser.SaveLabel,
				("Kind", kind),
				("Characters", string.Join(", ", save.Summary.CharacterIds)),
				("Act", save.Summary.CurrentActIndex + 1),
				("Floor", save.Summary.EstimatedFloor),
				("Created", save.CreatedUtc.LocalDateTime.ToString("g")))
			: SaveUiText.Format(
				SaveUiText.Keys.SaveBrowser.SaveLabelUnavailable,
				("Kind", kind),
				("Created", save.CreatedUtc.LocalDateTime.ToString("g")));
		return summary;
	}

	private static string BuildSnapshotDetails(SaveArchiveMetadata save, string? runNote)
	{
		string none = SaveUiText.Get(SaveUiText.Keys.SaveBrowser.NoteValueNone);
		if (!save.Summary.SummaryAvailable)
		{
			string details = SaveUiText.Format(
				SaveUiText.Keys.SaveBrowser.SnapshotDetailsUnavailable,
				("SaveId", save.SaveId),
				("Created", save.CreatedUtc.LocalDateTime.ToString("g")),
				("Unknown", SaveUiText.Get(SaveUiText.Keys.Common.Unknown)),
				("Error", save.Summary.ErrorMessage ?? SaveUiText.Get(SaveUiText.Keys.Common.Unknown)));
			return details
				+ "\n"
				+ SaveUiText.Format(SaveUiText.Keys.SaveBrowser.SnapshotNoteLine, ("Note", save.Note ?? none))
				+ "\n"
				+ SaveUiText.Format(SaveUiText.Keys.SaveBrowser.RunNoteLine, ("Note", runNote ?? none));
		}

		string hp = save.Summary.CurrentHp.HasValue && save.Summary.MaxHp.HasValue
			? $"{save.Summary.CurrentHp}/{save.Summary.MaxHp}"
			: SaveUiText.Get(SaveUiText.Keys.Common.Unknown);
		string gold = save.Summary.Gold?.ToString() ?? SaveUiText.Get(SaveUiText.Keys.Common.Unknown);
		string daily = save.Summary.DailyTime?.LocalDateTime.ToString("g") ?? SaveUiText.Get(SaveUiText.Keys.Common.None);
		string kind = SaveUiText.Get(save.Kind == SaveArchiveKind.Auto ? SaveUiText.Keys.Common.Auto : SaveUiText.Keys.Common.Manual);
		string baseDetails = SaveUiText.Format(
			SaveUiText.Keys.SaveBrowser.SnapshotDetails,
			("SaveId", save.SaveId),
			("RunId", save.RunId),
			("Kind", kind),
			("Created", save.CreatedUtc.LocalDateTime.ToString("g")),
			("Characters", string.Join(", ", save.Summary.CharacterIds)),
			("Players", save.Summary.PlayerCount),
			("Act", save.Summary.CurrentActIndex + 1),
			("Floor", save.Summary.EstimatedFloor),
			("Ascension", save.Summary.Ascension),
			("Hp", hp),
			("Gold", gold),
			("Daily", daily),
			("Platform", save.Summary.PlatformType));
		return baseDetails
			+ "\n"
			+ SaveUiText.Format(SaveUiText.Keys.SaveBrowser.SnapshotNoteLine, ("Note", save.Note ?? none))
			+ "\n"
			+ SaveUiText.Format(SaveUiText.Keys.SaveBrowser.RunNoteLine, ("Note", runNote ?? none));
	}

	private string BuildRunDetails(RunArchiveRecord run)
	{
		string current = SaveUiText.CommonYesNo(SaveBrowserCoordinator.IsCurrentRun(_request.IsMultiplayer, run.RunId));
		string latestSave = run.LatestSaveUtc?.LocalDateTime.ToString("g") ?? SaveUiText.Get(SaveUiText.Keys.Common.Unknown);
		string details = SaveUiText.Format(
			SaveUiText.Keys.SaveBrowser.RunDetails,
			("RunId", run.RunId),
			("SnapshotCount", run.AutoSaveCount + run.ManualSaveCount),
			("Current", current),
			("LatestSave", latestSave));
		return details
			+ "\n"
			+ SaveUiText.Format(
				SaveUiText.Keys.SaveBrowser.RunNoteLine,
				("Note", run.Note ?? SaveUiText.Get(SaveUiText.Keys.SaveBrowser.NoteValueNone)));
	}
}