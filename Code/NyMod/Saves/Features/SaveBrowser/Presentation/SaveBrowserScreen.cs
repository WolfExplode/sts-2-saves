using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using MegaCrit.Sts2.Core.Assets;
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

[ScriptPath("res://Src/Features/SaveBrowser/Presentation/SaveBrowserScreen.cs")]
internal sealed partial class SaveBrowserScreen : NSubmenu
{
	private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";

	private readonly Dictionary<string, SaveArchiveMetadata> _snapshotsByKey = new Dictionary<string, SaveArchiveMetadata>(StringComparer.Ordinal);

	private readonly Dictionary<string, RunArchiveRecord> _runsByKey = new Dictionary<string, RunArchiveRecord>(StringComparer.Ordinal);

	private readonly Dictionary<string, RunArchiveRecord> _runsById = new Dictionary<string, RunArchiveRecord>(StringComparer.Ordinal);

	private SaveBrowserRequest _request;

	private Tree _tree = null;

	private RichTextLabel _details = null;

	private Label _title = null;

	private Button _loadButton = null;

	private Button _backupButton = null;

	private Button _openFolderButton = null;

	private Button _editNoteButton = null;

	private Button _deleteSaveButton = null;

	private Button _deleteRunButton = null;

	private Button _abandonRunButton = null;

	private SaveNoteEditDialog _noteDialog = null;

	private string? _selectedKey;

	private bool _wasVisibleForMainMenuRefresh;

	protected override Control? InitialFocusedControl => (Control?)(object)_tree;

	public static SaveBrowserScreen Create()
	{
		return new SaveBrowserScreen();
	}

	public void Configure(SaveBrowserRequest request)
	{
		_request = request;
		if (((Node)this).IsNodeReady())
		{
			Refresh();
		}
	}

	public override void _Ready()
	{
		((CanvasItem)this).TopLevel = true;
		BuildUi();
		ConnectSignals();
		ConnectViewportSignals();
		ApplyViewportLayout();
		Refresh();
		_wasVisibleForMainMenuRefresh = ((CanvasItem)this).Visible;
		((CanvasItem)this).VisibilityChanged += OnVisibilityChangedRefreshMainMenu;
	}

