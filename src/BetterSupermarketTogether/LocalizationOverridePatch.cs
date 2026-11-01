using HarmonyLib;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(LocalizationManager), "GetLocalizationString")]
[HarmonyPriority(600)]
internal static class LocalizationOverridePatch
{
	private static bool Prefix(LocalizationManager __instance, string key, ref string __result)
	{
		if (!BetterSupermarketPlugin.EnablePlugin || !LocalizationOverride.Loaded)
		{
			return true;
		}
		if (!LocalizationOverride.IsChinese(__instance))
		{
			return true;
		}
		if (!LocalizationOverride.Table.TryGetValue(key, out var value))
		{
			return true;
		}
		__result = value;
		return false;
	}
}
