using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(NPC_Manager), "EmployeeNPCControl")]
internal static class EmployeeWorkGuard
{
    // Synchronous scope for storage/ingredient selectors called by this employee's update.
    internal static NPC_Info Current;

    internal static string Describe(NPC_Info employee)
    {
        string flow = employee.taskPriority == 2 ? "ordinary-restock" : employee.taskPriority == 3 ? "ordinary-storage" :
            employee.taskPriority == 7 ? employee.thiefProductsNumber == 0 ? "production-ingredients" :
            employee.thiefProductsNumber == 1 ? "manufactured-storage" : "manufactured-restock" : "other";
        var agent = employee.GetComponent<NavMeshAgent>();
        string navigation = agent == null || !agent.enabled || !agent.isOnNavMesh ? "off-navmesh/disabled" :
            $"path={agent.pathStatus} pending={agent.pathPending} remaining={agent.remainingDistance:0.0} destination={agent.destination}";
        string wait = RestockAssignments.TryGetWaitTarget(employee, out int target) ? target.ToString() : "unknown";
        return $"flow={flow} equipped={employee.equippedItem} waitTarget={wait} recipe={employee.mBoxCombinableData} {navigation}";
    }

    private static bool Prefix(NPC_Manager __instance, int __0, out NPC_Info __state)
    {
        __state = Current;
        Current = null;
        if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled || !__instance.isServer ||
            __instance.employeeParentOBJ == null || __0 < 0 || __0 >= __instance.employeeParentOBJ.transform.childCount) return true;
        var employee = __instance.employeeParentOBJ.transform.GetChild(__0).GetComponent<NPC_Info>();
        if (employee == null || (employee.taskPriority != 2 && employee.taskPriority != 3 && employee.taskPriority != 7)) return true;
        Current = employee;
        if (employee.employeeDismissed || __instance.employeesOnRiot) return true;
        var agent = employee.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if (employee.state == -1)
        {
            EmployeeNavigation.ClearProgress(employee);
            if (!employee.playingCoroutine)
            {
                if (RestockAssignments.TryGetWaitTarget(employee, out int target)) employee.state = target;
                else Recover(__instance, employee, agent, "orphan wait state / 等待程序已結束");
            }
            return true;
        }
        if (IsTravel(employee) && EmployeeNavigation.IsStalled(employee, agent))
        {
            EmployeeNavigation.Block(employee, agent.destination);
            Recover(__instance, employee, agent, "path incomplete or no progress / 路徑失效或長時間沒有移動");
            return false;
        }
        if (employee.taskPriority == 2 && employee.state >= 2 && employee.state <= 4)
            return GuardRestock(__instance, employee, agent, false);
        if (employee.taskPriority == 7 && employee.thiefProductsNumber == 2 && employee.state >= 42 && employee.state <= 44)
            return GuardRestock(__instance, employee, agent, true);
        if (GroundBoxHelper.IsPickingUp(employee, false)) return GuardGround(__instance, employee, agent, false);
        if (GroundBoxHelper.IsPickingUp(employee, true)) return GuardGround(__instance, employee, agent, true);
        if (employee.taskPriority == 2 && employee.state == 7) return GuardReturn(__instance, employee, false);
        if (employee.taskPriority == 7 && employee.thiefProductsNumber == 2 && employee.state == 47)
            return GuardReturn(__instance, employee, true);
        if (employee.taskPriority == 3 && (employee.state == 3 || employee.state == 20))
            return GuardStorageReference(__instance, employee, false, employee.state == 3 ? 2 : 19);
        if (employee.taskPriority == 2 && employee.state == 20) return GuardStorageReference(__instance, employee, false, 5);
        if (employee.taskPriority == 7 && (employee.state == 23 || employee.state == 33 || employee.state == 50))
            return GuardStorageReference(__instance, employee, true, employee.state == 50 ? 45 : employee.state == 33 ? 32 : 22);
        if (employee.taskPriority == 7 && employee.thiefProductsNumber == 0 && employee.state >= 2 && employee.state <= 5)
            return GuardProducer(__instance, employee, agent);
        return true;
    }

    private static void Postfix(NPC_Info __state)
    {
        Current = __state;
    }
    private static Exception Finalizer(Exception __exception, NPC_Info __state)
    {
        Current = __state;
        return __exception;
    }

    private static bool GuardRestock(NPC_Manager manager, NPC_Info employee, NavMeshAgent agent, bool manufacturing)
    {
        int pickup = manufacturing ? 42 : 2;
        int placing = manufacturing ? 44 : 4;
        bool collecting = employee.state == pickup;
        var route = new EmployeeNavigation(employee.gameObject);
        if (!RestockAssignments.Resolve(manager, employee, manufacturing, collecting))
            return Redirect(manager, employee, agent, manufacturing, "assigned container removed / 任務貨架已移除");
        var task = employee.productAvailableArray;
        var shelves = manufacturing ? manager.manufacturingShelvesOBJ : manager.shelvesOBJ;
        var targetObject = shelves.transform.GetChild(task[0]);
        int[] contents;
        string[] recipes = null;
        Transform stand;
        if (manufacturing)
        {
            var target = targetObject.GetComponent<ManufacturingContainer>();
            contents = target != null ? target.productInfoArray : null;
            recipes = target != null ? target.combinableInfoArray : null;
            stand = target != null ? target.standSpot : null;
        }
        else
        {
            var target = targetObject.GetComponent<Data_Container>();
            contents = target != null ? target.productInfoArray : null;
            stand = target != null ? target.standSpot : null;
        }
        string recipe = collecting ? employee.packagingAssignedOrderData : employee.mBoxCombinableData;
        bool validRow = contents != null && task[1] >= 0 && task[1] % 2 == 0 && task[1] + 1 < contents.Length &&
            contents[task[1]] == task[4] && contents[task[1] + 1] >= 0 && stand != null;
        if (manufacturing) validRow &= recipes != null && task[1] >= 0 && task[1] / 2 < recipes.Length &&
            RepairPolicies.NormalizeRecipe(recipes[task[1] / 2]) == RepairPolicies.NormalizeRecipe(recipe);
        int capacity = validRow ? manufacturing ? manager.GetMaxManufacturingProductsPerRow(task[0], task[4]) :
            manager.GetMaxProductsPerRow(task[0], task[4]) : 0;
        RestockAssignments.Collect(manager, employee.gameObject, manufacturing, out var occupied, out _);
        if (!validRow || capacity <= 0 || contents[task[1] + 1] >= capacity || occupied.Contains(task[0]))
            return Redirect(manager, employee, agent, manufacturing, "target changed, full or occupied / 貨架已變更、補滿或被占用");
        if (collecting)
        {
            var storage = manufacturing ? manager.manufacturingStorageShelvesOBJ : manager.storageOBJ;
            var sourceObject = storage.transform.GetChild(task[2]);
            int[] sourceContents;
            Transform sourceStand;
            if (manufacturing)
            {
                var source = sourceObject.GetComponent<ManufacturingContainer>();
                sourceContents = source != null ? source.productInfoArray : null;
                sourceStand = source != null ? source.standSpot : null;
                if (source == null || source.combinableInfoArray == null || task[3] < 0 ||
                    task[3] / 2 >= source.combinableInfoArray.Length ||
                    RepairPolicies.NormalizeRecipe(source.combinableInfoArray[task[3] / 2]) != RepairPolicies.NormalizeRecipe(recipe))
                {
                    Recover(manager, employee, agent, "source recipe changed / 取貨配方已變更");
                    return false;
                }
                employee.packagingAssignedOrderData = source.combinableInfoArray[task[3] / 2];
            }
            else
            {
                var source = sourceObject.GetComponent<Data_Container>();
                sourceContents = source != null ? source.productInfoArray : null;
                sourceStand = source != null ? source.standSpot : null;
            }
            if (employee.equippedItem != 0 || sourceContents == null || sourceStand == null || task[3] < 0 ||
                task[3] % 2 != 0 || task[3] + 1 >= sourceContents.Length || sourceContents[task[3]] != task[5] || sourceContents[task[3] + 1] <= 0)
            {
                Recover(manager, employee, agent, "source empty or changed / 取貨來源已空或變更");
                return false;
            }
            return Approach(employee, route, sourceStand.transform.position);
        }
        if (employee.equippedItem != (manufacturing ? 4 : 1) || employee.boxProductID != task[4] || employee.boxNumberOfProducts <= 0)
        {
            employee.state = manufacturing ? 45 : 5;
            return false;
        }
        if (!Approach(employee, route, stand.transform.position)) return false;
        // Placement pauses can outlive edits to shelf labels; validation runs again on every return to placing.
        return employee.state == placing || !agent.pathPending;
    }

    private static bool Redirect(NPC_Manager manager, NPC_Info employee, NavMeshAgent agent, bool manufacturing, string reason)
    {
        if (employee.equippedItem == (manufacturing ? 4 : 1) && employee.boxNumberOfProducts > 0 &&
            RestockAssignments.Retarget(manager, employee, employee.boxProductID, -1, manufacturing, employee.mBoxCombinableData))
        {
            employee.state = manufacturing ? 43 : 3;
            agent.ResetPath();
            return false;
        }
        Recover(manager, employee, agent, reason);
        return false;
    }

    private static bool GuardGround(NPC_Manager manager, NPC_Info employee, NavMeshAgent agent, bool manufacturing)
    {
        var root = manufacturing ? manager.manufacturingBoxesOBJ : manager.boxesOBJ;
        var box = employee.randomBox;
        bool correctType = box != null && (manufacturing ? box.GetComponent<ManufacturingBoxData>() != null : box.GetComponent<BoxData>() != null);
        if (!correctType || root == null || box.transform.parent != root.transform)
        {
            Recover(manager, employee, agent, "ground box removed / 地上箱子已移除");
            return false;
        }
        return Approach(employee, new EmployeeNavigation(employee.gameObject), box.transform.position);
    }

    private static bool GuardReturn(NPC_Manager manager, NPC_Info employee, bool manufacturing)
    {
        int shelf = manufacturing ? StorageSlots.ManufacturedContainer(manager, employee, employee.boxProductID, employee.mBoxCombinableData, false) :
            StorageSlots.OrdinaryContainer(manager, employee, employee.boxProductID, false);
        if (shelf < 0) return true; // Vanilla will merge, wait, or choose the leftover-box area.
        var root = manufacturing ? manager.manufacturingStorageShelvesOBJ : manager.storageOBJ;
        var target = root.transform.GetChild(shelf);
        var stand = manufacturing ? target.GetComponent<ManufacturingContainer>().standSpot : target.GetComponent<Data_Container>().standSpot;
        // Vanilla reselects the row on arrival and otherwise deposits into a different rack without walking there.
        return Approach(employee, new EmployeeNavigation(employee.gameObject), stand.transform.position);
    }

    private static bool GuardStorageReference(NPC_Manager manager, NPC_Info employee, bool manufacturing, int retry)
    {
        var root = manufacturing ? manager.manufacturingStorageShelvesOBJ : manager.storageOBJ;
        var target = employee.currentFreeStorageOBJ;
        if (root == null || target == null || target.transform.parent != root.transform)
        {
            employee.state = retry;
            return false;
        }
        employee.currentFreeStorageIndex = target.transform.GetSiblingIndex();
        var stand = manufacturing ? target.GetComponent<ManufacturingContainer>()?.standSpot : target.GetComponent<Data_Container>()?.standSpot;
        if (stand == null) { employee.state = retry; return false; }
        return Approach(employee, new EmployeeNavigation(employee.gameObject), stand.transform.position);
    }

    private static bool GuardProducer(NPC_Manager manager, NPC_Info employee, NavMeshAgent agent)
    {
        if (!ProductionLifecycle.ValidateCollector(manager, employee, out var table))
        {
            if (employee.state != -1) Recover(manager, employee, agent, "production job changed / 生產任務已變更");
            return false;
        }
        var route = new EmployeeNavigation(employee.gameObject);
        var needed = employee.manufacturedEmployeeProductsList;
        if (employee.state == 2)
        {
            needed.Clear();
            string baseRecipe = ManufacturingBase.Instance.RetrieveBaseRecipe(table.productQueue[0]);
            if (string.IsNullOrWhiteSpace(baseRecipe))
            { ProductionLifecycle.CancelCollector(employee); return false; }
            foreach (string group in baseRecipe.Split('|'))
            {
                var alternatives = new System.Collections.Generic.List<string>();
                foreach (string token in group.Split('-'))
                    if (int.TryParse(token, out int product) && product >= 0 && product != 999) alternatives.Add(token);
                // Keep missing groups: availability determines where to collect, never whether a recipe needs them.
                if (alternatives.Count == 0) { ProductionLifecycle.CancelCollector(employee); return false; }
                needed.Add(string.Join("|", alternatives));
            }
            string recipe = RepairPolicies.NormalizeRecipe(table.combinableQueue[0]);
            if (recipe != null && recipe != "999")
                foreach (string token in recipe.Split('-')) needed.Add(token);
            employee.EquipNPCItem(5);
            employee.StartWaitState(1.5f, 3);
            employee.state = -1;
            return false;
        }
        if (employee.state == 3)
        {
            if (needed.Count == 0)
            {
                var stand = table.transform.Find("Standspot");
                employee.state = 5;
                if (stand != null) route.MoveTo(stand.position);
                return false;
            }
            int firstProduct = -1;
            foreach (string token in needed[0].Split('|'))
            {
                if (!int.TryParse(token, out int product) || product < 0) continue;
                if (firstProduct < 0) firstProduct = product;
                if (ProductionLifecycle.TakeFromTray(table, product))
                {
                    ProductionLifecycle.Collected(employee, product);
                    needed.RemoveAt(0);
                    employee.StartWaitState(0.25f, 3); employee.state = -1;
                    return false;
                }
                int[] location = ReachableProductionIngredientPatch.Find(manager, employee, product);
                if (location[0] < 0) continue;
                var root = location[0] == 0 ? manager.storageOBJ : manager.shelvesOBJ;
                var source = root.transform.GetChild(location[1]).GetComponent<Data_Container>();
                employee.orderProductLocationInfoArray = location;
                ProductionLifecycle.RememberSource(employee, source.gameObject, location[0], product);
                if (route.MoveTo(source.standSpot.transform.position)) employee.state = 4;
                return false;
            }
            employee.RPCNotificationAboveHead("mfctrmply0", "product" + firstProduct);
            employee.StartWaitState(3f, 3); employee.state = -1;
            return false;
        }
        if (employee.state == 4)
        {
            var location = employee.orderProductLocationInfoArray;
            if (needed.Count == 0 || location == null || location.Length < 3 ||
                !ProductionLifecycle.ResolveSource(manager, employee, out var source, out int kind, out int product) ||
                source.productInfoArray == null || location[2] < 0 || location[2] % 2 != 0 || location[2] + 1 >= source.productInfoArray.Length ||
                source.productInfoArray[location[2]] != product || source.productInfoArray[location[2] + 1] <= 0 || source.standSpot == null)
            { employee.state = 3; return false; }
            if (!Approach(employee, route, source.standSpot.transform.position)) return false;
            int count = source.productInfoArray[location[2] + 1];
            if (kind == 0)
            {
                source.EmployeeUpdateArrayValuesStorage(location[2], count == 1 && source.transform.Find("CanvasSigns") == null ? -1 : product, count == 1 ? -1 : count - 1);
                if (count == 1) GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(employee.transform.position, product, 0);
            }
            else source.NPCGetsItemFromRow(product);
            ProductionLifecycle.Collected(employee, product);
            needed.RemoveAt(0);
            employee.StartWaitState(1.5f, 3); employee.state = -1;
            return false;
        }
        if (employee.state == 5)
        {
            if (needed.Count != 0) { employee.state = 3; return false; }
            var stand = table.transform.Find("Standspot");
            if (stand == null || !Approach(employee, route, stand.position)) return false;
            employee.manufacturingExperience += 3 * employee.manufacturingValue;
            table.ProduceFromEmployee();
            employee.StartWaitState(1.5f, 0); employee.state = -1;
        }
        return false;
    }

    private static bool Approach(NPC_Info employee, EmployeeNavigation route, Vector3 destination)
    {
        if (route.IsAt(destination)) return true;
        var agent = employee.GetComponent<NavMeshAgent>();
        if (!agent.pathPending && (Vector3.SqrMagnitude(agent.destination - destination) > 2.25f || !agent.hasPath))
        {
            if (!route.MoveTo(destination))
            {
                EmployeeNavigation.Block(employee, destination);
                Recover(NPC_Manager.Instance, employee, agent, "destination no longer reachable / 目的地已無法到達");
            }
        }
        return false;
    }

    private static void Recover(NPC_Manager manager, NPC_Info employee, NavMeshAgent agent, string reason)
    {
        int oldState = employee.state;
        if (employee.taskPriority == 7 && employee.thiefProductsNumber == 0 && employee.equippedItem == 5 && oldState == 5)
        {
            // Ingredients already collected on the employee's tray must not be discarded.
            agent.ResetPath();
            EmployeeNavigation.ClearProgress(employee);
            employee.StartWaitState(3f, 5);
            employee.state = -1;
            return;
        }
        agent.ResetPath();
        EmployeeNavigation.ClearProgress(employee);
        employee.randomBox = null;
        employee.currentFreeStorageIndex = -1;
        employee.currentFreeStorageOBJ = null;
        if (employee.taskPriority == 7 && employee.thiefProductsNumber == 0 && employee.equippedItem == 5 && oldState == 4)
            employee.state = 3; // Retry the uncollected ingredient using a different reachable source.
        else if (employee.taskPriority == 2 && employee.equippedItem == 1) employee.state = 5;
        else if (employee.taskPriority == 3 && employee.equippedItem == 1) employee.state = employee.boxNumberOfProducts > 0 ? 2 : 6;
        else if (employee.taskPriority == 7 && employee.equippedItem == 4) employee.state = employee.thiefProductsNumber == 2 ? 45 : employee.boxNumberOfProducts > 0 ? 22 : 26;
        else
        {
            RestockAssignments.ForgetTask(employee);
            employee.StartWaitState(2f, 0);
            employee.state = -1;
        }
        BetterSupermarketPlugin.Log.LogInfo($"Employee recovery: NPCID={employee.NPCID} role={employee.taskPriority} state={oldState}->{employee.state} item={employee.equippedItem} count={employee.boxNumberOfProducts} reason={reason}");
    }

    private static bool IsTravel(NPC_Info employee)
    {
        int s = employee.state;
        if (employee.taskPriority == 2) return s == 2 || s == 3 || s == 7 || s == 8 || s == 9 || s == 20 || s == 30;
        if (employee.taskPriority == 3) return s == 1 || s == 3 || s == 4 || s == 7 || s == 20 || s == 33;
        if (employee.taskPriority != 7) return false;
        return employee.thiefProductsNumber == 0 ? s == 4 || s == 5 :
            s == 21 || s == 23 || s == 24 || s == 27 || s == 33 || s == 34 ||
            s == 42 || s == 43 || s == 47 || s == 48 || s == 49 || s == 50 || s == 60;
    }
}

