namespace NyMod.Saves.Infrastructure.Localization;

/// <summary>
/// Localization key constants for the SaveEdit feature. Keys are added to
/// <c>localization/eng/sts2_saves_ui.json</c> and <c>zhs/sts2_saves_ui.json</c>.
/// </summary>
internal static class SaveEditUiText
{
	internal static class Keys
	{
		// Browser action button
		public const string EditSave = "SAVE_BROWSER.action.editSave";

		// SaveEditDialog
		public const string Title = "SAVE_EDIT.title";
		public const string AscensionLabel = "SAVE_EDIT.ascensionLabel";
		public const string PlayersHeader = "SAVE_EDIT.playersHeader";
		public const string AddPlayer = "SAVE_EDIT.addPlayer";
		public const string RemovePlayer = "SAVE_EDIT.removePlayer";
		public const string RewardsHeader = "SAVE_EDIT.rewardsHeader";
		public const string RewardsHeaderEmpty = "SAVE_EDIT.rewardsHeaderEmpty";
		public const string AddReward = "SAVE_EDIT.addReward";
		public const string RemoveReward = "SAVE_EDIT.removeReward";
		public const string RewardOptionCount = "SAVE_EDIT.rewardOptionCount";
		public const string RewardGoldAmount = "SAVE_EDIT.rewardGoldAmount";
		public const string Apply = "SAVE_EDIT.apply";
		public const string Cancel = "SAVE_EDIT.cancel";
		public const string AppliedSuccess = "SAVE_EDIT.appliedSuccess";
		public const string AppliedFailed = "SAVE_EDIT.appliedFailed";

		// AddPlayer flow
		public const string AddPlayerModeLabel = "SAVE_EDIT.addPlayerModeLabel";
		public const string AddPlayerModeClone = "SAVE_EDIT.addPlayerMode.clone";
		public const string AddPlayerModeFromEmpty = "SAVE_EDIT.addPlayerMode.fromEmpty";
		public const string AddPlayerSourceLabel = "SAVE_EDIT.addPlayerSourceLabel";
		public const string AddPlayerNetIdLabel = "SAVE_EDIT.addPlayerNetIdLabel";
		public const string AddPlayerCharacterLabel = "SAVE_EDIT.addPlayerCharacterLabel";

		// AutoReward dialog
		public const string AutoRewardTitle = "SAVE_EDIT.autoReward.title";
		public const string AutoRewardHint = "SAVE_EDIT.autoReward.hint";
		public const string AutoRewardApply = "SAVE_EDIT.autoReward.apply";
		public const string AutoRewardSkip = "SAVE_EDIT.autoReward.skip";
		public const string AutoRewardColCard = "SAVE_EDIT.autoReward.col.card";
		public const string AutoRewardColRemove = "SAVE_EDIT.autoReward.col.remove";
		public const string AutoRewardColRelic = "SAVE_EDIT.autoReward.col.relic";
		public const string AutoRewardRowNormal = "SAVE_EDIT.autoReward.row.normal";
		public const string AutoRewardRowElite = "SAVE_EDIT.autoReward.row.elite";
		public const string AutoRewardRowBoss = "SAVE_EDIT.autoReward.row.boss";
		public const string AutoRewardRowEvent = "SAVE_EDIT.autoReward.row.event";
		public const string AutoRewardRowAncient = "SAVE_EDIT.autoReward.row.ancient";
		public const string AutoRewardRowTreasure = "SAVE_EDIT.autoReward.row.treasure";
		public const string AutoRewardRowShop = "SAVE_EDIT.autoReward.row.shop";

		// CharacterPickerDialog
		public const string PickCharacter = "SAVE_EDIT.pickCharacter";

		// PlayerPickerDialog
		public const string PlayerPickerTitle = "SAVE_EDIT.playerPicker.title";
		public const string PlayerPickerSteamHeader = "SAVE_EDIT.playerPicker.steamHeader";
		public const string PlayerPickerPartnersHeader = "SAVE_EDIT.playerPicker.partnersHeader";
		public const string PlayerPickerManualHeader = "SAVE_EDIT.playerPicker.manualHeader";
		public const string PlayerPickerManualPlaceholder = "SAVE_EDIT.playerPicker.manualPlaceholder";
		public const string PlayerPickerScan = "SAVE_EDIT.playerPicker.scan";
		public const string PlayerPickerStopScan = "SAVE_EDIT.playerPicker.stopScan";
		public const string PlayerPickerScanning = "SAVE_EDIT.playerPicker.scanning";
		public const string PlayerPickerScanDone = "SAVE_EDIT.playerPicker.scanDone";
		public const string PlayerPickerScanCanceled = "SAVE_EDIT.playerPicker.scanCanceled";
		public const string PlayerPickerSteamUnavailable = "SAVE_EDIT.playerPicker.steamUnavailable";
		public const string PlayerPickerPlayingNow = "SAVE_EDIT.playerPicker.playingNow";
		public const string PlayerPickerAlreadyInSave = "SAVE_EDIT.playerPicker.alreadyInSave";
		public const string PlayerPickerSearchPlaceholder = "SAVE_EDIT.playerPicker.searchPlaceholder";
	}
}
