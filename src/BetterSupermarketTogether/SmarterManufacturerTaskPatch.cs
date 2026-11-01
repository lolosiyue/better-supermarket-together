using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(NPC_Manager), "ReturnWeightedManufacturerTask")]
internal static class SmarterManufacturerTaskPatch
{
	private readonly struct RowCandidate(int shelfIdx, int shelfPair, int productId, int count, int maxPerRow, string combinable)
	{
		public readonly int ShelfIdx = shelfIdx;

		public readonly int ShelfPair = shelfPair;

		public readonly int ProductId = productId;

		public readonly int Count = count;

		public readonly int MaxPerRow = maxPerRow;

		public readonly string Combinable = combinable;
	}

	private static bool _warned;

	private static bool Prefix(NPC_Manager __instance, GameObject employeeRequesterOBJ, NPC_Info npcInfo, ref int[] __result)
	{
		if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled)
		{
			return true;
		}
		int[] array = null;
		try
		{
			array = SelectTask(__instance, employeeRequesterOBJ, npcInfo);
		}
		catch (Exception ex)
		{
			if (!_warned)
			{
				BetterSupermarketPlugin.Log.LogWarning("SmartManufacture failed, falling back to vanilla task selection: " + ex.Message);
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
        RestockAssignments.GuardAssignment(__instance, employeeRequesterOBJ, true, ref __result);
    }

    private static int[] SelectTask(NPC_Manager npc, GameObject requester, NPC_Info requesterInfo)
    {
        if (npc.manufacturingShelvesOBJ == null || npc.manufacturingStorageShelvesOBJ == null || npc.employeeParentOBJ == null)
            return RestockAssignments.NoTask();
        var route = new EmployeeNavigation(requester);
        if (!route.Ready) return RestockAssignments.NoTask();
        RestockAssignments.Collect(npc, requester, true, out var occupied, out var reserved);
        var rows = new List<RowCandidate>();
        var shelves = npc.manufacturingShelvesOBJ.transform;
        // Read current rows directly: vanilla lists are partial and may be five seconds old.
        for (int shelf = 0; shelf < shelves.childCount; shelf++)
        {
            if (occupied.Contains(shelf)) continue;
            var target = shelves.GetChild(shelf).GetComponent<ManufacturingContainer>();
            if (target == null || target.productInfoArray == null || target.standSpot == null) continue;
            var contents = target.productInfoArray;
            for (int pair = 0; pair + 1 < contents.Length; pair += 2)
            {
                int product = contents[pair], count = contents[pair + 1];
                if (product < 0 || count < 0) continue;
                int capacity = npc.GetMaxManufacturingProductsPerRow(shelf, product);
                if (capacity <= 0 || count >= capacity || !route.CanReach(target.standSpot.transform.position)) continue;
                if (target.combinableInfoArray == null || pair / 2 >= target.combinableInfoArray.Length) continue;
                string recipe = RepairPolicies.NormalizeRecipe(target.combinableInfoArray[pair / 2]);
                if (recipe == null) continue;
                rows.Add(new RowCandidate(shelf, pair, product, count, capacity, recipe));
            }
        }
        rows.Sort((a, b) =>
        {
            int ratio = ((float)a.Count / a.MaxPerRow).CompareTo((float)b.Count / b.MaxPerRow);
            if (ratio != 0) return ratio;
            int count = a.Count.CompareTo(b.Count);
            return count != 0 ? count : a.ShelfIdx.CompareTo(b.ShelfIdx);
        });
        var warehouse = npc.manufacturingStorageShelvesOBJ.transform;
        foreach (var row in rows)
        {
            int sourceShelf = -1, sourcePair = -1, smallestBox = int.MaxValue;
            var destination = shelves.GetChild(row.ShelfIdx).GetComponent<ManufacturingContainer>().standSpot.transform.position;
            for (int shelf = 0; shelf < warehouse.childCount; shelf++)
            {
                var source = warehouse.GetChild(shelf).GetComponent<ManufacturingContainer>();
                if (source == null || source.productInfoArray == null || source.standSpot == null) continue;
                var contents = source.productInfoArray;
                for (int pair = 0; pair + 1 < contents.Length; pair += 2)
                {
                    int count = contents[pair + 1];
                    if (contents[pair] != row.ProductId || count <= 0 || count >= smallestBox ||
                        reserved.Contains(SmarterRestockerTaskPatch.SlotKey(shelf, pair))) continue;
                    if (source.combinableInfoArray == null || pair / 2 >= source.combinableInfoArray.Length ||
                        RepairPolicies.NormalizeRecipe(source.combinableInfoArray[pair / 2]) != row.Combinable) continue;
                    if (!route.CanTravel(source.standSpot.transform.position, destination)) continue;
                    sourceShelf = shelf;
                    sourcePair = pair;
                    smallestBox = count;
                }
            }
            if (sourceShelf < 0) continue;
            // Vanilla pickup compares the exact stored string; normalize only for matching.
            if (requesterInfo != null)
                requesterInfo.packagingAssignedOrderData = warehouse.GetChild(sourceShelf).GetComponent<ManufacturingContainer>().combinableInfoArray[sourcePair / 2];
            return new[] { row.ShelfIdx, row.ShelfPair, sourceShelf, sourcePair, row.ProductId, row.ProductId };
        }
        return RestockAssignments.NoTask();
    }

}
