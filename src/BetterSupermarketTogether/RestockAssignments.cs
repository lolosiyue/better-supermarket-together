using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class RestockAssignments
{
    private sealed class WaitTarget { internal int State; }
    private static readonly ConditionalWeakTable<NPC_Info, WaitTarget> WaitTargets = new();
    private sealed class TaskTarget
    {
        internal GameObject Shelf, Source;
        internal bool Manufacturing;
    }
    private static readonly ConditionalWeakTable<NPC_Info, TaskTarget> Targets = new();

    internal static int[] NoTask() => new[] { -1, -1, -1, -1, -1, -1 };

    internal static void RememberWait(NPC_Info employee, int targetState)
    {
        if (employee != null && employee.isEmployee)
            WaitTargets.GetValue(employee, _ => new WaitTarget()).State = targetState;
    }

    internal static bool IsWaitingFor(NPC_Info employee, int targetState) =>
        employee != null && employee.state == -1 && employee.playingCoroutine &&
        WaitTargets.TryGetValue(employee, out var wait) && wait.State == targetState;

    internal static bool TryGetWaitTarget(NPC_Info employee, out int state)
    {
        state = 0;
        if (!WaitTargets.TryGetValue(employee, out var target)) return false;
        state = target.State;
        return true;
    }

    internal static bool IsRestocking(NPC_Info employee, bool manufacturing)
    {
        if (employee == null || !employee.isEmployee ||
            employee.taskPriority != (manufacturing ? 7 : 2) ||
            (manufacturing && employee.thiefProductsNumber != 2)) return false;

        int pickup = manufacturing ? 42 : 2;
        int placing = manufacturing ? 44 : 4;
        if (employee.state >= pickup && employee.state <= placing) return true;
        // Placement pauses between groups of items; retain the shelf during that pause.
        return IsWaitingFor(employee, placing);
    }

    internal static void RememberTask(NPC_Manager manager, GameObject requester, bool manufacturing, int[] task)
    {
        var employee = requester != null ? requester.GetComponent<NPC_Info>() : null;
        if (employee == null) return;
        Targets.Remove(employee);
        if (task == null || task.Length < 6 || task[0] < 0 || task[2] < 0) return;
        var shelves = manufacturing ? manager.manufacturingShelvesOBJ : manager.shelvesOBJ;
        var storage = manufacturing ? manager.manufacturingStorageShelvesOBJ : manager.storageOBJ;
        if (shelves == null || storage == null || task[0] >= shelves.transform.childCount || task[2] >= storage.transform.childCount) return;
        Targets.Add(employee, new TaskTarget
        {
            Shelf = shelves.transform.GetChild(task[0]).gameObject,
            Source = storage.transform.GetChild(task[2]).gameObject,
            Manufacturing = manufacturing
        });
    }

    internal static bool Resolve(NPC_Manager manager, NPC_Info employee, bool manufacturing, bool requireSource)
    {
        var task = employee.productAvailableArray;
        if (task == null || task.Length < 6 || task[0] < 0) return false;
        if (!Targets.TryGetValue(employee, out var target))
        {
            RememberTask(manager, employee.gameObject, manufacturing, task);
            if (!Targets.TryGetValue(employee, out target)) return false;
        }
        var shelves = manufacturing ? manager.manufacturingShelvesOBJ : manager.shelvesOBJ;
        var storage = manufacturing ? manager.manufacturingStorageShelvesOBJ : manager.storageOBJ;
        if (target.Manufacturing != manufacturing || shelves == null || target.Shelf == null ||
            target.Shelf.transform.parent != shelves.transform) return false;
        task[0] = target.Shelf.transform.GetSiblingIndex();
        if (!requireSource) return true;
        if (storage == null || target.Source == null || target.Source.transform.parent != storage.transform) return false;
        task[2] = target.Source.transform.GetSiblingIndex();
        return true;
    }

    internal static void ForgetTask(NPC_Info employee)
    {
        Targets.Remove(employee);
        employee.productAvailableArray = NoTask();
    }

    internal static void Collect(NPC_Manager manager, GameObject requester, bool manufacturing,
        out HashSet<int> shelves, out HashSet<long> sources)
    {
        shelves = new HashSet<int>();
        sources = new HashSet<long>();
        if (manager == null || manager.employeeParentOBJ == null) return;
        foreach (Transform child in manager.employeeParentOBJ.transform)
        {
            var employee = child.GetComponent<NPC_Info>();
            if (employee == null || employee.gameObject == requester || !IsRestocking(employee, manufacturing)) continue;
            if (!Resolve(manager, employee, manufacturing, employee.state == (manufacturing ? 42 : 2))) continue;
            var task = employee.productAvailableArray;
            if (task == null || task.Length < 6 || task[0] < 0) continue;
            shelves.Add(task[0]);
            if (employee.state == (manufacturing ? 42 : 2) && task[2] >= 0 && task[3] >= 0)
                sources.Add(SmarterRestockerTaskPatch.SlotKey(task[2], task[3]));
        }
    }

    internal static void GuardAssignment(NPC_Manager manager, GameObject requester, bool manufacturing, ref int[] task)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled ||
            task == null || task.Length < 6 || task[0] < 0) return;
        Collect(manager, requester, manufacturing, out var shelves, out var sources);
        if (shelves.Contains(task[0]) || sources.Contains(SmarterRestockerTaskPatch.SlotKey(task[2], task[3])))
            task = NoTask();
        RememberTask(manager, requester, manufacturing, task);
    }

    internal static bool Retarget(NPC_Manager manager, NPC_Info employee, int productId, int currentShelf,
        bool manufacturing, string recipe)
    {
        var root = manufacturing ? manager.manufacturingShelvesOBJ : manager.shelvesOBJ;
        if (root == null || employee == null || employee.productAvailableArray == null || employee.productAvailableArray.Length < 2)
            return false;
        Collect(manager, employee.gameObject, manufacturing, out var occupied, out _);
        var route = new EmployeeNavigation(employee.gameObject);
        recipe = RepairPolicies.NormalizeRecipe(recipe);
        int selectedShelf = -1, selectedRow = -1;
        float selectedRatio = float.MaxValue;
        bool selectedCurrent = false;
        for (int shelf = 0; shelf < root.transform.childCount; shelf++)
        {
            if (occupied.Contains(shelf)) continue;
            int[] contents;
            string[] recipes = null;
            if (manufacturing)
            {
                var container = root.transform.GetChild(shelf).GetComponent<ManufacturingContainer>();
                if (container == null || container.standSpot == null || !route.CanReach(container.standSpot.transform.position)) continue;
                contents = container.productInfoArray;
                recipes = container.combinableInfoArray;
            }
            else
            {
                var container = root.transform.GetChild(shelf).GetComponent<Data_Container>();
                if (container == null || container.standSpot == null || !route.CanReach(container.standSpot.transform.position)) continue;
                contents = container.productInfoArray;
            }
            if (contents == null) continue;
            for (int row = 0; row + 1 < contents.Length; row += 2)
            {
                if (contents[row] != productId || contents[row + 1] < 0) continue;
                if (manufacturing && (recipe == null || recipes == null || row / 2 >= recipes.Length || RepairPolicies.NormalizeRecipe(recipes[row / 2]) != recipe)) continue;
                int capacity = manufacturing ? manager.GetMaxManufacturingProductsPerRow(shelf, productId) : manager.GetMaxProductsPerRow(shelf, productId);
                if (capacity <= 0 || contents[row + 1] >= capacity) continue;
                float ratio = (float)contents[row + 1] / capacity;
                bool isCurrent = shelf == currentShelf;
                if (selectedShelf < 0 || (isCurrent && !selectedCurrent) || (isCurrent == selectedCurrent && ratio < selectedRatio))
                {
                    selectedShelf = shelf;
                    selectedRow = row;
                    selectedRatio = ratio;
                    selectedCurrent = isCurrent;
                }
            }
        }
        if (selectedShelf < 0) return false;
        employee.productAvailableArray[0] = selectedShelf;
        employee.productAvailableArray[1] = selectedRow;
        var target = Targets.GetValue(employee, _ => new TaskTarget { Manufacturing = manufacturing });
        target.Shelf = root.transform.GetChild(selectedShelf).gameObject;
        return true;
    }
}

[HarmonyPatch(typeof(NPC_Info), "StartWaitState")]
internal static class RestockWaitTargetPatch
{
    private static void Prefix(NPC_Info __instance, int targetState) => RestockAssignments.RememberWait(__instance, targetState);
}

[HarmonyPatch(typeof(NPC_Manager), "CheckIfShelfWithSameProduct")]
internal static class RestockRetargetPatch
{
    private static bool Prefix(NPC_Manager __instance, int productIDToCheck, NPC_Info npcInfoComponent,
        int currentShelfIndex, ref bool __result)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled) return true;
        __result = RestockAssignments.Retarget(__instance, npcInfoComponent, productIDToCheck, currentShelfIndex, false, null);
        return false;
    }
}

[HarmonyPatch(typeof(NPC_Manager), "CheckIfManufacturingShelfWithSameProduct")]
internal static class ManufacturingRestockRetargetPatch
{
    private static bool Prefix(NPC_Manager __instance, int productIDToCheck, NPC_Info npcInfoComponent,
        int currentShelfIndex, string combinableData, ref bool __result)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled) return true;
        __result = RestockAssignments.Retarget(__instance, npcInfoComponent, productIDToCheck, currentShelfIndex, true, combinableData);
        return false;
    }
}