	protected override void ConnectSignals()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		base.ConnectSignals();
		((GodotObject)_tree).Connect(Tree.SignalName.ItemSelected, Callable.From((Action)OnItemSelected), 0u);
		((GodotObject)_loadButton).Connect(BaseButton.SignalName.Pressed, Callable.From((Action)OnLoadPressed), 0u);
		((GodotObject)_backupButton).Connect(BaseButton.SignalName.Pressed, Callable.From((Action)OnBackupPressed), 0u);
		((GodotObject)_openFolderButton).Connect(BaseButton.SignalName.Pressed, Callable.From((Action)OnOpenFolderPressed), 0u);
		((GodotObject)_editNoteButton).Connect(BaseButton.SignalName.Pressed, Callable.From((Action)OnEditNotePressed), 0u);
		((GodotObject)_deleteSaveButton).Connect(BaseButton.SignalName.Pressed, Callable.From((Action)OnDeleteSavePressed), 0u);
		((GodotObject)_deleteRunButton).Connect(BaseButton.SignalName.Pressed, Callable.From((Action)OnDeleteRunPressed), 0u);
		((GodotObject)_abandonRunButton).Connect(BaseButton.SignalName.Pressed, Callable.From((Action)OnAbandonRunPressed), 0u);
	}

	private void OnVisibilityChangedRefreshMainMenu()
	{
		bool wasVisibleForMainMenuRefresh = _wasVisibleForMainMenuRefresh;
		_wasVisibleForMainMenuRefresh = ((CanvasItem)this).Visible;
		if (wasVisibleForMainMenuRefresh && !((CanvasItem)this).Visible)
		{
			SaveBrowserCoordinator.RefreshMainMenuAfterArchiveChange();
		}
	}

	public override void OnSubmenuOpened()
	{
		ApplyViewportLayout();
		Refresh();
	}

	private void ConnectViewportSignals()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Viewport viewport = ((Node)this).GetViewport();
		if (viewport != null)
		{
			((GodotObject)viewport).Connect(Viewport.SignalName.SizeChanged, Callable.From((Action)ApplyViewportLayout), 0u);
		}
	}

	private void ApplyViewportLayout()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		((Control)this).SetAnchorsPreset((LayoutPreset)0, false);
		((Control)this).Position = Vector2.Zero;
		Rect2 viewportRect = ((CanvasItem)this).GetViewportRect();
		((Control)this).Size = (viewportRect).Size;
	}

	private void BuildUi()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Expected O, but got Unknown
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Expected O, but got Unknown
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Expected O, but got Unknown
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Expected O, but got Unknown
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Expected O, but got Unknown
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Expected O, but got Unknown
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_0332: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Expected O, but got Unknown
		((Node)this).ProcessMode = (ProcessModeEnum)3;
		((Control)this).MouseFilter = (MouseFilterEnum)0;
		ColorRect val = new ColorRect
		{
			Color = new Color(0.03f, 0.03f, 0.04f, 0.95f)
		};
		((Control)val).SetAnchorsPreset((LayoutPreset)15, false);
		((Node)this).AddChild((Node)(object)val, false, (Node.InternalMode)0);
		MarginContainer val2 = new MarginContainer();
		((Control)val2).SetAnchorsPreset((LayoutPreset)15, false);
		((Control)val2).AddThemeConstantOverride((StringName)("margin_left"), 56);
		((Control)val2).AddThemeConstantOverride((StringName)("margin_top"), 56);
		((Control)val2).AddThemeConstantOverride((StringName)("margin_right"), 56);
		((Control)val2).AddThemeConstantOverride((StringName)("margin_bottom"), 48);
		((Node)this).AddChild((Node)(object)val2, false, (Node.InternalMode)0);
		VBoxContainer val3 = new VBoxContainer();
		((Control)val3).SizeFlagsHorizontal = (Control.SizeFlags)3;
		((Control)val3).SizeFlagsVertical = (Control.SizeFlags)3;
		((Control)val3).AddThemeConstantOverride((StringName)("separation"), 16);
		((Node)val2).AddChild((Node)(object)val3, false, (Node.InternalMode)0);
		_title = new Label();
		((Control)_title).AddThemeFontSizeOverride((StringName)("font_size"), 28);
		((Node)val3).AddChild((Node)(object)_title, false, (Node.InternalMode)0);
		HBoxContainer val4 = new HBoxContainer();
		((Control)val4).SizeFlagsHorizontal = (Control.SizeFlags)3;
		((Control)val4).SizeFlagsVertical = (Control.SizeFlags)3;
		((Control)val4).AddThemeConstantOverride((StringName)("separation"), 20);
		((Node)val3).AddChild((Node)(object)val4, false, (Node.InternalMode)0);
		MarginContainer val5 = new MarginContainer();
		((Control)val5).SizeFlagsHorizontal = (Control.SizeFlags)3;
		((Control)val5).SizeFlagsVertical = (Control.SizeFlags)3;
		((Control)val5).CustomMinimumSize = new Vector2(420f, 0f);
		((Node)val4).AddChild((Node)(object)val5, false, (Node.InternalMode)0);
		_tree = new Tree();
		_tree.Columns = 1;
		_tree.HideRoot = true;
		((Control)_tree).SizeFlagsHorizontal = (Control.SizeFlags)3;
		((Control)_tree).SizeFlagsVertical = (Control.SizeFlags)3;
		((Control)_tree).AddThemeColorOverride((StringName)("font_color"), Colors.White);
		((Control)_tree).AddThemeColorOverride((StringName)("font_hovered_color"), Colors.White);
		((Control)_tree).AddThemeColorOverride((StringName)("font_selected_color"), Colors.White);
		((Control)_tree).AddThemeColorOverride((StringName)("guide_color"), new Color(0.55f, 0.55f, 0.6f, 0.8f));
		((Node)val5).AddChild((Node)(object)_tree, false, (Node.InternalMode)0);
		VBoxContainer val6 = new VBoxContainer();
		((Control)val6).SizeFlagsHorizontal = (Control.SizeFlags)1;
		((Control)val6).SizeFlagsVertical = (Control.SizeFlags)3;
		((Control)val6).CustomMinimumSize = new Vector2(360f, 0f);
		((Control)val6).AddThemeConstantOverride((StringName)("separation"), 12);
		((Node)val4).AddChild((Node)(object)val6, false, (Node.InternalMode)0);
		_details = new RichTextLabel();
		_details.BbcodeEnabled = true;
		_details.FitContent = false;
		_details.ScrollActive = true;
		((Control)_details).SizeFlagsHorizontal = (Control.SizeFlags)3;
		((Control)_details).SizeFlagsVertical = (Control.SizeFlags)3;
		((Control)_details).AddThemeColorOverride((StringName)("default_color"), Colors.White);
		((Node)val6).AddChild((Node)(object)_details, false, (Node.InternalMode)0);
		GridContainer val7 = new GridContainer();
		val7.Columns = 2;
		((Control)val7).AddThemeConstantOverride((StringName)("h_separation"), 8);
		((Control)val7).AddThemeConstantOverride((StringName)("v_separation"), 8);
		((Node)val6).AddChild((Node)(object)val7, false, (Node.InternalMode)0);
		_loadButton = CreateActionButton(SaveUiText.Get("SAVE_BROWSER.action.loadSnapshot"));
		_backupButton = CreateActionButton(SaveUiText.Get("SAVE_BROWSER.action.backup"));
		_openFolderButton = CreateActionButton(SaveUiText.Get("SAVE_BROWSER.action.openFolder"));
		_editNoteButton = CreateActionButton(SaveUiText.Get("SAVE_BROWSER.action.editNote"));
		_deleteSaveButton = CreateActionButton(SaveUiText.Get("SAVE_BROWSER.action.deleteSave"));
		_deleteRunButton = CreateActionButton(SaveUiText.Get("SAVE_BROWSER.action.deleteRun"));
		_abandonRunButton = CreateActionButton(SaveUiText.GetFromTable("main_menu_ui", "ABANDON_RUN"));
		((Node)val7).AddChild((Node)(object)_loadButton, false, (Node.InternalMode)0);
		((Node)val7).AddChild((Node)(object)_backupButton, false, (Node.InternalMode)0);
		((Node)val7).AddChild((Node)(object)_openFolderButton, false, (Node.InternalMode)0);
		((Node)val7).AddChild((Node)(object)_editNoteButton, false, (Node.InternalMode)0);
		((Node)val7).AddChild((Node)(object)_deleteSaveButton, false, (Node.InternalMode)0);
		((Node)val7).AddChild((Node)(object)_deleteRunButton, false, (Node.InternalMode)0);
		((Node)val7).AddChild((Node)(object)_abandonRunButton, false, (Node.InternalMode)0);
		_noteDialog = new SaveNoteEditDialog();
		((Node)this).AddChild((Node)(object)_noteDialog, false, (Node.InternalMode)0);
		PackedScene scene = PreloadManager.Cache.GetScene("res://scenes/ui/back_button.tscn");
		NBackButton val8 = scene.Instantiate<NBackButton>((PackedScene.GenEditState)0);
		((Node)val8).Name = (StringName)("BackButton");
		((Node)this).AddChild((Node)(object)val8, false, (Node.InternalMode)0);
	}

	private static Button CreateActionButton(string text)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		return new Button
		{
			Text = text,
			SizeFlagsHorizontal = (Control.SizeFlags)3
		};
	}

	private void Refresh(string? preferredKey = null)
	{
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		_snapshotsByKey.Clear();
		_runsByKey.Clear();
		_runsById.Clear();
		_title.Text = SaveUiText.Get(_request.IsMultiplayer ? "SAVE_BROWSER.title.multiplayer" : "SAVE_BROWSER.title.singleplayer");
		_tree.Clear();
		TreeItem val = _tree.CreateItem((TreeItem)null, -1);
		TreeItem firstSnapshotItem = null;
		TreeItem preferredItem = null;
		foreach (RunArchiveRecord item in ServiceRegistry.ArchiveService.ListRuns(_request.IsMultiplayer))
		{
			string text = "run:" + item.RunId;
			_runsByKey[text] = item;
			_runsById[item.RunId] = item;
			TreeItem val2 = _tree.CreateItem(val, -1);
			val2.SetText(0, BuildRunLabel(item));
			val2.SetMetadata(0, (Variant)(text));
			val2.Collapsed = false;
			if (string.Equals(text, preferredKey, StringComparison.Ordinal))
			{
				preferredItem = val2;
			}
			AddSaveGroup(val2, item.RunId, SaveArchiveKind.Auto, "SAVE_BROWSER.group.auto", preferredKey, ref firstSnapshotItem, ref preferredItem);
			AddSaveGroup(val2, item.RunId, SaveArchiveKind.Manual, "SAVE_BROWSER.group.manual", preferredKey, ref firstSnapshotItem, ref preferredItem);
		}
		TreeItem val3 = preferredItem ?? firstSnapshotItem;
		if (val3 == null)
		{
			_selectedKey = null;
			_details.Text = SaveUiText.Get("SAVE_BROWSER.empty");
			UpdateActionButtons();
		}
		else
		{
			val3.Select(0);
			Variant metadata = val3.GetMetadata(0);
			_selectedKey = (metadata).AsString();
			UpdateDetailsForSelection();
			UpdateActionButtons();
		}
	}

	private void AddSaveGroup(TreeItem runItem, string runId, SaveArchiveKind kind, string labelKey, string? preferredKey, ref TreeItem? firstSnapshotItem, ref TreeItem? preferredItem)
	{
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		IReadOnlyList<SaveArchiveMetadata> readOnlyList = (from save in ServiceRegistry.ArchiveService.ListSnapshots(_request.IsMultiplayer, runId)
			where save.Kind == kind
			select save).ToList();
		TreeItem val = _tree.CreateItem(runItem, -1);
		val.SetText(0, SaveUiText.Format(labelKey, ("Count", readOnlyList.Count)));
		val.SetSelectable(0, false);
		val.Collapsed = false;
		foreach (SaveArchiveMetadata item in readOnlyList)
		{
			string text = $"save:{item.RunId}:{item.Kind}:{item.SaveId}";
			_snapshotsByKey[text] = item;
			TreeItem val2 = _tree.CreateItem(val, -1);
			val2.SetText(0, BuildSaveLabel(item));
			val2.SetMetadata(0, (Variant)(text));
			if (firstSnapshotItem == null)
			{
				firstSnapshotItem = val2;
			}
			if (string.Equals(text, preferredKey, StringComparison.Ordinal))
			{
				preferredItem = val2;
			}
		}
	}

	private void OnItemSelected()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		TreeItem selected = _tree.GetSelected();
		object selectedKey;
		if (selected == null)
		{
			selectedKey = null;
		}
		else
		{
			Variant metadata = selected.GetMetadata(0);
			selectedKey = (metadata).AsString();
		}
		_selectedKey = (string?)selectedKey;
		UpdateDetailsForSelection();
		UpdateActionButtons();
	}

	private void UpdateDetailsForSelection()
	{
		RunArchiveRecord value3;
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata value))
		{
			RunArchiveRecord value2;
			string runNote = (_runsById.TryGetValue(value.RunId, out value2) ? value2.Note : ServiceRegistry.ArchiveService.GetRunNote(_request.IsMultiplayer, value.RunId));
			_details.Text = BuildSnapshotDetails(value, runNote);
		}
		else if (_selectedKey != null && _runsByKey.TryGetValue(_selectedKey, out value3))
		{
			_details.Text = BuildRunDetails(value3);
		}
		else
		{
			_details.Text = SaveUiText.Get("SAVE_BROWSER.selectHint");
		}
	}

	private void UpdateActionButtons()
	{
		bool flag = _selectedKey != null && _snapshotsByKey.ContainsKey(_selectedKey);
		bool flag2 = _selectedKey != null && (_runsByKey.ContainsKey(_selectedKey) || flag);
		((BaseButton)_loadButton).Disabled = !flag;
		((BaseButton)_deleteSaveButton).Disabled = !flag;
		((BaseButton)_backupButton).Disabled = !flag2;
		((BaseButton)_openFolderButton).Disabled = !flag2;
		((BaseButton)_editNoteButton).Disabled = !flag2;
		((BaseButton)_deleteRunButton).Disabled = !flag2;
		((BaseButton)_abandonRunButton).Disabled = !flag2;
	}

	private async void OnLoadPressed()
	{
		if (_selectedKey == null || !_snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata save))
		{
			return;
		}
		if (_loadButton != null)
		{
			((BaseButton)_loadButton).Disabled = true;
		}
		try
		{
			await SaveBrowserCoordinator.LoadSnapshotAsync(save, _request.LaunchedFromRun);
		}
		finally
		{
			if (_loadButton != null && GodotObject.IsInstanceValid((GodotObject)(object)_loadButton))
			{
				((BaseButton)_loadButton).Disabled = false;
			}
		}
	}

	private void OnBackupPressed()
	{
		string text = null;
		string runId;
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata value))
		{
			text = SaveBrowserCoordinator.BackupSnapshot(value);
		}
		else if (_selectedKey != null && TryGetSelectedRunId(out runId))
		{
			text = SaveBrowserCoordinator.BackupRun(_request.IsMultiplayer, runId);
		}
		_details.Text = ((text != null) ? SaveUiText.Format("SAVE_BROWSER.backupCreated", ("Path", text)) : SaveUiText.Get("SAVE_BROWSER.backupFailed"));
	}

	private void OnOpenFolderPressed()
	{
		string runId;
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata value))
		{
			SaveBrowserCoordinator.OpenFolderForSnapshot(value);
		}
		else if (TryGetSelectedRunId(out runId) && !string.IsNullOrEmpty(runId))
		{
			SaveBrowserCoordinator.OpenFolderForRun(_request.IsMultiplayer, runId);
		}
	}

	private async void OnEditNotePressed()
	{
		string runId;
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata save))
		{
			SaveNoteEditDialog.SaveNoteEditResult result = await _noteDialog.ShowAsync(SaveUiText.Get("SAVE_BROWSER.noteDialog.title.snapshot"), SaveUiText.Get("SAVE_BROWSER.noteDialog.label.snapshot"), save.Note);
			if (result.Confirmed && SaveBrowserCoordinator.UpdateSnapshotNote(save, result.Note))
			{
				Refresh(_selectedKey);
			}
		}
		else if (TryGetSelectedRunId(out runId) && !string.IsNullOrEmpty(runId))
		{
			RunArchiveRecord run;
			string currentNote = ((_selectedKey != null && _runsByKey.TryGetValue(_selectedKey, out run)) ? run.Note : ServiceRegistry.ArchiveService.GetRunNote(_request.IsMultiplayer, runId));
			SaveNoteEditDialog.SaveNoteEditResult result2 = await _noteDialog.ShowAsync(SaveUiText.Get("SAVE_BROWSER.noteDialog.title.run"), SaveUiText.Get("SAVE_BROWSER.noteDialog.label.run"), currentNote);
			if (result2.Confirmed && SaveBrowserCoordinator.UpdateRunNote(_request.IsMultiplayer, runId, result2.Note))
			{
				Refresh(_selectedKey);
			}
		}
	}

	private void OnDeleteSavePressed()
	{
		if (_selectedKey != null && _snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata value))
		{
			string selectedKey = _selectedKey;
			SaveBrowserCoordinator.DeleteSnapshot(value);
			Refresh(selectedKey);
		}
	}

	private void OnDeleteRunPressed()
	{
		if (TryGetSelectedRunId(out string runId) && !string.IsNullOrEmpty(runId))
		{
			string selectedKey = _selectedKey;
			SaveBrowserCoordinator.DeleteRun(_request.IsMultiplayer, runId);
			Refresh(selectedKey);
			SaveBrowserCoordinator.RefreshMainMenuAfterArchiveChange();
		}
	}

	private async void OnAbandonRunPressed()
	{
		if (!TryGetSelectedRunId(out string runId) || string.IsNullOrEmpty(runId))
		{
			return;
		}
		if (SaveBrowserCoordinator.IsCurrentRun(_request.IsMultiplayer, runId))
		{
			if (_request.LaunchedFromRun)
			{
				await NGame.Instance.ReturnToMainMenu();
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
		SaveBrowserCoordinator.RefreshMainMenuAfterArchiveChange();
	}

	private bool TryGetSelectedRunId(out string? runId)
	{
		runId = null;
		if (_selectedKey == null)
		{
			return false;
		}
		if (_runsByKey.TryGetValue(_selectedKey, out RunArchiveRecord value))
		{
			runId = value.RunId;
			return true;
		}
		if (_snapshotsByKey.TryGetValue(_selectedKey, out SaveArchiveMetadata value2))
		{
			runId = value2.RunId;
			return true;
		}
		return false;
	}

	private static string BuildRunLabel(RunArchiveRecord run)
	{
		string item = run.LatestSaveUtc?.LocalDateTime.ToString("g") ?? SaveUiText.Get("COMMON.unknown");
		string item2 = SaveUiText.Get(run.IsMultiplayer ? "COMMON.multiplayerShort" : "COMMON.singleplayerShort");
		return SaveUiText.Format("SAVE_BROWSER.runLabel", ("Mode", item2), ("RunId", run.RunId), ("AutoCount", run.AutoSaveCount), ("ManualCount", run.ManualSaveCount), ("Latest", item));
	}

	private static string BuildSaveLabel(SaveArchiveMetadata save)
	{
		string item = SaveUiText.Get((save.Kind == SaveArchiveKind.Auto) ? "COMMON.auto" : "COMMON.manual");
		return save.Summary.SummaryAvailable ? SaveUiText.Format("SAVE_BROWSER.saveLabel", ("Kind", item), ("Characters", string.Join(", ", save.Summary.CharacterIds)), ("Act", save.Summary.CurrentActIndex + 1), ("Floor", save.Summary.EstimatedFloor), ("Created", save.CreatedUtc.LocalDateTime.ToString("g"))) : SaveUiText.Format("SAVE_BROWSER.saveLabelUnavailable", ("Kind", item), ("Created", save.CreatedUtc.LocalDateTime.ToString("g")));
	}

	private static string BuildSnapshotDetails(SaveArchiveMetadata save, string? runNote)
	{
		string text = SaveUiText.Get("SAVE_BROWSER.note.none");
		if (!save.Summary.SummaryAvailable)
		{
			string text2 = SaveUiText.Format("SAVE_BROWSER.snapshotDetailsUnavailable", ("SaveId", save.SaveId), ("Created", save.CreatedUtc.LocalDateTime.ToString("g")), ("Unknown", SaveUiText.Get("COMMON.unknown")), ("Error", save.Summary.ErrorMessage ?? SaveUiText.Get("COMMON.unknown")));
			return text2 + "\n" + SaveUiText.Format("SAVE_BROWSER.note.snapshot", ("Note", save.Note ?? text)) + "\n" + SaveUiText.Format("SAVE_BROWSER.note.run", ("Note", runNote ?? text));
		}
		string item = ((save.Summary.CurrentHp.HasValue && save.Summary.MaxHp.HasValue) ? $"{save.Summary.CurrentHp}/{save.Summary.MaxHp}" : SaveUiText.Get("COMMON.unknown"));
		string item2 = save.Summary.Gold?.ToString() ?? SaveUiText.Get("COMMON.unknown");
		string item3 = save.Summary.DailyTime?.LocalDateTime.ToString("g") ?? SaveUiText.Get("COMMON.none");
		string item4 = SaveUiText.Get((save.Kind == SaveArchiveKind.Auto) ? "COMMON.auto" : "COMMON.manual");
		string text3 = SaveUiText.Format("SAVE_BROWSER.snapshotDetails", ("SaveId", save.SaveId), ("RunId", save.RunId), ("Kind", item4), ("Created", save.CreatedUtc.LocalDateTime.ToString("g")), ("Characters", string.Join(", ", save.Summary.CharacterIds)), ("Players", save.Summary.PlayerCount), ("Act", save.Summary.CurrentActIndex + 1), ("Floor", save.Summary.EstimatedFloor), ("Ascension", save.Summary.Ascension), ("Hp", item), ("Gold", item2), ("Daily", item3), ("Platform", save.Summary.PlatformType));
		return text3 + "\n" + SaveUiText.Format("SAVE_BROWSER.note.snapshot", ("Note", save.Note ?? text)) + "\n" + SaveUiText.Format("SAVE_BROWSER.note.run", ("Note", runNote ?? text));
	}

	private string BuildRunDetails(RunArchiveRecord run)
	{
		string item = SaveUiText.CommonYesNo(SaveBrowserCoordinator.IsCurrentRun(_request.IsMultiplayer, run.RunId));
		string item2 = run.LatestSaveUtc?.LocalDateTime.ToString("g") ?? SaveUiText.Get("COMMON.unknown");
		string text = SaveUiText.Format("SAVE_BROWSER.runDetails", ("RunId", run.RunId), ("SnapshotCount", run.AutoSaveCount + run.ManualSaveCount), ("Current", item), ("LatestSave", item2));
		return text + "\n" + SaveUiText.Format("SAVE_BROWSER.note.run", ("Note", run.Note ?? SaveUiText.Get("SAVE_BROWSER.note.none")));
	}

}
