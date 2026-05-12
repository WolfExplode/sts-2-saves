using System.Threading.Tasks;
using Godot;
using NyMod.Saves.Features.SaveEdit.Logic;
using NyMod.Saves.Infrastructure.Localization;
using NyMod.Saves.Infrastructure.Ui;

namespace NyMod.Saves.Features.SaveEdit.Presentation;

/// <summary>
/// Lets the user tune the per-room-type / per-reward-kind multipliers used to seed a freshly
/// added "From Empty" player's pending-reward queue. Returns null when the user picks
/// "Skip auto rewards" or cancels the dialog.
/// </summary>
internal sealed partial class AutoRewardConfigDialog : Window
{
	private TaskCompletionSource<AutoRewardConfig?>? _pending;

	private SpinBox _normalCard = null!, _normalRemove = null!, _normalRelic = null!;
	private SpinBox _eliteCard = null!, _eliteRemove = null!, _eliteRelic = null!;
	private SpinBox _bossCard = null!, _bossRemove = null!, _bossRelic = null!;
	private SpinBox _eventCard = null!, _eventRemove = null!, _eventRelic = null!;
	private SpinBox _ancientCard = null!, _ancientRemove = null!, _ancientRelic = null!;
	private SpinBox _treasureCard = null!, _treasureRemove = null!, _treasureRelic = null!;
	private SpinBox _shopCard = null!, _shopRemove = null!, _shopRelic = null!;

	public override void _Ready()
	{
		Title = SaveUiText.Get(SaveEditUiText.Keys.AutoRewardTitle);
		Exclusive = true;
		Unresizable = false;
		Visible = false;
		BuildUi();
		CloseRequested += OnSkipPressed;
	}

	public Task<AutoRewardConfig?> ShowAsync()
	{
		if (_pending != null && !_pending.Task.IsCompleted)
		{
			return _pending.Task;
		}

		AutoRewardConfig defaults = AutoRewardConfig.Default();
		_normalCard.Value   = defaults.NormalCard;   _normalRemove.Value   = defaults.NormalRemove;   _normalRelic.Value   = defaults.NormalRelic;
		_eliteCard.Value    = defaults.EliteCard;    _eliteRemove.Value    = defaults.EliteRemove;    _eliteRelic.Value    = defaults.EliteRelic;
		_bossCard.Value     = defaults.BossCard;     _bossRemove.Value     = defaults.BossRemove;     _bossRelic.Value     = defaults.BossRelic;
		_eventCard.Value    = defaults.EventCard;    _eventRemove.Value    = defaults.EventRemove;    _eventRelic.Value    = defaults.EventRelic;
		_ancientCard.Value  = defaults.AncientCard;  _ancientRemove.Value  = defaults.AncientRemove;  _ancientRelic.Value  = defaults.AncientRelic;
		_treasureCard.Value = defaults.TreasureCard; _treasureRemove.Value = defaults.TreasureRemove; _treasureRelic.Value = defaults.TreasureRelic;
		_shopCard.Value     = defaults.ShopCard;     _shopRemove.Value     = defaults.ShopRemove;     _shopRelic.Value     = defaults.ShopRelic;

		_pending = new TaskCompletionSource<AutoRewardConfig?>();
		DialogThemeAdopter.AdoptParentTheme(this);
		PopupCentered(new Vector2I(560, 420));
		return _pending.Task;
	}

	private void BuildUi()
	{
		MarginContainer margin = new MarginContainer
		{
			AnchorRight = 1f,
			AnchorBottom = 1f,
		};
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		AddChild(margin);

		VBoxContainer root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 8);
		root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(root);

