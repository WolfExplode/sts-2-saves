using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using NyMod.Saves.Infrastructure.Localization;

namespace NyMod.Saves.Features.SaveBrowser.Presentation;

[ScriptPath("res://Src/Features/SaveBrowser/Presentation/SaveNoteEditDialog.cs")]
internal sealed partial class SaveNoteEditDialog : ConfirmationDialog
{
	internal readonly record struct SaveNoteEditResult(bool Confirmed, string? Note);

	private Label _label = null;

	private TextEdit _editor = null;

	private TaskCompletionSource<SaveNoteEditResult>? _pendingResult;

	public override void _Ready()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		((Window)this).Title = SaveUiText.Get("SAVE_BROWSER.noteDialog.title.snapshot");
		((Window)this).Exclusive = true;
		((Window)this).Unresizable = true;
		((Window)this).MinSize = new Vector2I(520, 240);
		BuildUi();
		((AcceptDialog)this).Confirmed += OnConfirmed;
		((AcceptDialog)this).Canceled += OnCanceled;
		((Window)this).CloseRequested += OnCloseRequested;
	}

	public Task<SaveNoteEditResult> ShowAsync(string title, string label, string? initialNote)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		if (_pendingResult != null && !_pendingResult.Task.IsCompleted)
		{
			return _pendingResult.Task;
		}
		((Window)this).Title = title;
		_label.Text = label;
		_editor.Text = initialNote ?? string.Empty;
		_pendingResult = new TaskCompletionSource<SaveNoteEditResult>();
		((Window)this).PopupCentered((Vector2I?)GetPopupSize());
		((GodotObject)this).CallDeferred("FocusEditor", Array.Empty<Variant>());
		return _pendingResult.Task;
	}

	private void BuildUi()
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		((AcceptDialog)this).GetOkButton().Text = SaveUiText.GetFromTable("main_menu_ui", "GENERIC_POPUP.confirm");
		((ConfirmationDialog)this).GetCancelButton().Text = SaveUiText.GetFromTable("main_menu_ui", "GENERIC_POPUP.cancel");
		MarginContainer val = new MarginContainer();
		((Control)val).AddThemeConstantOverride((StringName)("margin_left"), 20);
		((Control)val).AddThemeConstantOverride((StringName)("margin_top"), 20);
		((Control)val).AddThemeConstantOverride((StringName)("margin_right"), 20);
		((Control)val).AddThemeConstantOverride((StringName)("margin_bottom"), 12);
		((Node)this).AddChild((Node)(object)val, false, (Node.InternalMode)0);
		VBoxContainer val2 = new VBoxContainer();
		((Control)val2).AddThemeConstantOverride((StringName)("separation"), 12);
		((Node)val).AddChild((Node)(object)val2, false, (Node.InternalMode)0);
		_label = new Label();
		_label.AutowrapMode = (TextServer.AutowrapMode)3;
		((Node)val2).AddChild((Node)(object)_label, false, (Node.InternalMode)0);
		_editor = new TextEdit();
		((Control)_editor).CustomMinimumSize = new Vector2(0f, 150f);
		((Control)_editor).SizeFlagsHorizontal = (Control.SizeFlags)3;
		((Control)_editor).SizeFlagsVertical = (Control.SizeFlags)3;
		_editor.WrapMode = (TextEdit.LineWrappingMode)1;
		((Node)val2).AddChild((Node)(object)_editor, false, (Node.InternalMode)0);
	}

	private Vector2I GetPopupSize()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		Viewport viewport = ((Node)this).GetViewport();
		Vector2? val;
		if (viewport == null)
		{
			val = new Vector2(1280f, 720f);
		}
		else
		{
			Rect2 visibleRect = viewport.GetVisibleRect();
			val = (visibleRect).Size;
		}
		Vector2 val2 = (Vector2)val;
		int num = Mathf.Clamp((int)(val2.X - 120f), ((Window)this).MinSize.X, 720);
		int num2 = Mathf.Clamp((int)(val2.Y - 120f), ((Window)this).MinSize.Y, 320);
		return new Vector2I(num, num2);
	}

	private void FocusEditor()
	{
		((Control)_editor).GrabFocus();
		_editor.SelectAll();
	}

	private void OnConfirmed()
	{
		Complete(new SaveNoteEditResult(Confirmed: true, _editor.Text));
	}

	private void OnCanceled()
	{
		Complete(new SaveNoteEditResult(Confirmed: false, null));
	}

	private void OnCloseRequested()
	{
		((Window)this).Hide();
		Complete(new SaveNoteEditResult(Confirmed: false, null));
	}

	private void Complete(SaveNoteEditResult result)
	{
		if (_pendingResult != null && !_pendingResult.Task.IsCompleted)
		{
			_pendingResult.SetResult(result);
		}
	}

}
