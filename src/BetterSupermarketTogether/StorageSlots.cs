using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class StorageSlots
{
    internal static bool PhysicallyEmpty(Transform container, int row)
    {
        var boxes = container.Find("BoxContainer");
        return boxes != null && row >= 0 && row < boxes.childCount && boxes.GetChild(row).childCount == 0;
    }

    internal static int OrdinaryRow(Data_Container container, int product, bool merge)
    {
        var contents = container != null ? container.productInfoArray : null;
        if (contents == null) return -1;
        var products = ProductListing.Instance != null ? ProductListing.Instance.productsData : null;
        int max = products != null && product >= 0 && product < products.Length && products[product] != null
            ? products[product].maxItemsPerBox : 0;
        int empty = -1;
        for (int pair = 0; pair + 1 < contents.Length; pair += 2)
        {
            int id = contents[pair], count = contents[pair + 1];
            if (merge)
            {
                if (id == product && count > 0 && count < max) return pair / 2;
            }
            else if (count <= 0 && PhysicallyEmpty(container.transform, pair / 2))
            {
                if (id == product) return pair / 2;
                if (id == -1 && empty < 0) empty = pair / 2;
            }
        }
        return empty;
    }

    internal static int ManufacturedRow(ManufacturingContainer container, int product, string recipe, bool merge)
    {
        var contents = container != null ? container.productInfoArray : null;
        var recipes = container != null ? container.combinableInfoArray : null;
        recipe = RepairPolicies.NormalizeRecipe(recipe);
        if (contents == null || recipes == null || recipe == null) return -1;
        var products = ManufacturingBase.Instance != null ? ManufacturingBase.Instance.productsData : null;
        int max = products != null && product >= 0 && product < products.Length && products[product] != null
            ? products[product].itemsPerBox : 0;
        int empty = -1;
        for (int pair = 0; pair + 1 < contents.Length && pair / 2 < recipes.Length; pair += 2)
        {
            int id = contents[pair], count = contents[pair + 1];
            bool matches = id == product && RepairPolicies.NormalizeRecipe(recipes[pair / 2]) == recipe;
            if (merge)
            {
                if (matches && count > 0 && count < max) return pair / 2;
            }
            else if (count <= 0 && PhysicallyEmpty(container.transform, pair / 2))
            {
                if (matches) return pair / 2;
                if (id == -1 && empty < 0) empty = pair / 2;
            }
        }
        return empty;
    }

    internal static int OrdinaryContainer(NPC_Manager manager, NPC_Info employee, int product, bool merge)
    {
        if (manager.storageOBJ == null) return -1;
        var route = employee != null ? new EmployeeNavigation(employee.gameObject) : null;
        int selected = -1, bestRank = int.MaxValue;
        float bestDistance = float.MaxValue;
        foreach (Transform child in manager.storageOBJ.transform)
        {
            var container = child.GetComponent<Data_Container>();
            int row = OrdinaryRow(container, product, merge);
            if (row < 0 || container.standSpot == null || (route != null && !route.CanReach(container.standSpot.transform.position))) continue;
            bool labeled = container.productInfoArray[row * 2] == product;
            bool sameProduct = false;
            for (int pair = 0; pair + 1 < container.productInfoArray.Length; pair += 2)
                if (container.productInfoArray[pair] == product) sameProduct = true;
            int rank = labeled && container.containerID == 5 ? 0 :
                BetterSupermarketPlugin.GroupPlacementEnabled && sameProduct ? 1 : 2;
            float distance = Vector3.SqrMagnitude((employee != null ? employee.transform.position : manager.storageDistanceReferencePoint) - container.standSpot.transform.position);
            if (rank > bestRank || (rank == bestRank && distance >= bestDistance)) continue;
            selected = child.GetSiblingIndex(); bestRank = rank; bestDistance = distance;
        }
        return selected;
    }

    internal static int ManufacturedContainer(NPC_Manager manager, NPC_Info employee, int product, string recipe, bool merge)
    {
        if (manager.manufacturingStorageShelvesOBJ == null) return -1;
        var route = employee != null ? new EmployeeNavigation(employee.gameObject) : null;
        int selected = -1, bestRank = int.MaxValue;
        float bestDistance = float.MaxValue;
        recipe = RepairPolicies.NormalizeRecipe(recipe);
        foreach (Transform child in manager.manufacturingStorageShelvesOBJ.transform)
        {
            var container = child.GetComponent<ManufacturingContainer>();
            int row = ManufacturedRow(container, product, recipe, merge);
            if (row < 0 || container.standSpot == null || (route != null && !route.CanReach(container.standSpot.transform.position))) continue;
            bool labeled = container.productInfoArray[row * 2] == product &&
                RepairPolicies.NormalizeRecipe(container.combinableInfoArray[row]) == recipe;
            bool sameRecipe = false;
            for (int pair = 0; pair + 1 < container.productInfoArray.Length && pair / 2 < container.combinableInfoArray.Length; pair += 2)
                if (container.productInfoArray[pair] == product && RepairPolicies.NormalizeRecipe(container.combinableInfoArray[pair / 2]) == recipe) sameRecipe = true;
            int rank = labeled && container.containerID == 2 ? 0 : sameRecipe ? 1 : 2;
            float distance = Vector3.SqrMagnitude((employee != null ? employee.transform.position : manager.storageDistanceReferencePoint) - container.standSpot.transform.position);
            if (rank > bestRank || (rank == bestRank && distance >= bestDistance)) continue;
            selected = child.GetSiblingIndex(); bestRank = rank; bestDistance = distance;
        }
        return selected;
    }
}

