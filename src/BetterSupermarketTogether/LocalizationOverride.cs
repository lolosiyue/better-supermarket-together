using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace BetterSupermarketTogether;

internal static class LocalizationOverride
{
	internal static Dictionary<string, string> Table = new Dictionary<string, string>();

	internal static bool Loaded;

	public static void LoadFromFile(ConfigEntry<bool> enabledEntry, string path)
	{
		if (!enabledEntry.Value || !File.Exists(path))
		{
			BetterSupermarketPlugin.Log.LogInfo("i18n: table not loaded (" + (File.Exists(path) ? "disabled" : (path + " not found")) + ").");
			return;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array = File.ReadAllLines(path);
		foreach (string text in array)
		{
			if (string.IsNullOrEmpty(text) || text[0] == '#')
			{
				continue;
			}
			int num = text.IndexOf('\t');
			if (num > 0)
			{
				string key = text.Substring(0, num);
				string value = text.Substring(num + 1);
				if (!dictionary.ContainsKey(key))
				{
					dictionary[key] = value;
				}
			}
		}
		if (dictionary.Count == 0)
		{
			BetterSupermarketPlugin.Log.LogInfo("i18n: table file is empty.");
			return;
		}
		Table = dictionary;
		Loaded = true;
		BetterSupermarketPlugin.Log.LogInfo($"i18n: loaded {dictionary.Count} Traditional Chinese overrides.");
	}

	internal static bool IsChinese(LocalizationManager manager)
	{
		if (manager == null || manager.LocalizationDictionary == null)
		{
			return false;
		}
		if (manager.LocalizationDictionary.TryGetValue("day", out var value))
		{
			return ContainsCJK(value);
		}
		return false;
	}

	internal static bool ContainsCJK(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		foreach (char c in text)
		{
			if (c >= '一' && c <= '鿿')
			{
				return true;
			}
		}
		return false;
	}
}
