using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;
using Steamworks;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Read-only enumeration of the local Steam user's friends list, sorted with friends
/// currently playing STS2 first, then by online state, then by name. Falls back gracefully
/// when Steamworks is unavailable (e.g. running offline or under a non-Steam launch).
/// </summary>
internal sealed class SteamFriendsProvider
{
	// Steam app id for Slay the Spire 2.
	private const ulong Sts2AppId = 2868840UL;

	public readonly struct FriendInfo
	{
		public FriendInfo(ulong steamId, string name, EPersonaState state, bool isPlayingSts2)
		{
			SteamId = steamId;
			Name = name;
			State = state;
			IsPlayingSts2 = isPlayingSts2;
		}

		public ulong SteamId { get; }
		public string Name { get; }
		public EPersonaState State { get; }
		public bool IsPlayingSts2 { get; }
	}

	public bool TryGetFriends(out IReadOnlyList<FriendInfo> friends)
	{
		friends = Array.Empty<FriendInfo>();
		try
		{
			int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
			if (count <= 0)
			{
				return true;
			}

			List<FriendInfo> list = new List<FriendInfo>(count);
			for (int i = 0; i < count; i++)
			{
				CSteamID id;
				try
				{
					id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
				}
				catch (Exception ex)
				{
					Log.Warn($"NyMod.Saves SteamFriendsProvider: GetFriendByIndex({i}) failed: {ex.Message}");
					continue;
				}

				string name;
				try
				{
					name = SteamFriends.GetFriendPersonaName(id) ?? id.m_SteamID.ToString();
				}
				catch
				{
					name = id.m_SteamID.ToString();
				}

				EPersonaState state;
				try
				{
					state = SteamFriends.GetFriendPersonaState(id);
				}
				catch
				{
					state = EPersonaState.k_EPersonaStateOffline;
				}

				bool playingSts2 = false;
				try
				{
					if (SteamFriends.GetFriendGamePlayed(id, out FriendGameInfo_t info))
					{
						playingSts2 = info.m_gameID.m_GameID == Sts2AppId;
					}
				}
				catch
				{
					// ignore — treat as not in game
				}

				list.Add(new FriendInfo(id.m_SteamID, name, state, playingSts2));
			}

			list.Sort(static (a, b) =>
			{
				if (a.IsPlayingSts2 != b.IsPlayingSts2)
				{
					return a.IsPlayingSts2 ? -1 : 1;
				}

				int rankA = StateRank(a.State);
				int rankB = StateRank(b.State);
				if (rankA != rankB)
				{
					return rankA.CompareTo(rankB);
				}

				return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
			});

			friends = list;
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves SteamFriendsProvider: enumeration failed: {ex.Message}");
			return false;
		}
	}

	public bool TryGetLocalSteamId(out ulong steamId)
	{
		steamId = 0UL;
		try
		{
			steamId = SteamUser.GetSteamID().m_SteamID;
			return steamId != 0UL;
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves SteamFriendsProvider: GetSteamID failed: {ex.Message}");
			return false;
		}
	}

	private Dictionary<ulong, string>? _nameCache;

	/// <summary>
	/// Resolve a SteamID64 to a display name. Returns true with the local
	/// user's persona name when <paramref name="steamId"/> is the local user,
	/// the cached friend persona name for any friend, or false otherwise.
	/// Steamworks does not allow synchronous lookups for non-friends, so we
	/// don't attempt to resolve arbitrary remote ids.
	/// </summary>
	public bool TryResolveName(ulong steamId, out string name)
	{
		name = string.Empty;
		if (steamId == 0UL)
		{
			return false;
		}

		try
		{
			if (TryGetLocalSteamId(out ulong localId) && localId == steamId)
			{
				string self = SteamFriends.GetPersonaName();
				if (!string.IsNullOrEmpty(self))
				{
					name = self;
					return true;
				}
			}

			Dictionary<ulong, string> cache = _nameCache ??= BuildFriendNameCache();
			return cache.TryGetValue(steamId, out name!);
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves SteamFriendsProvider: TryResolveName({steamId}) failed: {ex.Message}");
			return false;
		}
	}

	/// <summary>Drop the friend-name cache so the next lookup re-enumerates.</summary>
	public void InvalidateNameCache() => _nameCache = null;

	private static Dictionary<ulong, string> BuildFriendNameCache()
	{
		Dictionary<ulong, string> map = new Dictionary<ulong, string>();
		try
		{
			int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
			for (int i = 0; i < count; i++)
			{
				CSteamID id;
				try { id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate); }
				catch { continue; }

				string name;
				try { name = SteamFriends.GetFriendPersonaName(id) ?? string.Empty; }
				catch { continue; }

				if (!string.IsNullOrEmpty(name))
				{
					map[id.m_SteamID] = name;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"NyMod.Saves SteamFriendsProvider: BuildFriendNameCache failed: {ex.Message}");
		}
		return map;
	}

	private static int StateRank(EPersonaState state)
	{
		// Lower = closer to "online and reachable".
		return state switch
		{
			EPersonaState.k_EPersonaStateOnline => 0,
			EPersonaState.k_EPersonaStateLookingToPlay => 1,
			EPersonaState.k_EPersonaStateLookingToTrade => 2,
			EPersonaState.k_EPersonaStateAway => 3,
			EPersonaState.k_EPersonaStateBusy => 4,
			EPersonaState.k_EPersonaStateSnooze => 5,
			EPersonaState.k_EPersonaStateOffline => 6,
			_ => 7,
		};
	}
}
