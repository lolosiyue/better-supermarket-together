using System.Collections.Generic;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class WarehouseOptimizer
{
	internal static bool PatchWarned;

	internal static bool ConsolidateWarned;

	internal static int FindFreeRow(Data_Container container, int productId, HashSet<long> claimedSlots, int containerIndex)
	{
		int[] productInfoArray = container.productInfoArray;
		if (productInfoArray == null)
		{
			return -1;
		}
		Transform transform = container.transform.Find("BoxContainer");
		if (transform == null)
		{
			return -1;
		}
		int num = productInfoArray.Length / 2;
		for (int i = 0; i < num; i++)
		{
			if (productInfoArray[i * 2] == productId && productInfoArray[i * 2 + 1] <= 0 && transform.GetChild(i).childCount <= 0 && !claimedSlots.Contains(SmarterRestockerTaskPatch.SlotKey(containerIndex, i * 2)))
			{
				return i;
			}
		}
		for (int j = 0; j < num; j++)
		{
			if (productInfoArray[j * 2] == -1 && !claimedSlots.Contains(SmarterRestockerTaskPatch.SlotKey(containerIndex, j * 2)))
			{
				return j;
			}
		}
		return -1;
	}

	internal static void MoveBox(Data_Container source, int sourcePair, Data_Container target, int targetRow, int productId, int count)
	{
		source.EmployeeUpdateArrayValuesStorage(sourcePair, -1, -1);
		target.EmployeeUpdateArrayValuesStorage(targetRow * 2, productId, count);
	}
}
