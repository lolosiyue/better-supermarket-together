using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class ProductionLifecycle
{
    private sealed class TableState
    {
        internal int Accepted, Product = -1, Generation;
        internal string Recipe, AutoRecipe;
        internal float Finished = -1f;
    }
    private sealed class Collector
    {
        internal ManufacturingProduction Table;
        internal GameData Game;
        internal string Recipe;
        internal readonly List<int> Gathered = new();
        internal GameObject Source;
        internal int SourceKind, SelectedProduct;
        internal Vector3 Position;
    }
    private static readonly ConditionalWeakTable<ManufacturingProduction, TableState> Tables = new();
    private static readonly ConditionalWeakTable<NPC_Info, Collector> Collectors = new();
    private static readonly MethodInfo AddTray = AccessTools.Method(typeof(ManufacturingProduction), "UserCode_CmdAddItemToTable__Int32__GameObject");
    private static readonly MethodInfo RemoveTray = AccessTools.Method(typeof(ManufacturingProduction), "UserCode_CmdRemoveItemFromTable__Int32__GameObject");
    private static readonly FieldInfo IngredientsReady = AccessTools.Field(typeof(ManufacturingProduction), "ingredientsArePlaced");
    private static readonly FieldInfo Producing = AccessTools.Field(typeof(ManufacturingProduction), "producing");
    private static readonly MethodInfo Unequip = AccessTools.Method(typeof(NPC_Manager), "UnequipBox");

    private static string Head(ManufacturingProduction table) => table != null && table.productQueue != null && table.productQueue.Count > 0 &&
        table.combinableQueue != null && table.combinableQueue.Count > 0 ?
        table.productQueue[0] + "|" + RepairPolicies.NormalizeRecipe(table.combinableQueue[0]) : null;

    internal static bool ReadyOrProducing(ManufacturingProduction table) =>
        IngredientsReady?.GetValue(table) is true || Producing?.GetValue(table) is true;

    internal static string PreparedRecipe(ManufacturingProduction table) => IngredientsReady?.GetValue(table) is true ? Head(table) : null;

    internal static void PreservePreparedRecipe(ManufacturingProduction table, string recipe)
    {
        if (BetterSupermarketPlugin.IsHost && table.isServer && recipe != null && recipe == Head(table))
            IngredientsReady?.SetValue(table, true);
    }

    internal static bool AutoClaimed(ManufacturingProduction table)
    {
        if (!Tables.TryGetValue(table, out var state)) return false;
        if (!BetterSupermarketPlugin.ProductionTrayEnabled || state.AutoRecipe != Head(table) || ReadyOrProducing(table))
            state.AutoRecipe = null;
        return state.AutoRecipe != null;
    }

    internal static bool TryFeed(ManufacturingProduction table, int product)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !table.isServer || AddTray == null || table.assignedToEmployee || ReadyOrProducing(table)) return false;
        var state = Tables.GetValue(table, _ => new TableState());
        state.AutoRecipe = Head(table);
        int before = state.Accepted;
        // Invoke the host implementation synchronously. The Command wrapper only sends a message.
        AddTray.Invoke(table, new object[] { product, null });
        // The final accepted ingredient can immediately be consumed, shrinking itemsInTray.
        return state.Accepted != before;
    }

    internal static void Accepted(ManufacturingProduction table)
    {
        if (BetterSupermarketPlugin.IsHost && table.isServer) Tables.GetValue(table, _ => new TableState()).Accepted++;
    }

    internal static void Started(ManufacturingProduction table)
    {
        if (!BetterSupermarketPlugin.IsHost || !table.isServer || Head(table) == null) return;
        var state = Tables.GetValue(table, _ => new TableState());
        state.Product = table.productQueue[0];
        state.Recipe = RepairPolicies.NormalizeRecipe(table.combinableQueue[0]);
        state.Finished = -1f;
        state.AutoRecipe = null;
    }

    internal static void Finished(ManufacturingProduction table)
    {
        if (BetterSupermarketPlugin.IsHost && table.isServer && Tables.TryGetValue(table, out var state)) state.Finished = Time.unscaledTime;
    }

    internal static bool HasInFlight(ManufacturingProduction table, int product, string recipe)
    {
        if (!Tables.TryGetValue(table, out var state)) return false;
        if (state.Finished >= 0f && Time.unscaledTime - state.Finished > 2f) state.Product = -1;
        return state.Product == product && state.Recipe == recipe;
    }

    internal static bool ValidateCollector(NPC_Manager manager, NPC_Info employee, out ManufacturingProduction table)
    {
        table = null;
        if (!BetterSupermarketPlugin.IsHost) return false;
        if (Collectors.TryGetValue(employee, out var collector))
        {
            table = collector.Table;
            int actual = table != null ? manager.manufacturingProducersList.IndexOf(table.gameObject) : -1;
            if (actual < 0 || Head(table) != collector.Recipe)
            { CancelCollector(employee); return false; }
            employee.packagingAssignedOrderIndex = actual;
            return true;
        }
        int index = employee.packagingAssignedOrderIndex;
        if (index < 0 || index >= manager.manufacturingProducersList.Count || manager.manufacturingProducersList[index] == null)
            return false;
        table = manager.manufacturingProducersList[index].GetComponent<ManufacturingProduction>();
        string head = Head(table);
        if (head == null) return false;
        Collectors.Add(employee, new Collector { Table = table, Game = GameData.Instance, Recipe = head, Position = employee.transform.position });
        return true;
    }

    internal static bool TakeFromTray(ManufacturingProduction table, int product)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !table.isServer || RemoveTray == null || table.itemsInTray == null || !table.itemsInTray.Contains(product)) return false;
        int before = table.itemsInTray.Count;
        RemoveTray.Invoke(table, new object[] { product, null });
        return table.itemsInTray.Count == before - 1;
    }

    internal static void Collected(NPC_Info employee, int product)
    {
        if (!Collectors.TryGetValue(employee, out var collector)) return;
        collector.Gathered.Add(product);
        collector.Position = employee.transform.position;
    }

    internal static void RememberSource(NPC_Info employee, GameObject source, int kind, int product)
    {
        if (!Collectors.TryGetValue(employee, out var collector)) return;
        collector.Source = source; collector.SourceKind = kind; collector.SelectedProduct = product;
    }

    internal static bool ResolveSource(NPC_Manager manager, NPC_Info employee, out Data_Container container, out int kind, out int product)
    {
        container = null; kind = -1; product = -1;
        if (!Collectors.TryGetValue(employee, out var collector) || collector.Source == null) return false;
        var root = collector.SourceKind == 0 ? manager.storageOBJ : manager.shelvesOBJ;
        if (root == null || collector.Source.transform.parent != root.transform) return false;
        container = collector.Source.GetComponent<Data_Container>();
        kind = collector.SourceKind; product = collector.SelectedProduct;
        employee.orderProductLocationInfoArray[1] = collector.Source.transform.GetSiblingIndex();
        return container != null;
    }

    internal static void EmployeeProduced(ManufacturingProduction table)
    {
        var manager = NPC_Manager.Instance;
        if (!BetterSupermarketPlugin.IsHost || !table.isServer || manager == null || manager.employeeParentOBJ == null) return;
        foreach (Transform child in manager.employeeParentOBJ.transform)
        {
            var employee = child.GetComponent<NPC_Info>();
            if (employee != null && Collectors.TryGetValue(employee, out var collector) && collector.Table == table)
                Collectors.Remove(employee); // These ingredients now belong to the accepted production batch.
        }
    }

    internal static void CancelCollector(NPC_Info employee, ManufacturingProduction expectedTable = null, bool resetEmployee = true)
    {
        if (ReferenceEquals(employee, null) || !Collectors.TryGetValue(employee, out var collector)) return;
        if (!ReferenceEquals(expectedTable, null) && !ReferenceEquals(collector.Table, expectedTable)) return;
        Collectors.Remove(employee);
        // A coroutine from an old host session must never return its stock into a new room.
        if (!BetterSupermarketPlugin.IsHost || collector.Game != GameData.Instance ||
            collector.Table != null && !collector.Table.isServer) return;
        if (collector.Table != null) collector.Table.assignedToEmployee = false;
        var blackboard = GameData.Instance != null ? GameData.Instance.GetComponent<ManagerBlackboard>() : null;
        var returned = new Dictionary<int, int>();
        foreach (int product in collector.Gathered)
            returned[product] = returned.TryGetValue(product, out int count) ? count + 1 : 1;
        if (blackboard != null)
            foreach (var entry in returned)
                blackboard.SpawnBoxFromEmployee(collector.Position + Vector3.up, entry.Key, entry.Value);
        if (resetEmployee && employee != null && employee.equippedItem == 5 && NPC_Manager.Instance != null)
            Unequip?.Invoke(NPC_Manager.Instance, new object[] { employee });
        if (returned.Count > 0)
            BetterSupermarketPlugin.Log.LogInfo($"Production collection cancelled: returned {collector.Gathered.Count} ingredient(s) as ordinary boxes.");
    }

    internal static void Assign(ManufacturingProduction table, NPC_Info employee)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !table.isServer) return;
        var state = Tables.GetValue(table, _ => new TableState());
        int generation = ++state.Generation;
        CancelCollector(employee, resetEmployee: false);
        string recipe = Head(table);
        if (recipe != null)
            Collectors.Add(employee, new Collector { Table = table, Game = GameData.Instance, Recipe = recipe, Position = employee.transform.position });
        table.assignedToEmployee = true;
        table.StartCoroutine(WatchOwner(table, employee, generation));
    }

    private static IEnumerator WatchOwner(ManufacturingProduction table, NPC_Info employee, int generation)
    {
        var game = GameData.Instance;
        yield return null; // The caller sets the employee's state immediately after assigning the table.
        var delay = new WaitForSeconds(1f);
        while (BetterSupermarketPlugin.IsHost && GameData.Instance == game && table != null && table.isServer &&
            Tables.TryGetValue(table, out var state) && state.Generation == generation)
        {
            bool active = employee != null && Collectors.TryGetValue(employee, out var collector) && collector.Table == table &&
                !employee.employeeDismissed && employee.taskPriority == 7 && employee.thiefProductsNumber == 0 &&
                (employee.state >= 2 && employee.state <= 5 || employee.state == -1 &&
                    RestockAssignments.TryGetWaitTarget(employee, out int target) && target >= 2 && target <= 5);
            if (!active && !ReadyOrProducing(table)) break;
            yield return delay;
        }
        if (BetterSupermarketPlugin.IsHost && GameData.Instance == game && table != null && table.isServer &&
            Tables.TryGetValue(table, out var current) && current.Generation == generation)
        {
            CancelCollector(employee, table);
            table.assignedToEmployee = false;
        }
    }
}

