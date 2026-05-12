namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Per-room-type x per-reward-kind multipliers used to seed the new player's pending-reward
/// queue when adding a player via <see cref="AddPlayerMode.FromEmpty"/>.
/// Values are floats; per-kind totals across the run are ceil'd before generating rewards.
/// </summary>
internal sealed class AutoRewardConfig
{
	public float NormalCard { get; set; } = 1.0f;
	public float NormalRemove { get; set; } = 0.0f;
	public float NormalRelic { get; set; } = 0.0f;

	public float EliteCard { get; set; } = 1.0f;
	public float EliteRemove { get; set; } = 0.0f;
	public float EliteRelic { get; set; } = 1.0f;

	public float BossCard { get; set; } = 1.0f;
	public float BossRemove { get; set; } = 0.0f;
	public float BossRelic { get; set; } = 1.0f;

	public float EventCard { get; set; } = 0.5f;
	public float EventRemove { get; set; } = 0.3f;
	public float EventRelic { get; set; } = 0.2f;

	public float AncientCard { get; set; } = 0.0f;
	public float AncientRemove { get; set; } = 0.0f;
	public float AncientRelic { get; set; } = 1.0f;

	public float TreasureCard { get; set; } = 0.0f;
	public float TreasureRemove { get; set; } = 0.0f;
	public float TreasureRelic { get; set; } = 1.0f;

	public float ShopCard { get; set; } = 1.0f;
	public float ShopRemove { get; set; } = 1.0f;
	public float ShopRelic { get; set; } = 0.5f;

	public static AutoRewardConfig Default() => new AutoRewardConfig();
}
