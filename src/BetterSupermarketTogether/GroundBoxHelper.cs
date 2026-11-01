using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BetterSupermarketTogether;

internal static class GroundBoxHelper
{
	internal static bool Warned;

	internal static void Warn(Exception e, string patch)
	{
		if (!Warned)
		{
			Warned = true;
			BetterSupermarketPlugin.Log.LogWarning("SmartGroundBox (" + patch + ") failed, falling back to vanilla: " + e.Message);
		}
	}

	internal static GameObject Select(NPC_Manager npc, GameObject requester, bool manufacturing, bool storableOnly)
	{
		Transform transform = (manufacturing ? npc.manufacturingBoxesOBJ : npc.boxesOBJ).transform;
		if (transform == null || transform.childCount == 0)
		{
			return null;
		}
		HashSet<GameObject> hashSet = new HashSet<GameObject>();
		Transform transform2 = npc.employeeParentOBJ.transform;
		for (int i = 0; i < transform2.childCount; i++)
		{
			NPC_Info component = transform2.GetChild(i).GetComponent<NPC_Info>();
			if (!(component == null) && !(component.gameObject == requester) && component.randomBox != null)
			{
				hashSet.Add(component.randomBox);
			}
		}
		HashSet<int> hashSet2 = null;
		HashSet<string> hashSet3 = null;
		if (storableOnly)
		{
			if (manufacturing)
			{
				hashSet3 = CollectFreeManufacturingSlots(npc);
			}
			else
			{
				hashSet2 = CollectFreeStorageSlots(npc);
			}
			if (hashSet2 == null && hashSet3 == null)
			{
				return null;
			}
		}
		GameObject result = null;
		int num = int.MaxValue;
		for (int j = 0; j < transform.childCount; j++)
		{
			GameObject gameObject = transform.GetChild(j).gameObject;
			if (hashSet.Contains(gameObject))
			{
				continue;
			}
			int numberOfProducts;
			if (manufacturing)
			{
				ManufacturingBoxData component2 = gameObject.GetComponent<ManufacturingBoxData>();
				if (component2 == null)
				{
					continue;
				}
				numberOfProducts = component2.numberOfProducts;
				if (storableOnly && !hashSet3.Contains(component2.manufacturedProductIndex + "|" + component2.combinablesData))
				{
					continue;
				}
			}
			else
			{
				BoxData component3 = gameObject.GetComponent<BoxData>();
				if (component3 == null)
				{
					continue;
				}
				numberOfProducts = component3.numberOfProducts;
				if ((storableOnly && !hashSet2.Contains(component3.productID)) || !NavMesh.SamplePosition(new Vector3(gameObject.transform.position.x, 0f, gameObject.transform.position.z), out var _, 1f, -1))
				{
					continue;
				}
			}
			if (numberOfProducts < num)
			{
				num = numberOfProducts;
				result = gameObject;
				if (numberOfProducts == 0)
				{
					break;
				}
			}
		}
		return result;
	}

	private static HashSet<int> CollectFreeStorageSlots(NPC_Manager npc)
	{
		HashSet<int> hashSet = new HashSet<int>();
		Transform transform = npc.storageOBJ.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			Data_Container component = transform.GetChild(i).GetComponent<Data_Container>();
			if (component == null || component.productInfoArray == null)
			{
				continue;
			}
			Transform transform2 = transform.GetChild(i).transform.Find("BoxContainer");
			if (transform2 == null)
			{
				continue;
			}
			int[] productInfoArray = component.productInfoArray;
			int num = productInfoArray.Length / 2;
			for (int j = 0; j < num; j++)
			{
				if (productInfoArray[j * 2] >= 0 && productInfoArray[j * 2 + 1] <= 0 && transform2.GetChild(j).childCount <= 0)
				{
					hashSet.Add(productInfoArray[j * 2]);
				}
			}
		}
		return hashSet;
	}

	private static HashSet<string> CollectFreeManufacturingSlots(NPC_Manager npc)
	{
		HashSet<string> hashSet = new HashSet<string>();
		Transform transform = npc.manufacturingStorageShelvesOBJ.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			ManufacturingContainer component = transform.GetChild(i).GetComponent<ManufacturingContainer>();
			if (component == null || component.productInfoArray == null || component.combinableInfoArray == null)
			{
				continue;
			}
			Transform transform2 = transform.GetChild(i).transform.Find("BoxContainer");
			if (transform2 == null)
			{
				continue;
			}
			int[] productInfoArray = component.productInfoArray;
			int num = productInfoArray.Length / 2;
			for (int j = 0; j < num; j++)
			{
				if (productInfoArray[j * 2] >= 0 && productInfoArray[j * 2 + 1] <= 0 && transform2.GetChild(j).childCount <= 0)
				{
					hashSet.Add(productInfoArray[j * 2] + "|" + component.combinableInfoArray[j]);
				}
			}
		}
		return hashSet;
	}
}