		Label hint = new Label
		{
			Text = SaveUiText.Get(SaveEditUiText.Keys.AutoRewardHint),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		root.AddChild(hint);

		GridContainer grid = new GridContainer
		{
			Columns = 4,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 6);
		root.AddChild(grid);

		// Header row: blank | Card | Remove | Relic
		grid.AddChild(new Label { Text = string.Empty });
		grid.AddChild(HeaderLabel(SaveUiText.Get(SaveEditUiText.Keys.AutoRewardColCard)));
		grid.AddChild(HeaderLabel(SaveUiText.Get(SaveEditUiText.Keys.AutoRewardColRemove)));
		grid.AddChild(HeaderLabel(SaveUiText.Get(SaveEditUiText.Keys.AutoRewardColRelic)));

		AddRow(grid, SaveEditUiText.Keys.AutoRewardRowNormal,   out _normalCard,   out _normalRemove,   out _normalRelic);
		AddRow(grid, SaveEditUiText.Keys.AutoRewardRowElite,    out _eliteCard,    out _eliteRemove,    out _eliteRelic);
		AddRow(grid, SaveEditUiText.Keys.AutoRewardRowBoss,     out _bossCard,     out _bossRemove,     out _bossRelic);
		AddRow(grid, SaveEditUiText.Keys.AutoRewardRowEvent,    out _eventCard,    out _eventRemove,    out _eventRelic);
		AddRow(grid, SaveEditUiText.Keys.AutoRewardRowAncient,  out _ancientCard,  out _ancientRemove,  out _ancientRelic);
		AddRow(grid, SaveEditUiText.Keys.AutoRewardRowTreasure, out _treasureCard, out _treasureRemove, out _treasureRelic);
		AddRow(grid, SaveEditUiText.Keys.AutoRewardRowShop,     out _shopCard,     out _shopRemove,     out _shopRelic);

		// Spacer pushes the action row to the bottom.
		Control spacer = new Control
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		root.AddChild(spacer);

		root.AddChild(new HSeparator());
		HBoxContainer actions = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.End,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		actions.AddThemeConstantOverride("separation", 12);
		root.AddChild(actions);

		Button skip = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.AutoRewardSkip) };
		skip.Pressed += OnSkipPressed;
		actions.AddChild(skip);

		Button apply = new Button { Text = SaveUiText.Get(SaveEditUiText.Keys.AutoRewardApply) };
		apply.Pressed += OnApplyPressed;
		actions.AddChild(apply);
	}

	private static Label HeaderLabel(string text)
	{
		return new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
	}

	private static void AddRow(GridContainer grid, string labelKey, out SpinBox card, out SpinBox remove, out SpinBox relic)
	{
		grid.AddChild(new Label { Text = SaveUiText.Get(labelKey) });
		card = MakeSpin();   grid.AddChild(card);
		remove = MakeSpin(); grid.AddChild(remove);
		relic = MakeSpin();  grid.AddChild(relic);
	}

	private static SpinBox MakeSpin()
	{
		return new SpinBox
		{
			MinValue = 0,
			MaxValue = 99,
			Step = 0.1,
			CustomMinimumSize = new Vector2(80f, 0f),
		};
	}

	private void OnApplyPressed()
	{
		AutoRewardConfig cfg = new AutoRewardConfig
		{
			NormalCard   = (float)_normalCard.Value,   NormalRemove   = (float)_normalRemove.Value,   NormalRelic   = (float)_normalRelic.Value,
			EliteCard    = (float)_eliteCard.Value,    EliteRemove    = (float)_eliteRemove.Value,    EliteRelic    = (float)_eliteRelic.Value,
			BossCard     = (float)_bossCard.Value,     BossRemove     = (float)_bossRemove.Value,     BossRelic     = (float)_bossRelic.Value,
			EventCard    = (float)_eventCard.Value,    EventRemove    = (float)_eventRemove.Value,    EventRelic    = (float)_eventRelic.Value,
			AncientCard  = (float)_ancientCard.Value,  AncientRemove  = (float)_ancientRemove.Value,  AncientRelic  = (float)_ancientRelic.Value,
			TreasureCard = (float)_treasureCard.Value, TreasureRemove = (float)_treasureRemove.Value, TreasureRelic = (float)_treasureRelic.Value,
			ShopCard     = (float)_shopCard.Value,     ShopRemove     = (float)_shopRemove.Value,     ShopRelic     = (float)_shopRelic.Value,
		};
		Hide();
		Resolve(cfg);
	}

	private void OnSkipPressed()
	{
		Hide();
		Resolve(null);
	}

	private void Resolve(AutoRewardConfig? cfg)
	{
		if (_pending != null && !_pending.Task.IsCompleted)
		{
			_pending.SetResult(cfg);
		}
	}
}
