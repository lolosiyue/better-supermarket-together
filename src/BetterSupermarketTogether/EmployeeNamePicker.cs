using System;
using System.Collections.Generic;

namespace BetterSupermarketTogether;

internal static class EmployeeNamePicker
{
    internal static string Pick(IEnumerable<string> preferred, IEnumerable<string> expanded,
        IEnumerable<string> existing, bool english, Random random)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedGiven = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string value in existing)
        {
            string name = (value ?? "").Trim();
            if (name.Length == 0) continue;
            used.Add(name);
            usedGiven.Add(GivenName(name, english));
        }
        string choice = Choose(preferred, used, usedGiven, english, random, true) ??
            Choose(preferred, used, usedGiven, english, random, false) ??
            Choose(expanded, used, usedGiven, english, random, true) ??
            Choose(expanded, used, usedGiven, english, random, false);
        if (choice != null) return choice;

        // Custom pools may be deliberately tiny. Never return an already-used full name.
        string seed = english ? "Alex Morgan" : "陳子安";
        foreach (string value in preferred)
            if (Valid(value)) { seed = value.Trim(); break; }
        for (int suffix = 2; ; suffix++)
        {
            string number = " " + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            choice = seed.Substring(0, Math.Min(seed.Length, 25 - number.Length)).TrimEnd() + number;
            if (!used.Contains(choice)) return choice;
        }
    }

    private static string Choose(IEnumerable<string> pool, HashSet<string> used, HashSet<string> usedGiven,
        bool english, Random random, bool freshGiven)
    {
        string result = null;
        int available = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string value in pool)
        {
            if (!Valid(value)) continue;
            string name = value.Trim();
            if (!seen.Add(name) || used.Contains(name) || freshGiven && usedGiven.Contains(GivenName(name, english))) continue;
            if (random.Next(++available) == 0) result = name;
        }
        return result;
    }

    private static bool Valid(string name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 25 && !name.Contains("|");

    private static string GivenName(string name, bool english)
    {
        if (english)
        {
            int space = name.IndexOf(' ');
            return space < 0 ? name : name.Substring(0, space);
        }
        return name.Length > 2 ? name.Substring(name.Length - 2) : name;
    }
}
