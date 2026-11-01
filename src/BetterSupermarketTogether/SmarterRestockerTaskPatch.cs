using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(NPC_Manager), "ReturnWeightedRestockerTask")]
internal static class SmarterRestockerTaskPatch
{
	private readonly struct RowCandidate(int shelfIdx, int shelfPair, int productId, int count, int maxPerRow)
	{
		public readonly int ShelfIdx = shelfIdx;

		public readonly int ShelfPair = shelfPair;

		public readonly int ProductId = productId;

		public readonly int Count = count;

		public readonly int MaxPerRow = maxPerRow;
	}

	private static bool _warned;

	private static bool Prefix(NPC_Manager __instance, GameObject employeeRequesterOBJ, ref int[] __result)
	{
		if (BetterSupermarketPlugin.EnablePlugin && CompanionWorkWorld.HasTarget(employeeRequesterOBJ?.GetComponent<NPC_Info>()))
		{
			__result = SelectTask(__instance, employeeRequesterOBJ);
			return false;
		}
		if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled)
		{
			return true;
		}
		int[] array = null;
		try
		{
			array = SelectTask(__instance, employeeRequesterOBJ);
		}
		catch (Exception ex)
		{
			if (!_warned)
			{
				BetterSupermarketPlugin.Log.LogWarning("SmartRestock failed, falling back to vanilla task selection: " + ex.Message);
				_warned = true;
			}
		}
		if (array == null)
		{
			return true;
		}
		__result = array;
		return false;
	}

    private static void Postfix(NPC_Manager __instance, GameObject employeeRequesterOBJ, ref int[] __result)
    {
        RestockAssignments.GuardAssignment(__instance, employeeRequesterOBJ, false, ref __result);
    }

    private static int[] SelectTask(NPC_Manager npc, GameObject requester)
    {
        if (npc.shelvesOBJ == null || npc.storageOBJ == null || npc.employeeParentOBJ == null)
            return RestockAssignments.NoTask();
        var route = new EmployeeNavigation(requester);
        if (!route.Ready) return RestockAssignments.NoTask();
        RestockAssignments.Collect(npc, requester, false, out var occupied, out var reserved);
        var rows = new List<RowCandidate>();
        var shelves = npc.shelvesOBJ.transform;
        // Read current rows directly: vanilla lists are partial and may be five seconds old.
        for (int shelf = 0; shelf < shelves.childCount; shelf++)
        {
            if (occupied.Contains(shelf)) continue;
            var target = shelves.GetChild(shelf).GetComponent<Data_Container>();
            if (target == null || target.productInfoArray == null || target.standSpot == null) continue;
            var contents = target.productInfoArray;
            for (int pair = 0; pair + 1 < contents.Length; pair += 2)
            {
                int product = contents[pair], count = contents[pair + 1];
                if (product < 0 || count < 0 || !CompanionWorkWorld.Accepts(requester.GetComponent<NPC_Info>(),target,pair,product)) continue;
                int capacity = npc.GetMaxProductsPerRow(shelf, product);
                if (capacity <= 0 || count >= capacity || !route.CanReach(target.standSpot.transform.position)) continue;
                rows.Add(new RowCandidate(shelf, pair, product, count, capacity));
            }
        }
        rows.Sort((a, b) =>
        {
            int ratio = ((float)a.Count / a.MaxPerRow).CompareTo((float)b.Count / b.MaxPerRow);
            if (ratio != 0) return ratio;
            int count = a.Count.CompareTo(b.Count);
            return count != 0 ? count : a.ShelfIdx.CompareTo(b.ShelfIdx);
        });
        var warehouse = npc.storageOBJ.transform;
        foreach (var row in rows)
        {
            int sourceShelf = -1, sourcePair = -1, smallestBox = int.MaxValue;
            var destination = shelves.GetChild(row.ShelfIdx).GetComponent<Data_Container>().standSpot.transform.position;
            for (int shelf = 0; shelf < warehouse.childCount; shelf++)
            {
                var source = warehouse.GetChild(shelf).GetComponent<Data_Container>();
                if (source == null || source.productInfoArray == null || source.standSpot == null) continue;
                var contents = source.productInfoArray;
                for (int pair = 0; pair + 1 < contents.Length; pair += 2)
                {
                    int count = contents[pair + 1];
                    if (contents[pair] != row.ProductId || count <= 0 || count >= smallestBox ||
                        reserved.Contains(SlotKey(shelf, pair))) continue;
                    if (!route.CanTravel(source.standSpot.transform.position, destination)) continue;
                    sourceShelf = shelf;
                    sourcePair = pair;
                    smallestBox = count;
                }
            }
            if (sourceShelf < 0) continue;
            return new[] { row.ShelfIdx, row.ShelfPair, sourceShelf, sourcePair, row.ProductId, row.ProductId };
        }
        return RestockAssignments.NoTask();
    }

	internal static void AddTasks(List<string> source, List<string> target)
	{
		if (source == null)
		{
			return;
		}
		foreach (string item in source)
		{
			target.Add(item);
		}
	}

	internal static long SlotKey(int a, int b)
	{
		return ((long)a << 32) | (uint)b;
	}
}
