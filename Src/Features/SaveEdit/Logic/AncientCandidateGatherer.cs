using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace NyMod.Saves.Features.SaveEdit.Logic;

/// <summary>
/// Inspects a <see cref="SerializableRun"/>'s map history to determine which ancients the team
/// visited per act, then resolves each act's <c>AncientId</c> to an <see cref="AncientEventModel"/>
/// and pulls every relic id reachable from that ancient's options. Used to seed the ancient
/// auto-reward pre-roll so a freshly added player gets an ancient-flavored relic strictly drawn
/// from the specific ancient that was actually visited.
/// </summary>
internal static class AncientCandidateGatherer
{
	/// <summary>
	/// Returns one candidate relic-id pool per ancient room visited (in visit order), where
	/// each pool contains only the relic ids reachable from the specific ancient at that
	/// visit. The total visit count equals <c>PoolsPerVisit.Count</c>.
	/// </summary>
	public static (IReadOnlyList<IReadOnlyList<ModelId>> PoolsPerVisit, int VisitCount) Gather(SerializableRun run)
	{
		List<IReadOnlyList<ModelId>> pools = new List<IReadOnlyList<ModelId>>();
		if (run?.MapPointHistory == null || run.Acts == null) return (pools, 0);

		int actIndex = -1;
		foreach (List<MapPointHistoryEntry> floor in run.MapPointHistory)
		{
			actIndex++;
			if (floor == null) continue;
			int visitsInAct = 0;
			foreach (MapPointHistoryEntry entry in floor)
			{
				if (entry == null) continue;
				if (entry.MapPointType != MapPointType.Ancient) continue;
				if (entry.Rooms == null || entry.Rooms.Count == 0) continue;
				visitsInAct += entry.Rooms.Count;
			}
			if (visitsInAct == 0) continue;
			if (actIndex < 0 || actIndex >= run.Acts.Count) continue;

			ModelId? ancientId = run.Acts[actIndex]?.SerializableRooms?.AncientId;
			IReadOnlyList<ModelId> pool = ResolvePoolFor(ancientId);
			for (int i = 0; i < visitsInAct; i++)
			{
				pools.Add(pool);
			}
		}

		return (pools, pools.Count);
	}

	private static IReadOnlyList<ModelId> ResolvePoolFor(ModelId? ancientId)
	{
		if (ancientId == null || ancientId == ModelId.none) return System.Array.Empty<ModelId>();

		AncientEventModel? ancient;
		try
		{
			ancient = ModelDb.GetById<AncientEventModel>(ancientId);
		}
		catch
		{
			return System.Array.Empty<ModelId>();
		}
		if (ancient == null) return System.Array.Empty<ModelId>();

		List<ModelId> ids = new List<ModelId>();
		HashSet<string> seen = new HashSet<string>();
		foreach (MegaCrit.Sts2.Core.Events.EventOption option in ancient.AllPossibleOptions)
		{
			RelicModel? relic = option?.Relic;
			if (relic == null || relic.Id == null) continue;
			string key = relic.Id.ToString() ?? string.Empty;
			if (key.Length == 0) continue;
			if (seen.Add(key))
			{
				ids.Add(relic.Id);
			}
		}
		return ids;
	}
}
