using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class WarehouseConsolidator
{
	[HarmonyPatch(typeof(NPC_Manager), "GetFreeManufacturingStorageContainer")]
	internal static class SmartManufacturingRackPlacementPatch
	{
		private static bool Warned;

		private static bool Prefix(NPC_Manager __instance, int boxIDProduct, string combinableData, ref int __result)
		{
			if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled)
			{
				return true;
			}
			if (boxIDProduct < 0)
			{
				return true;
			}
			try
			{
				Transform transform = __instance.manufacturingStorageShelvesOBJ.transform;
				if (transform.childCount == 0)
				{
					return true;
				}
				int num = -1;
				int num2 = int.MaxValue;
				float num3 = float.MaxValue;
				for (int i = 0; i < transform.childCount; i++)
				{
					ManufacturingContainer component = transform.GetChild(i).GetComponent<ManufacturingContainer>();
					if (component == null || component.productInfoArray == null || component.combinableInfoArray == null)
					{
						continue;
					}
					int[] productInfoArray = component.productInfoArray;
					Transform transform2 = transform.GetChild(i).transform.Find("BoxContainer");
					int num4 = productInfoArray.Length / 2;
					int num5 = 0;
					bool flag = false;
					for (int j = 0; j < num4; j++)
					{
						int num6 = productInfoArray[j * 2];
						int num7 = productInfoArray[j * 2 + 1];
						string text = component.combinableInfoArray[j];
						bool flag2 = transform2 == null || transform2.GetChild(j).childCount <= 0;
						if (num6 == boxIDProduct && text == combinableData)
						{
							if (num7 > 0)
							{
								num5 += num7;
							}
							if ((num7 <= 0) & flag2)
							{
								flag = true;
							}
						}
						else if ((num6 == -1) & flag2)
						{
							flag = true;
						}
					}
					if ((num5 != 0 || HasLabeledEmptyRow(productInfoArray, transform2, num4, boxIDProduct, combinableData)) && flag)
					{
						float num8 = Vector3.Distance(__instance.storageDistanceReferencePoint, transform.GetChild(i).position);
						if (num5 < num2 || (num5 == num2 && num8 < num3))
						{
							num2 = num5;
							num3 = num8;
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
				if (!Warned)
				{
					Warned = true;
					BetterSupermarketPlugin.Log.LogWarning("SmartManufacturingRackPlacement failed, falling back to vanilla: " + ex.Message);
				}
				return true;
			}
		}

		private static bool HasLabeledEmptyRow(int[] info, Transform boxContainer, int pairs, int productId, string combinableData)
		{
			if (boxContainer == null)
			{
				return false;
			}
			for (int i = 0; i < pairs; i++)
			{
				if (info[i * 2] == productId && info[i * 2 + 1] <= 0 && boxContainer.GetChild(i).childCount <= 0)
				{
					return true;
				}
			}
			return false;
		}
	}

	public static void Run(NPC_Manager npc, int maxMoves)
	{
		if (npc.storageOBJ == null || npc.employeeParentOBJ == null)
		{
			return;
		}
		Transform transform = npc.storageOBJ.transform;
		int childCount = transform.childCount;
		if (childCount == 0)
		{
			return;
		}
		HashSet<long> hashSet = new HashSet<long>();
		HashSet<int> hashSet2 = new HashSet<int>();
		Transform transform2 = npc.employeeParentOBJ.transform;
		for (int i = 0; i < transform2.childCount; i++)
		{
			NPC_Info component = transform2.GetChild(i).GetComponent<NPC_Info>();
			if (component == null)
			{
				continue;
			}
			if (component.taskPriority == 2)
			{
				int[] productAvailableArray = component.productAvailableArray;
				if (productAvailableArray != null && productAvailableArray.Length >= 6 && productAvailableArray[0] != -1 && productAvailableArray[2] != -1)
				{
					hashSet.Add(SmarterRestockerTaskPatch.SlotKey(productAvailableArray[2], productAvailableArray[3]));
				}
			}
			if (component.taskPriority == 3 && component.currentFreeStorageIndex >= 0)
			{
				hashSet2.Add(component.currentFreeStorageIndex);
			}
		}
		Dictionary<int, List<int[]>> dictionary = new Dictionary<int, List<int[]>>();
		Data_Container[] array = new Data_Container[childCount];
		for (int j = 0; j < childCount; j++)
		{
			Data_Container data_Container = (array[j] = transform.GetChild(j).GetComponent<Data_Container>());
			if (data_Container == null || data_Container.productInfoArray == null)
			{
				continue;
			}
			int[] productInfoArray = data_Container.productInfoArray;
			int num = productInfoArray.Length / 2;
			for (int k = 0; k < num; k++)
			{
				int num2 = productInfoArray[k * 2];
				if (num2 >= 0)
				{
					if (!dictionary.TryGetValue(num2, out var value))
					{
						value = (dictionary[num2] = new List<int[]>());
					}
					value.Add(new int[3]
					{
						j,
						k * 2,
						productInfoArray[k * 2 + 1]
					});
				}
			}
		}
		int num3 = 0;
		foreach (KeyValuePair<int, List<int[]>> item in dictionary)
		{
			if (num3 >= maxMoves)
			{
				break;
			}
			int key = item.Key;
			List<int[]> value2 = item.Value;
			int num4 = -1;
			int num5 = 0;
			float num6 = float.MaxValue;
			for (int l = 0; l < childCount; l++)
			{
				int num7 = 0;
				foreach (int[] item2 in value2)
				{
					if (item2[0] == l)
					{
						num7++;
					}
				}
				if (num7 != 0)
				{
					float num8 = Vector3.Distance(npc.storageDistanceReferencePoint, transform.GetChild(l).position);
					if (num7 > num5 || (num7 == num5 && num8 < num6))
					{
						num5 = num7;
						num6 = num8;
						num4 = l;
					}
				}
			}
			if (num4 < 0 || hashSet2.Contains(num4))
			{
				continue;
			}
			Data_Container data_Container2 = array[num4];
			if (data_Container2 == null)
			{
				continue;
			}
			foreach (int[] item3 in value2)
			{
				if (num3 >= maxMoves)
				{
					break;
				}
				int num9 = item3[0];
				int num10 = item3[1];
				int num11 = item3[2];
				if (num9 != num4 && num11 > 0 && !hashSet2.Contains(num9) && !hashSet.Contains(SmarterRestockerTaskPatch.SlotKey(num9, num10)))
				{
					int num12 = WarehouseOptimizer.FindFreeRow(data_Container2, key, hashSet, num4);
					if (num12 < 0)
					{
						break;
					}
					Data_Container data_Container3 = array[num9];
					if (!(data_Container3 == null))
					{
						WarehouseOptimizer.MoveBox(data_Container3, num10, data_Container2, num12, key, num11);
						hashSet.Add(SmarterRestockerTaskPatch.SlotKey(num4, num12 * 2));
						num3++;
					}
				}
			}
		}
		if (num3 > 0)
		{
			BetterSupermarketPlugin.Log.LogInfo($"Warehouse consolidation: re-homed {num3} box(es) into their product groups.");
		}
	}
}
