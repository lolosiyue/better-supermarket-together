using System;
using System.Collections.Generic;
using UnityEngine;

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

    internal static bool IsPickingUp(NPC_Info employee, bool manufacturing)
    {
        return employee != null && employee.isEmployee && !employee.employeeDismissed && employee.equippedItem == 0 &&
            (manufacturing ? employee.taskPriority == 7 && employee.thiefProductsNumber == 1 && employee.state == 21 :
                employee.taskPriority == 3 && employee.state == 1);
    }

    internal static GameObject Select(NPC_Manager npc, GameObject requester, bool manufacturing, bool storableOnly)
    {
        var root = manufacturing ? npc.manufacturingBoxesOBJ : npc.boxesOBJ;
        var employee = requester != null ? requester.GetComponent<NPC_Info>() : null;
        if (root == null || employee == null || npc.employeeParentOBJ == null) return null;
        var route = new EmployeeNavigation(requester);
        if (!route.Ready) return null;
        var reserved = new HashSet<GameObject>();
        foreach (Transform child in npc.employeeParentOBJ.transform)
        {
            var other = child.GetComponent<NPC_Info>();
            if (other != employee && IsPickingUp(other, manufacturing) && other.randomBox != null)
                reserved.Add(other.randomBox);
        }
        GameObject selected = null;
        int smallest = int.MaxValue;
        float shortest = float.MaxValue;
        foreach (Transform child in root.transform)
        {
            if (reserved.Contains(child.gameObject)) continue;
            int count, product;
            string recipe = null;
            if (manufacturing)
            {
                var box = child.GetComponent<ManufacturingBoxData>();
                if (box == null) continue;
                count = box.numberOfProducts; product = box.manufacturedProductIndex; recipe = box.combinablesData;
            }
            else
            {
                var box = child.GetComponent<BoxData>();
                if (box == null) continue;
                count = box.numberOfProducts; product = box.productID;
            }
            if (count < 0 || product < 0 || count > smallest || !route.CanReach(child.position)) continue;
            // Empty cartons can always be collected for recycling. Full boxes need a reachable
            // compatible empty row or merge target when the warehouse is otherwise full.
            if (storableOnly && count > 0)
            {
                int free = manufacturing ? StorageSlots.ManufacturedContainer(npc, employee, product, recipe, false) :
                    StorageSlots.OrdinaryContainer(npc, employee, product, false);
                int merge = free >= 0 ? free : manufacturing ? StorageSlots.ManufacturedContainer(npc, employee, product, recipe, true) :
                    StorageSlots.OrdinaryContainer(npc, employee, product, true);
                if (merge < 0) continue;
            }
            float distance = Vector3.SqrMagnitude(employee.transform.position - child.position);
            if (count == smallest && distance >= shortest) continue;
            smallest = count; shortest = distance; selected = child.gameObject;
        }
        return selected;
    }
}
