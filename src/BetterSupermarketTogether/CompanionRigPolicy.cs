using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BetterSupermarketTogether;

internal static class CompanionRigPolicy
{
    internal static readonly string[] Required = { "hips", "spine", "head", "leftUpperArm", "leftLowerArm", "leftHand", "rightUpperArm", "rightLowerArm", "rightHand", "leftUpperLeg", "leftLowerLeg", "leftFoot", "rightUpperLeg", "rightLowerLeg", "rightFoot" };

    internal static string Normalize(string name)
    {
        string key = new string((name ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        foreach (string prefix in new[] { "mixamorig", "bip001", "bip01" })
            if (key.StartsWith(prefix, StringComparison.Ordinal)) return key.Substring(prefix.Length);
        return key;
    }

    internal static string Find(string role, IEnumerable<string> available, bool upperChest)
    {
        var names = available.GroupBy(Normalize).ToDictionary(x => x.Key, x => x.First());
        foreach (string alias in Aliases(role, upperChest)) if (names.TryGetValue(Normalize(alias), out string result)) return result;
        return null;
    }

    private static IEnumerable<string> Aliases(string role, bool upperChest)
    {
        if (role == "hips") return new[] { "pelvis", "hips" };
        if (role == "spine") return new[] { "spine" };
        if (role == "chest") return upperChest ? new[] { "chest", "spine1" } : new[] { "chest", "spine2", "spine1" };
        if (role == "upperChest") return new[] { "upperChest", "spine2" };
        if (role == "neck" || role == "head") return new[] { role };
        string side = role.StartsWith("left", StringComparison.Ordinal) ? "left" : role.StartsWith("right", StringComparison.Ordinal) ? "right" : null;
        if (side == null) return new[] { role };
        string shortSide = side == "left" ? "l" : "r", part = role.Substring(side.Length);
        switch (part)
        {
            case "Shoulder": return new[] { side + "Shoulder", shortSide + "Clavicle" };
            case "UpperArm": return new[] { side + "UpperArm", side + "Arm", shortSide + "UpperArm" };
            case "LowerArm": return new[] { side + "LowerArm", side + "ForeArm", shortSide + "Forearm" };
            case "Hand": return new[] { side + "Hand", shortSide + "Hand" };
            case "UpperLeg": return new[] { side + "UpperLeg", side + "UpLeg", shortSide + "Thigh" };
            case "LowerLeg": return new[] { side + "LowerLeg", side + "Leg", shortSide + "Calf" };
            case "Foot": return new[] { side + "Foot", shortSide + "Foot" };
            case "Toes": return new[] { side + "ToeBase", side + "Toes", shortSide + "Toe0" };
        }
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        for (int i = 0; i < fingers.Length; i++)
        {
            if (!part.StartsWith(fingers[i], StringComparison.Ordinal)) continue;
            string joint = part.Substring(fingers[i].Length);
            int segment = joint == "Metacarpal" ? 0 : joint == "Proximal" ? (i == 0 ? 1 : 0) : joint == "Intermediate" ? 1 : 2;
            string biped = segment == 0 ? shortSide + "Finger" + i : segment == 1 ? shortSide + "Finger" + i + "1" : shortSide + "Finger" + i + "2";
            return new[] { role, side + "Hand" + fingers[i] + (segment + 1), biped };
        }
        return new[] { role };
    }

    // Compare rotations in a common character frame, preserving each rig's bind axes.
    internal static Quaternion Rotation(Quaternion current, Quaternion bind, Quaternion alignment, Quaternion targetBind)
    {
        return Quaternion.Normalize(Quaternion.Inverse(alignment) * current * Quaternion.Inverse(bind) * alignment * targetBind);
    }
}

internal sealed class CompanionAvatarRetry
{
    private float _next;
    private int _attempts;
    internal bool Ready { get; private set; }
    internal bool Due(float now) => !Ready && now >= _next;
    internal void Failed(float now, float minimumDelay = 0) { _next = now + Math.Max(minimumDelay, Math.Min(5, ++_attempts)); }
    internal void Succeeded() { Ready = true; }
    internal void Reset() { _next = 0; _attempts = 0; Ready = false; }
}
