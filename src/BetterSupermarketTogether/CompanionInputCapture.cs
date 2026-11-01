using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

// Intercept action reads, not Unity's text/mouse events used by InputField.
internal static class CompanionInputCapture
{
    private static bool _releasePending;
    private static int _releaseFrame = -1;
    internal static bool Blocked
    {
        get
        {
            if (SettingsPanelUI.CompanionOpen || SettingsPanelUI.Typing)
            {
                _releasePending = true;
                return true;
            }
            if (_releasePending)
            {
                if (Input.anyKey) return true;
                _releasePending = false;
                _releaseFrame = Time.frameCount;
            }
            return Time.frameCount <= _releaseFrame;
        }
    }

    internal static IEnumerable<MethodBase> Targets(Type resultType)
    {
        Type player = AccessTools.TypeByName("Rewired.Player");
        if (player == null) throw new InvalidOperationException("Rewired.Player input API is missing.");
        return player.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == resultType && (resultType == typeof(bool)
                ? m.Name.StartsWith("GetButton", StringComparison.Ordinal) ||
                  m.Name.StartsWith("GetNegativeButton", StringComparison.Ordinal) ||
                  m.Name.StartsWith("GetAnyButton", StringComparison.Ordinal)
                : m.Name.StartsWith("GetAxis", StringComparison.Ordinal)))
            .Cast<MethodBase>();
    }
}

[HarmonyPatch]
internal static class CompanionButtonCapturePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => CompanionInputCapture.Targets(typeof(bool));
    private static bool Prefix(ref bool __result)
    {
        if (!CompanionInputCapture.Blocked) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal static class CompanionAxisCapturePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => CompanionInputCapture.Targets(typeof(float));
    private static bool Prefix(ref float __result)
    {
        if (!CompanionInputCapture.Blocked) return true;
        __result = 0f;
        return false;
    }
}

[HarmonyPatch]
internal static class CompanionAxis2DCapturePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => CompanionInputCapture.Targets(typeof(Vector2));
    private static bool Prefix(ref Vector2 __result)
    {
        if (!CompanionInputCapture.Blocked) return true;
        __result = Vector2.zero;
        return false;
    }
}
