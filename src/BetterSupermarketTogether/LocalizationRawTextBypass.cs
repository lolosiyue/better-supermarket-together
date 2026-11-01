using HarmonyLib;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(LocalizationManager), "GetLocalizationString")]
internal static class LocalizationRawTextBypass
{
	private static bool Prefix(ref string key, ref string __result)
	{
        if (!BetterSupermarketPlugin.IsHost) return true;
		if (!string.IsNullOrEmpty(key) && key[0] == '`')
		{
			__result = key.Substring(1);
			return false;
		}
		return true;
	}
}
