using HarmonyLib;

namespace BetterSupermarketTogether;

internal static class EmployeeNaming
{
    internal static bool Valid(NPC_Manager npc, int index, string name)
    {
        return npc != null && npc.isServer && !string.IsNullOrWhiteSpace(name) && name.Length <= 25 && !name.Contains("|")
            && npc.hiredEmployeesData != null && index >= 0 && index < npc.hiredEmployeesData.Length
            && !string.IsNullOrWhiteSpace(npc.hiredEmployeesData[index])
            && npc.employeesArray != null && index < npc.employeesArray.Length && npc.employeesArray[index] != null;
    }

    internal static void Persist(NPC_Manager npc, int index, string name)
    {
        if (!Valid(npc, index, name)) return;
        string[] record = npc.hiredEmployeesData[index].Split('|');
        if (record.Length <= 9 || record[9] == name) return;
        record[9] = name;
        var data = (string[])npc.hiredEmployeesData.Clone();
        data[index] = string.Join("|", record);
        npc.NetworkhiredEmployeesData = data;
    }

    internal static void Rename(NPC_Manager npc, int index, string name, bool manual = true)
    {
        name = (name ?? "").Trim();
        if (!Valid(npc,index,name)) return;
        // The game's handler reconstructs the saved record using its old name.
        Persist(npc,index,name);
        npc.CmdChangeEmployeeName(index,name);
        if (manual) BetterSupermarketPlugin.Instance.KeepChosenEmployeeNames();
    }
}

[HarmonyPatch(typeof(NPC_Manager), "UserCode_CmdChangeEmployeeName__Int32__String")]
internal static class PersistNativeEmployeeNamePatch
{
    private static void Postfix(NPC_Manager __instance, int index, string newName)
    {
        EmployeeNaming.Persist(__instance,index,newName);
    }
}