[HarmonyPatch(typeof(ManufacturingProduction), "RpcAddItemToTable")]
internal static class TrayAcceptancePatch
{
    private static void Prefix(ManufacturingProduction __instance) => ProductionLifecycle.Accepted(__instance);
}

[HarmonyPatch(typeof(ManufacturingProduction), "Produce")]
internal static class ProductionStartedPatch
{
    private static void Prefix(ManufacturingProduction __instance) => ProductionLifecycle.Started(__instance);
}

[HarmonyPatch(typeof(ManufacturingProduction), "RpcProduceFinish")]
internal static class ProductionFinishedPatch
{
    private static void Prefix(ManufacturingProduction __instance) => ProductionLifecycle.Finished(__instance);
}

[HarmonyPatch(typeof(ManufacturingProduction), "ProduceFromEmployee")]
internal static class EmployeeProductionAcceptedPatch
{
    private static void Prefix(ManufacturingProduction __instance) => ProductionLifecycle.EmployeeProduced(__instance);
}

[HarmonyPatch(typeof(ManufacturingProduction), "EmployeeAssignCoroutineCall")]
internal static class ProductionOwnerPatch
{
    private static bool Prefix(ManufacturingProduction __instance, NPC_Info __0)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !__instance.isServer) return true;
        ProductionLifecycle.Assign(__instance, __0);
        return false;
    }
}

[HarmonyPatch(typeof(NPC_Manager), "GetManufacturingProducerWithQueue")]
internal static class ReachableProducerPatch
{
    private static bool Prefix(NPC_Manager __instance, ref int __result)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !__instance.isServer) return true;
        var employee = EmployeeWorkGuard.Current;
        var route = employee != null ? new EmployeeNavigation(employee.gameObject) : null;
        __result = -1;
        for (int i = 0; i < __instance.manufacturingProducersList.Count; i++)
        {
            var obj = __instance.manufacturingProducersList[i];
            var table = obj != null ? obj.GetComponent<ManufacturingProduction>() : null;
            var stand = obj != null ? obj.transform.Find("Standspot") : null;
            if (table == null || table.assignedToEmployee || table.productQueue == null || table.productQueue.Count == 0 ||
                ProductionLifecycle.ReadyOrProducing(table) || ProductionLifecycle.AutoClaimed(table) ||
                stand == null || route != null && !route.CanReach(stand.position)) continue;
            __result = i;
            break;
        }
        return false;
    }
}
