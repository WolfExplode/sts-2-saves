using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Models;
using NyMod.Saves.Features.SaveEdit.Logic;
using NyMod.Saves.Infrastructure.Localization;
using NyMod.Saves.Infrastructure.Ui;

namespace NyMod.Saves.Features.SaveEdit.Presentation;

/// <summary>
/// Lists every <see cref="CharacterModel"/> known to <see cref="ModelDb"/> and lets the
/// user pick one. Returns the chosen <see cref="ModelId"/> via <see cref="ShowAsync"/>.
/// </summary>
internal sealed partial class CharacterPickerDialog : ConfirmationDialog
{
	private ItemList _list = null!;
	private List<ModelId> _ids = new List<ModelId>();
	private TaskCompletionSource<ModelId?>? _pendingResult;

	public override void _Ready()
	{
		Title = SaveUiText.Get(SaveEditUiText.Keys.PickCharacter);
		Exclusive = true;
		Unresizable = true;
		MinSize = new Vector2I(420, 360);
		BuildUi();
		Confirmed += OnConfirmed;
		Canceled += OnCanceled;
		CloseRequested += OnCloseRequested;
	}

	public Task<ModelId?> ShowAsync()
	{
		if (_pendingResult != null && !_pendingResult.Task.IsCompleted)
		{
			return _pendingResult.Task;
		}

		PopulateList();
		_pendingResult = new TaskCompletionSource<ModelId?>();
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

		_list = new ItemList
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			AllowReselect = true,
		};
		margin.AddChild(_list);
	}

	private void PopulateList()
	{
		_list.Clear();
		_ids.Clear();
		foreach (CharacterModel character in ModelDb.AllCharacters)
		{
			string label = CharacterDisplayHelper.LocalizedName(character.Id);
			_list.AddItem(label);
			_ids.Add(character.Id!);
		}

		if (_ids.Count > 0)
		{
			_list.Select(0);
		}
	}

	private void OnConfirmed()
	{
		ModelId? chosen = null;
		int[] selected = _list.GetSelectedItems();
		if (selected.Length > 0 && selected[0] >= 0 && selected[0] < _ids.Count)
		{
			chosen = _ids[selected[0]];
		}

		Complete(chosen);
	}

	private void OnCanceled() => Complete(null);
	private void OnCloseRequested() { Hide(); Complete(null); }

	private void Complete(ModelId? value)
	{
		if (_pendingResult == null || _pendingResult.Task.IsCompleted)
		{
			return;
		}

		_pendingResult.SetResult(value);
	}
}
