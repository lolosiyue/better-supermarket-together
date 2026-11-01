using System;
using System.Collections.Generic;

namespace BetterSupermarketTogether;

internal static class RepairPolicies
{
    internal static string NormalizeRecipe(string recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe) || recipe.Trim() == "999") return "999";
        var ids = new SortedSet<int>();
        foreach (string token in recipe.Split('-'))
        {
            if (!int.TryParse(token, out int id) || id < 0) return null;
            if (id != 999) ids.Add(id);
        }
        return ids.Count == 0 ? "999" : string.Join("-", ids);
    }

    internal static Dictionary<string, string> ReadVariants(string current, string legacy)
    {
        var variants = new Dictionary<string, string>();
        foreach (string entry in ((current ?? "") + ";" + (legacy ?? "")).Split(';'))
        {
            int split = entry.IndexOf('|');
            if (split < 0) split = entry.IndexOf(':');
            if (split <= 0 || !int.TryParse(entry.Substring(0, split), out int id) || id < 0) continue;
            string recipe = NormalizeRecipe(entry.Substring(split + 1));
            if (recipe != null) variants[id + "|" + recipe] = recipe;
        }
        return variants;
    }

    internal static int NewRowsStart(int childCount, int employeeCount)
    {
        return Math.Max(0, childCount - employeeCount);
    }
}
