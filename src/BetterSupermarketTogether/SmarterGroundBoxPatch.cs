using System;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(NPC_Manager), "GetRandomGroundBox")]
internal static class SmarterGroundBoxPatch
{
	private static bool Prefix(NPC_Manager __instance, GameObject employeeRequesterOBJ, ref GameObject __result)
	{
		if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled)
		{
			return true;
		}
		try
		{
			if (__instance.boxesOBJ == null || __instance.boxesOBJ.transform.childCount == 0)
			{
				return true;
			}
			__result = GroundBoxHelper.Select(__instance, employeeRequesterOBJ, manufacturing: false, storableOnly: false);
			return false;
		}
		catch (Exception e)
		{
			GroundBoxHelper.Warn(e, "GroundBox");
			return true;
		}
	}
}