[HarmonyPatch(typeof(NPC_Manager), "GetStorageShelfWithProduct")]
internal static class ReachableProductionIngredientPatch
{
    private static bool Prefix(NPC_Manager __instance, int __0, ref int[] __result)
    {
        var employee = EmployeeWorkGuard.Current;
        if (employee == null || employee.taskPriority != 7 || employee.thiefProductsNumber != 0) return true;
        __result = Find(__instance, employee, __0);
        return false;
    }

    internal static int[] Find(NPC_Manager manager, NPC_Info employee, int product)
    {
        var route = new EmployeeNavigation(employee.gameObject);
        var roots = new[] { manager.storageOBJ, manager.shelvesOBJ };
        for (int kind = 0; kind < roots.Length; kind++)
        {
            if (roots[kind] == null) continue;
            foreach (Transform child in roots[kind].transform)
            {
                var container = child.GetComponent<Data_Container>();
                if (container == null || container.productInfoArray == null || container.standSpot == null ||
                    !route.CanReach(container.standSpot.transform.position)) continue;
                var contents = container.productInfoArray;
                for (int pair = 0; pair + 1 < contents.Length; pair += 2)
                    if (contents[pair] == product && contents[pair + 1] > 0)
                        return new[] { kind, child.GetSiblingIndex(), pair };
            }
        }
        return new[] { -1, -1, -1 };
    }
}
