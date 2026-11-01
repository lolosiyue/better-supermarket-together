using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine.UI;

namespace BetterSupermarketTogether;

internal enum ModLanguage { Auto, English, TraditionalChinese }

// Only mod-owned labels are translated. Employee names, input fields and chat content
// are never passed through this catalog.
internal static partial class ModText
{
    private static ConfigEntry<ModLanguage> _language;
    private static readonly List<(UnityEngine.Object target, Action refresh)> Refreshers = new List<(UnityEngine.Object, Action)>();
    private static bool? _lastEnglish;

    internal static void Initialize(ConfigFile config) => _language = config.Bind(
        "Localization", "UILanguage", ModLanguage.Auto,
        "Mod interface: Auto follows the game (Chinese or English fallback), English, TraditionalChinese. / 插件介面語言：Auto 跟隨遊戲（中文或英文），English 英文，TraditionalChinese 繁體中文。");

    internal static bool English => ResolveLanguage(_language?.Value ?? ModLanguage.Auto,
        LocalizationManager.instance?.LocalizationDictionary);

    internal static bool ResolveLanguage(ModLanguage language, IDictionary<string, string> gameText)
    {
        if (language != ModLanguage.Auto) return language == ModLanguage.English;
        // Chinese uses a day prefix (第); Japanese day suffixes must not be treated as Chinese.
        return gameText == null || !gameText.TryGetValue("day", out string day) || string.IsNullOrEmpty(day) ||
            !(day.Trim().StartsWith("第", StringComparison.Ordinal) || day.Trim() == "天");
    }

    internal static string LanguageLabel => Get("介面語言：") +
        ((_language?.Value ?? ModLanguage.Auto) == ModLanguage.Auto ? Get("自動（跟隨遊戲）") :
        _language.Value == ModLanguage.English ? "English" : "繁體中文");

    internal static void CycleLanguage()
    {
        if (_language == null) return;
        _language.Value = (ModLanguage)(((int)_language.Value + 1) % 3);
        Refresh();
    }

    internal static string Description(string chinese, string english) => english + " / " + chinese;

    internal static void Bind(Text target, string source)
    {
        Refreshers.RemoveAll(binding => binding.target == target);
        target.text = Get(source);
        if (Catalog.ContainsKey(source)) Refreshers.Add((target, () => target.text = Get(source)));
    }

    internal static void Bind(TMP_Text target, string source)
    {
        Refreshers.RemoveAll(binding => binding.target == target);
        target.text = Get(source);
        if (Catalog.ContainsKey(source)) Refreshers.Add((target, () => target.text = Get(source)));
    }

    internal static void Refresh()
    {
        bool english = English;
        Refreshers.RemoveAll(binding => binding.target == null);
        if (_lastEnglish == english) return;
        _lastEnglish = english;
        foreach (var binding in Refreshers) binding.refresh();
    }

    internal static void ClearBindings() { Refreshers.Clear(); _lastEnglish = null; }

    internal static readonly string[] Categories = {
        "基本商品", "乳製品", "汽水飲料", "冷凍食品", "衛生用品", "清潔用品", "甜食零食", "肉類", "罐頭食品", "茶類",
        "海鮮", "書籍", "酒類", "藥妝", "蔬果", "嬰幼用品", "園藝", "小型家電", "季節限定"
    };

}
