using System.Threading.Tasks;
using Godot;
using NyMod.Saves.Infrastructure.Localization;
using NyMod.Saves.Infrastructure.Ui;

namespace NyMod.Saves.Features.SaveBrowser.Presentation;

internal sealed partial class SaveNoteEditDialog : ConfirmationDialog
{
	private Label _label = null!;
	private TextEdit _editor = null!;
	private TaskCompletionSource<SaveNoteEditResult>? _pendingResult;

	public override void _Ready()
	{
		Title = SaveUiText.Get(SaveUiText.Keys.SaveBrowser.NoteDialogTitleSnapshot);
		Exclusive = true;
		Unresizable = true;
		MinSize = new Vector2I(520, 240);
		BuildUi();
		Confirmed += OnConfirmed;
		Canceled += OnCanceled;
		CloseRequested += OnCloseRequested;
	}

	public Task<SaveNoteEditResult> ShowAsync(string title, string label, string? initialNote)
	{
		if (_pendingResult != null && !_pendingResult.Task.IsCompleted)
		{
			return _pendingResult.Task;
		}

		Title = title;
		_label.Text = label;
		_editor.Text = initialNote ?? string.Empty;
		_pendingResult = new TaskCompletionSource<SaveNoteEditResult>();
		DialogThemeAdopter.AdoptParentTheme(this);
		PopupCentered(GetPopupSize());
		CallDeferred(MethodName.FocusEditor);
		return _pendingResult.Task;
	}

	private void BuildUi()
	{
		GetOkButton().Text = SaveUiText.GetFromTable(SaveUiText.MainMenuTable, "GENERIC_POPUP.confirm");
		GetCancelButton().Text = SaveUiText.GetFromTable(SaveUiText.MainMenuTable, "GENERIC_POPUP.cancel");

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		AddChild(margin);

		VBoxContainer root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_label = new Label();
		_label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(_label);

		_editor = new TextEdit();
		_editor.CustomMinimumSize = new Vector2(0f, 150f);
		_editor.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_editor.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_editor.WrapMode = TextEdit.LineWrappingMode.Boundary;
		root.AddChild(_editor);
	}

	private Vector2I GetPopupSize()
	{
		Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1280f, 720f);
		int width = Mathf.Clamp((int)(viewportSize.X - 120f), MinSize.X, 720);
		int height = Mathf.Clamp((int)(viewportSize.Y - 120f), MinSize.Y, 320);
		return new Vector2I(width, height);
	}

	private void FocusEditor()
	{
		_editor.GrabFocus();
		_editor.SelectAll();
	}

	private void OnConfirmed()
	{
		Complete(new SaveNoteEditResult(true, _editor.Text));
	}

	private void OnCanceled()
	{
		Complete(new SaveNoteEditResult(false, null));
	}

	private void OnCloseRequested()
	{
		Hide();
		Complete(new SaveNoteEditResult(false, null));
	}

	private void Complete(SaveNoteEditResult result)
	{
		if (_pendingResult == null || _pendingResult.Task.IsCompleted)
		{
			return;
		}

		_pendingResult.SetResult(result);
	}
	internal readonly record struct SaveNoteEditResult(bool Confirmed, string? Note);
}