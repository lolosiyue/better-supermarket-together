using System;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class StaffCapacity
{
    private static NPC_Manager _manager;
    private static int _baseLimit;
    private static int _appliedExtras;
    private static int _lastLimit;

    internal static void Reset()
    {
        _manager = null;
        _baseLimit = _appliedExtras = _lastLimit = 0;
    }

    internal static void Apply(NPC_Manager npc, int extras)
    {
        if (!BetterSupermarketPlugin.IsHost || npc == null || !npc.isServer || !npc.initialEmployeesSpawnIsFinished) return;
        if (_manager != npc)
        {
            _manager = npc;
            _baseLimit = npc.maxEmployees;
            _appliedExtras = 0;
            _lastLimit = _baseLimit;
        }
        // Keep perks gained after loading; restore a limit reset by scene initialization.
        if (npc.maxEmployees > _lastLimit) _baseLimit += npc.maxEmployees - _lastLimit;
        int wanted = Math.Max(0, _baseLimit + extras);
        EnsureCapacity(npc, wanted);
        bool changed = npc.maxEmployees != wanted || _appliedExtras != extras;
        npc.maxEmployees = wanted;
        _appliedExtras = extras;
        _lastLimit = wanted;
        if (changed)
        {
            BetterSupermarketPlugin.Log.LogInfo($"Staff: base={_baseLimit}, purchased={extras}, effective={wanted}, data capacity={npc.hiredEmployeesData.Length}.");
            var ui = npc.GetComponent<EmployeesDataGeneration>();
            if (ui != null) ui.UpdateValuesInEmployeesTab();
        }
    }

    internal static void EnsureCapacity(NPC_Manager npc, int requested)
    {
        if (!BetterSupermarketPlugin.IsHost || npc == null) return;
        int size = Math.Max(requested, Math.Max(npc.hiredEmployeesData?.Length ?? 0,
            Math.Max(npc.priorityArray?.Length ?? 0, npc.employeesArray?.Length ?? 0)));
        if (npc.hiredEmployeesData == null || npc.hiredEmployeesData.Length < size)
        {
            var data = npc.hiredEmployeesData ?? Array.Empty<string>();
            int old = data.Length;
            Array.Resize(ref data, size);
            for (int i = old; i < size; i++) data[i] = "";
            npc.NetworkhiredEmployeesData = data;
        }
        if (npc.priorityArray == null || npc.priorityArray.Length < size)
        {
            var priorities = npc.priorityArray ?? Array.Empty<int>();
            Array.Resize(ref priorities, size);
            npc.NetworkpriorityArray = priorities;
        }
        if (npc.employeesArray == null || npc.employeesArray.Length < size)
        {
            var employees = npc.employeesArray ?? Array.Empty<GameObject>();
            Array.Resize(ref employees, size);
            npc.employeesArray = employees;
        }
    }
}

[HarmonyPatch(typeof(SaveBehaviour), "LoadEmployeesDataValues")]
internal static class EmployeeSaveCapacityPatch
{
    private static void Prefix(SaveBehaviour __instance, string[] hiredEmployeesDataValues)
    {
        if (!BetterSupermarketPlugin.IsHost) return;
        var npc = __instance.NPCManager?.GetComponent<NPC_Manager>();
        if (npc != null && hiredEmployeesDataValues != null)
            StaffCapacity.EnsureCapacity(npc, hiredEmployeesDataValues.Length);
    }
}
