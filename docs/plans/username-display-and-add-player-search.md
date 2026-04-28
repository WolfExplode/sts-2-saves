# Username display + Add-Player search filter

## Goal summary

In the Save Edit dialog, replace the bare numeric NetId shown in the Players
section with the player's Steam username, and label each row with the
localized character name in the form `{LocalizedCharacterName} ({Username})`.
In the Add Player picker, add a search/filter text box that filters the Steam
friends list and the scanned-partners list by name as the user types.

## Sub-goals

1. **Resolve username for an arbitrary SteamID64**: extend
   `SteamFriendsProvider` with a `TryResolveName(ulong steamId, out string)`
   that returns the local Steam user's name for self, the friend persona name
   for a friend, or `false` when Steamworks is unavailable / the id is not in
   the friends list.
2. **Resolve localized character name from a `ModelId`**: add a tiny helper
   (`CharacterDisplayHelper.LocalizedName(ModelId?)`) that looks up the
   `CharacterModel` from `ModelDb` and returns its `Title` LocString resolved
   to the active language, falling back to the raw entry id when missing.
3. **Save data fallback**: there is no per-player username stored in
   `SerializablePlayer`, so "save data" can't supply one directly. Treat the
   NetId itself as the only save-data source — use it as the deterministic
   fallback when Steam is unavailable. (No additional save schema work.)
4. **Update Players section rendering** in `SaveEditDialog.RebuildPlayersUi`
   / `AddPlayerRow` to display
   `{LocalizedCharacterName} ({Username})` instead of `{rawCharId} [{netId}]`.
   Keep the raw NetId as a tooltip on the row label so power users can still
   copy it. Mirror the same change for `(added)` rows where the character
   override is known.
5. **Add search filter to PlayerPickerDialog**: insert a `LineEdit` at the
   top of the dialog that re-populates both the friends and partners
   `ItemList`s in real time with case-insensitive substring matching against
   the visible name. Preserve current sorting and the existing "already in
   save / playing now" suffixes.

## Technical implementation

### Sub-goal 1 — `SteamFriendsProvider.TryResolveName`

File: `Src/Features/SaveEdit/Logic/SteamFriendsProvider.cs`

- Cache the local user's `CSteamID` via `SteamUser.GetSteamID()` once per
  successful Steamworks call.
- Implement:
  ```csharp
  public bool TryResolveName(ulong steamId, out string name)
  ```
  - If `steamId == localSteamId` → `SteamFriends.GetPersonaName()`.
  - Else iterate `EFriendFlags.k_EFriendFlagImmediate` once and build a
    cached `Dictionary<ulong,string>` (lazy + invalidated never — friends
    list rarely changes during a single session; safe).
  - Return `false` when Steamworks throws or the id isn't found.
- Wrap every Steamworks call in try/catch as the existing `TryGetFriends`
  already does.

### Sub-goal 2 — `CharacterDisplayHelper`

New file: `Src/Features/SaveEdit/Logic/CharacterDisplayHelper.cs`

```csharp
internal static class CharacterDisplayHelper
{
    public static string LocalizedName(ModelId? characterId)
    {
        if (characterId == null || characterId == ModelId.none) return "?";
        CharacterModel? cm = ModelDb.GetOrNull<CharacterModel>(characterId);
        if (cm == null) return characterId.Entry;
        try { return cm.Title?.ToString() ?? characterId.Entry; }
        catch { return characterId.Entry; }
    }
}
```

Verify the `ModelDb.GetOrNull<T>` (or equivalent) API exists in both
v0.103.2 and v0.104.0 decompile sources before committing the helper.

### Sub-goal 3 — Save-data fallback

Use `netId.ToString()` (`"76561…"`) as the username when Steam can't resolve.
Document the choice in a code comment so future readers don't grep for a
"PlayerName" field that doesn't exist.

### Sub-goal 4 — `SaveEditDialog` players section

File: `Src/Features/SaveEdit/Presentation/SaveEditDialog.cs`

- Change `AddPlayerRow(ulong netId, string charLabel, bool isPending)` to
  `AddPlayerRow(ulong netId, ModelId? characterId, bool isPending,
  string? suffix = null)`.
- Inside, build display text:
  `{CharacterDisplayHelper.LocalizedName(characterId)} ({usernameOrNetId})
  {suffix?}`
  where `usernameOrNetId = ServiceRegistry.SteamFriendsProvider
  .TryResolveName(netId, out string n) ? n : netId.ToString()`.
- Set `label.TooltipText = netId.ToString();` so the raw NetId remains
  recoverable.
- Update `RebuildPlayersUi` callers (existing + added players) to pass the
  `ModelId?` directly and a `(added)` suffix when pending.

### Sub-goal 5 — Player picker filter

File: `Src/Features/SaveEdit/Presentation/PlayerPickerDialog.cs`

- Add a `LineEdit _filter` at the very top of `BuildUi` (before friends
  header), with placeholder text from a new
  `SaveEditUiText.Keys.PlayerPickerSearchPlaceholder`.
- Hook `_filter.TextChanged += _ => ApplyFilter();`.
- Refactor `PopulateFriends` to (a) build a snapshot
  `List<(FriendInfo info, string suffix, bool excluded)>` once and (b) call
  `RenderFriends(_filter.Text)` which clears `_friendsList` and re-adds
  filtered rows. Keep the same suffix/disabled logic.
- Mirror for partners: store harvested entries into a
  `List<(string label, ulong netId)>` and render filtered.
- Filter is case-insensitive substring on the display name (not the suffix).

### Localization keys to add

In `SaveEditUiText.Keys`:
- `PlayerPickerSearchPlaceholder`

Strings (en + zh-CN) per existing pattern in
`Src/Infrastructure/Localization/SaveEditUiText.cs` and any per-language
resource files.

## Automated testing options

- `cd d:\Projects\Sts2Mods; .\scripts\Build-AllVersions.ps1 -Mods STS2Saves`
  must succeed for both v0.103.2 and v0.104.0.
- No unit-test infra in the mod; rely on build + manual run.

## Manual testing checklist

1. Launch game with mod installed → main menu → Singleplayer → Load → pick a
   run → Edit Save → confirm Players section shows
   `Watcher (PlayerName)` instead of `watcher [76561...]`. Tooltip on the
   label should still show the SteamID64.
2. Multiplayer → Load → pick a saved run → Edit Save → same expectation,
   showing remote player's Steam name when they are in your friends list,
   their NetId otherwise.
3. In Edit Save → Add Player → Steam friends list visible. Type part of a
   friend's name in the search box → list immediately narrows to matches;
   clearing the box restores the full list. Same with the partner-scan list.
4. Pick a filtered friend, confirm, save, reload — verify the new player
   row also displays the resolved username.
5. Run with Steam offline / no Steamworks → rows fall back gracefully to
   numeric NetIds; no exceptions in the godot.log.

## Risks & precautions

- `ModelDb.GetOrNull<CharacterModel>` API name may differ across versions —
  must verify in decompile sources before relying on it. If unavailable,
  iterate `ModelDb.AllCharacters` once and build a `Dictionary<ModelId,
  CharacterModel>` cache.
- `LocString.ToString()` may need a different accessor (e.g. `.LocalizedText`
  or `.GetLocalizedString()`); confirm against a working call site in
  decompile.
- Username resolution must NOT block on Steam network calls. Persona-name
  lookups for friends are cached locally by Steamworks and return instantly;
  for non-friends they would require an async `RequestUserInformation` round
  trip — out of scope, fall back to NetId.
- Adding `_filter` changes dialog layout; verify min-size still fits (bump
  `MinSize.Y` if needed).
