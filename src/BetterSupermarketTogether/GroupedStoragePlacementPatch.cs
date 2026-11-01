using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(NPC_Manager), "GetFreeStorageContainer")]
internal static class GroupedStoragePlacementPatch
{
	private static bool Prefix(NPC_Manager __instance, int boxIDProduct, ref int __result)
	{
		if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.GroupPlacementEnabled)
		{
			return true;
		}
		if (boxIDProduct < 0)
		{
			return true;
		}
		try
		{
			Transform transform = __instance.storageOBJ.transform;
			if (transform.childCount == 0)
			{
				return true;
			}
			int num = -1;
			float num2 = float.MaxValue;
			for (int i = 0; i < transform.childCount; i++)
			{
				Data_Container component = transform.GetChild(i).GetComponent<Data_Container>();
				if (component == null || component.productInfoArray == null)
				{
					continue;
				}
				int[] productInfoArray = component.productInfoArray;
				bool flag = false;
				int num3 = productInfoArray.Length / 2;
				for (int j = 0; j < num3; j++)
				{
					if (productInfoArray[j * 2] == boxIDProduct)
					{
						flag = true;
						break;
					}
				}
				if (flag && WarehouseOptimizer.FindFreeRow(component, boxIDProduct, new HashSet<long>(), i) >= 0)
				{
					float num4 = Vector3.Distance(__instance.storageDistanceReferencePoint, transform.GetChild(i).position);
					if (num4 < num2)
					{
						num2 = num4;
						num = i;
					}
				}
			}
			if (num < 0)
			{
				return true;
			}
			__result = num;
			return false;
		}
		catch (Exception ex)
		{
			if (!WarehouseOptimizer.PatchWarned)
			{
				WarehouseOptimizer.PatchWarned = true;
				BetterSupermarketPlugin.Log.LogWarning("GroupedStoragePlacement failed, falling back to vanilla: " + ex.Message);
			}
			return true;
		}
	}
}