[HarmonyPatch]
internal static class ReachableOrdinaryStoragePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NPC_Manager), "GetFreeStorageContainer");
        yield return AccessTools.Method(typeof(NPC_Manager), "GetStorageContainerWithBoxToMerge");
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NPC_Manager __instance, int __0, MethodBase __originalMethod, ref int __result)
    {
        var employee = EmployeeWorkGuard.Current;
        if (employee == null) return true;
        __result = StorageSlots.OrdinaryContainer(__instance, employee, __0, __originalMethod.Name == "GetStorageContainerWithBoxToMerge");
        if (__result >= 0 && __0 != 10000)
            employee.currentFreeStorageOBJ = __instance.storageOBJ.transform.GetChild(__result).gameObject;
        return false;
    }
}

[HarmonyPatch]
internal static class ReachableManufacturedStoragePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NPC_Manager), "GetFreeManufacturingStorageContainer");
        yield return AccessTools.Method(typeof(NPC_Manager), "GetStorageContainerWithManufacturingBoxToMerge");
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NPC_Manager __instance, int __0, string __1, MethodBase __originalMethod, ref int __result)
    {
        var employee = EmployeeWorkGuard.Current;
        if (employee == null) return true;
        __result = StorageSlots.ManufacturedContainer(__instance, employee, __0, __1, __originalMethod.Name == "GetStorageContainerWithManufacturingBoxToMerge");
        if (__result >= 0 && __0 != 10000)
            employee.currentFreeStorageOBJ = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(__result).gameObject;
        return false;
    }
}

[HarmonyPatch]
internal static class OrdinaryStorageRowPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NPC_Manager), "GetFreeStorageRow");
        yield return AccessTools.Method(typeof(NPC_Manager), "GetStorageRowWithBoxToMerge");
    }
    private static bool Prefix(NPC_Manager __instance, int __0, int __1, MethodBase __originalMethod, ref int __result)
    {
        if (EmployeeWorkGuard.Current == null) return true;
        var root = __instance.storageOBJ;
        __result = root != null && __0 >= 0 && __0 < root.transform.childCount ?
            StorageSlots.OrdinaryRow(root.transform.GetChild(__0).GetComponent<Data_Container>(), __1, __originalMethod.Name == "GetStorageRowWithBoxToMerge") : -1;
        return false;
    }
}

[HarmonyPatch]
internal static class ManufacturedStorageRowPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NPC_Manager), "GetFreeManufacturingStorageRow");
        yield return AccessTools.Method(typeof(NPC_Manager), "GetStorageRowWithManufacturingBoxToMerge");
    }
    private static bool Prefix(NPC_Manager __instance, int __0, int __1, string __2, MethodBase __originalMethod, ref int __result)
    {
        if (EmployeeWorkGuard.Current == null) return true;
        var root = __instance.manufacturingStorageShelvesOBJ;
        __result = root != null && __0 >= 0 && __0 < root.transform.childCount ?
            StorageSlots.ManufacturedRow(root.transform.GetChild(__0).GetComponent<ManufacturingContainer>(), __1, __2, __originalMethod.Name == "GetStorageRowWithManufacturingBoxToMerge") : -1;
        return false;
    }
}
