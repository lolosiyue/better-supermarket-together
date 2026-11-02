using HarmonyLib;
using StarterAssets;
using UnityEngine;
namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(NPC_Manager),"AttemptToGetRestPosition")]
internal static class CompanionRestCorner
{
    private static bool InRestArea(NPC_Manager manager,Vector3 point)
    {
        if(manager.restSpotOBJ==null)return false;
        var rest=manager.restSpotOBJ.transform;
        if((point-rest.position).sqrMagnitude<=6.5f*6.5f)return true;
        foreach(Transform child in rest)if((point-child.position).sqrMagnitude<=3.5f*3.5f)return true;
        return false;
    }
    private static bool Occupied(NPC_Manager manager,NPC_Info employee,Vector3 point)
    {
        if(manager.employeeParentOBJ!=null)foreach(Transform other in manager.employeeParentOBJ.transform)
            if(other!=employee.transform && (other.position-point).sqrMagnitude<1)return true;
        if(manager.customersnpcParentOBJ!=null)foreach(Transform other in manager.customersnpcParentOBJ.transform)
            if((other.position-point).sqrMagnitude<1)return true;
        var player=FirstPersonController.Instance;
        return player!=null && (player.transform.position-point).sqrMagnitude<1;
    }
    private static void Postfix(NPC_Manager __instance,ref Vector3 __result)
    {
        var companion=BetterSupermarketPlugin.Instance?.Companion;
        var employee=CompanionEmployeeScope.Current;
        if(!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.IsHost || companion==null ||
            employee==null || employee!=companion.BoundEmployee || employee.equippedItem!=0 || employee.employeeDismissed ||
            !companion.UseRestCorner)return;
        var route=new EmployeeNavigation(employee.gameObject);
        if(!route.Ready)return;
        var saved=companion.RestCorner;
        if(saved!=null)
        {
            Vector3 point=new Vector3(saved[0],saved[1],saved[2]);
            if(InRestArea(__instance,point) && route.TrySample(point,out var sampled) &&
                (sampled-point).sqrMagnitude<.25f && route.CanReach(sampled))
            {
                // Keep the preference when somebody temporarily occupies the spot.
                if(!Occupied(__instance,employee,sampled))__result=sampled;
                return;
            }
        }
        if(InRestArea(__instance,__result) && route.TrySample(__result,out var candidate) && route.CanReach(candidate) && !Occupied(__instance,employee,candidate))
        {
            companion.SaveRestCorner(candidate);__result=candidate;
        }
    }
}
