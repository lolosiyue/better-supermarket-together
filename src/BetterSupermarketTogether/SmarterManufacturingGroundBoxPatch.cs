using System;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(NPC_Manager), "GetRandomGroundManufacturingBox")]
internal static class SmarterManufacturingGroundBoxPatch
{
	private static bool Prefix(NPC_Manager __instance, GameObject employeeRequesterOBJ, ref GameObject __result)
	{
		if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.SmartRestockEnabled)
		{
			return true;
		}
		try
		{
			if (__instance.manufacturingBoxesOBJ == null || __instance.manufacturingBoxesOBJ.transform.childCount == 0)
			{
				return true;
			}
			__result = GroundBoxHelper.Select(__instance, employeeRequesterOBJ, manufacturing: true, storableOnly: false);
			return false;
		}
		catch (Exception e)
		{
			GroundBoxHelper.Warn(e, "ManufacturingGroundBox");
			return true;
		}
	}
}
