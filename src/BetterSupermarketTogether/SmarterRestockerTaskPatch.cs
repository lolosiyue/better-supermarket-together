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
		{
			return RestockAssignments.NoTask();
		}
		List<string> list = new List<string>();
		AddTasks(npc.lowProductCountList, list);
		AddTasks(npc.mediumProductCountList, list);
		AddTasks(npc.highProductCountList, list);
		AddTasks(npc.veryHighProductCountList, list);
		if (list.Count == 0)
		{
			return RestockAssignments.NoTask();
		}
        RestockAssignments.Collect(npc, requester, false, out var occupiedShelves, out var reservedSources);
		List<RowCandidate> list2 = new List<RowCandidate>();
		HashSet<long> hashSet3 = new HashSet<long>();
		Transform transform2 = npc.shelvesOBJ.transform;
		foreach (string item in list)
		{
			string[] array = item.Split(new char[1] { '|' });
			if (array.Length < 6 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2) || !int.TryParse(array[4], out var result3) || !hashSet3.Add(SlotKey(result, result2)) || occupiedShelves.Contains(result) || result < 0 || result >= transform2.childCount)
			{
				continue;
			}
			Data_Container component2 = transform2.GetChild(result).GetComponent<Data_Container>();
			if (component2 == null || component2.productInfoArray == null)
			{
				continue;
			}
			int[] productInfoArray = component2.productInfoArray;
			if (result2 >= 0 && result2 + 1 < productInfoArray.Length && productInfoArray[result2] == result3)
			{
				int count = productInfoArray[result2 + 1];
				int num = npc.GetMaxProductsPerRow(result, result3);
				if (num <= 0)
				{
					num = 1;
				}
				list2.Add(new RowCandidate(result, result2, result3, count, num));
			}
		}
		if (list2.Count == 0)
		{
			return RestockAssignments.NoTask();
		}
		list2.Sort((RowCandidate a, RowCandidate b) =>
		{
			int num8 = ((float)a.Count / (float)a.MaxPerRow).CompareTo((float)b.Count / (float)b.MaxPerRow);
			if (num8 != 0)
			{
				return num8;
			}
			int count2 = a.Count;
			num8 = count2.CompareTo(b.Count);
			if (num8 != 0)
			{
				return num8;
			}
			count2 = a.ShelfIdx;
			return count2.CompareTo(b.ShelfIdx);
		});
		Transform transform3 = npc.storageOBJ.transform;
		foreach (RowCandidate item2 in list2)
		{
			int num2 = -1;
			int num3 = -1;
			int num4 = int.MaxValue;
			for (int num5 = 0; num5 < transform3.childCount; num5++)
			{
				Data_Container component3 = transform3.GetChild(num5).GetComponent<Data_Container>();
				if (component3 == null || component3.productInfoArray == null)
				{
					continue;
				}
				int[] productInfoArray2 = component3.productInfoArray;
				for (int num6 = 0; num6 + 1 < productInfoArray2.Length; num6 += 2)
				{
					if (productInfoArray2[num6] != item2.ProductId)
					{
						continue;
					}
					int num7 = productInfoArray2[num6 + 1];
					if (num7 > 0 && !reservedSources.Contains(SlotKey(num5, num6)) && num7 < num4)
					{
						num4 = num7;
						num2 = num5;
						num3 = num6;
						if (num7 == 1)
						{
							break;
						}
					}
				}
			}
			if (num2 >= 0)
			{
				return new int[6] { item2.ShelfIdx, item2.ShelfPair, num2, num3, item2.ProductId, item2.ProductId };
			}
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
