using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSupermarketTogether;

internal static class SettingsPanelUI
{
	private static bool _buttonCreated;
    private static GameObject _boardButton;

	private static GameObject _panel;

	private static GameObject _canvas;

	private static GameObject _backdrop;

	private static GameObject _page1;

	private static GameObject _page2;
	private static GameObject _page3;
	private static int _employeePage;

	private static Font _font;

	private static readonly List<(Toggle toggle, ConfigEntry<bool> entry)> BoolBindings = new List<(Toggle, ConfigEntry<bool>)>();

	private static readonly List<(Text text, Func<float> get, Action<float> set, float step)> FloatBindings = new List<(Text, Func<float>, Action<float>, float)>();

	private static readonly List<(Text text, Func<string> get)> LabelBindings = new List<(Text, Func<string>)>();

	internal static bool PanelOpen
	{
		get
		{
			if (_panel != null)
			{
				return _panel.activeSelf;
			}
			return false;
		}
	}

	private static Font UiFont
	{
		get
		{
			if (_font == null)
			{
				_font = Font.CreateDynamicFontFromOSFont(new string[4] { "Microsoft JhengHei", "Microsoft YaHei", "SimHei", "Segoe UI" }, 14);
			}
			return _font;
		}
	}

	public static void ResetForSceneChange()
	{
        if (_boardButton != null) UnityEngine.Object.Destroy(_boardButton);
        _boardButton = null;
		_buttonCreated = false;
		_panel = null;
		_page1 = null;
		_page2 = null;
		_page3 = null;
		if (_canvas != null)
		{
			UnityEngine.Object.Destroy(_canvas);
		}
		_canvas = null;
		BoolBindings.Clear();
		FloatBindings.Clear();
	}

	public static void EnsureBoardButton()
	{
        if (!BetterSupermarketPlugin.IsHost) return;
        if (PanelOpen) RefreshValues();
		if (_buttonCreated)
		{
			return;
		}
		GameObject gameObject = ((GameData.Instance != null) ? GameData.Instance.gameObject : null);
		if (gameObject == null)
		{
			return;
		}
		ManagerBlackboard component = gameObject.GetComponent<ManagerBlackboard>();
		if (component == null || component.shortcutsParent == null)
		{
			return;
		}
		Transform transform = component.shortcutsParent.transform;
		if (transform.childCount == 0)
		{
			return;
		}
		if (transform.Find("AR2230_Tab") != null)
		{
            _boardButton = transform.Find("AR2230_Tab").gameObject;
			_buttonCreated = true;
			return;
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(transform.GetChild(transform.childCount - 1).gameObject, transform);
		gameObject2.name = "AR2230_Tab";
        _boardButton = gameObject2;
		PlayMakerFSM[] componentsInChildren = gameObject2.GetComponentsInChildren<PlayMakerFSM>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
		Button component2 = gameObject2.GetComponent<Button>();
		if (component2 != null)
		{
			component2.onClick.RemoveAllListeners();
			component2.onClick.AddListener(TogglePanel);
		}
		else
		{
			gameObject2.AddComponent<Button>().onClick.AddListener(TogglePanel);
		}
		SetText(gameObject2.transform, "插件");
		_buttonCreated = true;
		BetterSupermarketPlugin.Log.LogInfo("Settings tab added to the manager board.");
	}

	private static void SetText(Transform root, string text)
	{
		TextMeshProUGUI componentInChildren = root.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.text = text;
			return;
		}
		Text componentInChildren2 = root.GetComponentInChildren<Text>(includeInactive: true);
		if (componentInChildren2 != null)
		{
			componentInChildren2.text = text;
		}
	}

	public static void TogglePanel()
	{
        if (!BetterSupermarketPlugin.IsHost) return;
		if (_panel == null)
		{
			BuildPanel();
		}
		if (_panel == null)
		{
			BetterSupermarketPlugin.Log.LogWarning("Settings panel could not be created (board not found).");
			return;
		}
		bool flag = !_panel.activeSelf;
		_panel.SetActive(flag);
		if (_backdrop != null)
		{
			_backdrop.SetActive(flag);
		}
		if (flag)
		{
			ShowPage(1);
			RefreshValues();
		}
	}

	private static void ShowPage(int page)
    {
        if (_page1 != null) _page1.SetActive(page == 1);
        if (_page2 != null) _page2.SetActive(page == 2);
        if (_page3 != null)
        {
            _page3.SetActive(page == 3);
            if (page == 3) BuildEmployeePage();
        }
        RefreshValues();
    }

    private static void BuildEmployeePage()
    {
        foreach (Transform child in _page3.transform)
        {
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
        var npc = NPC_Manager.Instance;
        NewText(_page3.transform, "員工改名", 22, new Vector2(0f, 292f), new Vector2(800f, 32f), TextAnchor.MiddleCenter);
        AddButton(_page3.transform, "返回", new Vector2(420f, 292f), new Vector2(110f, 32f), new Color(.2f,.35f,.55f), () => ShowPage(1));
        if (npc?.hiredEmployeesData == null) return;
        var employees = new List<int>();
        for (int i = 0; i < npc.hiredEmployeesData.Length; i++)
            if (!string.IsNullOrWhiteSpace(npc.hiredEmployeesData[i])) employees.Add(i);
        _employeePage = Math.Max(0, Math.Min(_employeePage, Math.Max(0, (employees.Count - 1) / 8)));
        NewText(_page3.transform, $"已僱用 {npc.numberOfHiredEmployees} / 上限 {npc.maxEmployees}，第 {_employeePage + 1} 頁", 15, new Vector2(90f, 245f), new Vector2(570f, 28f), TextAnchor.MiddleCenter);
        AddButton(_page3.transform, "隨機風格：" + BetterSupermarketPlugin.Instance.RandomNameStyleLabel,
            new Vector2(-350f,245f), new Vector2(260f,34f), new Color(.25f,.35f,.55f), () =>
            {
                BetterSupermarketPlugin.Instance.ToggleRandomNameStyle();
                BuildEmployeePage();
            });
        for (int n = _employeePage * 8; n < Math.Min(employees.Count, (_employeePage + 1) * 8); n++)
        {
            int index = employees[n];
            float y = 190f - (n % 8) * 50f;
            string[] data = npc.hiredEmployeesData[index].Split('|');
            if (data.Length <= 9) continue;
            NewText(_page3.transform, $"員工 {index + 1}", 15, new Vector2(-350f,y), new Vector2(160f,32f), TextAnchor.MiddleLeft);
            GameObject entry = NewRect(_page3.transform, "Name_" + index, new Vector2(350f, 34f), new Vector2(-90f,y), new Color(.95f,.95f,.95f));
            var input = entry.AddComponent<InputField>();
            Text value = NewText(entry.transform, "", 16, Vector2.zero, new Vector2(330f,32f), TextAnchor.MiddleLeft);
            value.color = Color.black;
            input.textComponent = value;
            input.characterLimit = 25;
            input.text = data[9];
            AddButton(_page3.transform, "改名", new Vector2(170f,y), new Vector2(95f,34f), new Color(.2f,.45f,.35f), () => RenameEmployee(npc,index,input.text));
            AddButton(_page3.transform, "隨機", new Vector2(285f,y), new Vector2(95f,34f), new Color(.2f,.4f,.6f), () => RenameEmployee(npc,index,EmployeeRandomNamePatch.PickRandomName(npc,index)));
        }
        AddButton(_page3.transform, "上一頁", new Vector2(-160f,-270f), new Vector2(120f,34f), new Color(.2f,.35f,.55f), () => { _employeePage--; BuildEmployeePage(); });
        AddButton(_page3.transform, "下一頁", new Vector2(0f,-270f), new Vector2(120f,34f), new Color(.2f,.35f,.55f), () => { _employeePage++; BuildEmployeePage(); });
        AddButton(_page3.transform, "全部隨機改名", new Vector2(240f,-270f), new Vector2(210f,34f), new Color(.2f,.4f,.6f), () =>
        {
            if (!npc.isServer || !BetterSupermarketPlugin.RandomNameButtonEnabled) return;
            foreach (int index in employees) EmployeeNaming.Rename(npc,index, EmployeeRandomNamePatch.PickRandomName(npc,index));
            BuildEmployeePage();
        });
    }

    private static void RenameEmployee(NPC_Manager npc, int index, string name)
    {
        name = (name ?? "").Trim();
        if (!npc.isServer || !BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.RandomNameButtonEnabled || name.Length == 0 || name.Contains("|")) return;
        EmployeeNaming.Rename(npc,index, name);
        BuildEmployeePage();
    }

	private static void RefreshValues()
	{
		foreach (var (toggle, configEntry) in BoolBindings)
		{
            bool available = !IsPricingSetting(configEntry) || BetterSupermarketPlugin.AutoPricingUnlocked;
            toggle.interactable = available;
			toggle.SetIsOnWithoutNotify(available && configEntry.Value);
		}
		foreach (var floatBinding in FloatBindings)
		{
			Text item = floatBinding.text;
			Func<float> item2 = floatBinding.get;
			float item3 = floatBinding.step;
			item.text = FormatValue(item2(), item3);
		}
		foreach (var labelBinding in LabelBindings)
		{
			Text item4 = labelBinding.text;
			Func<string> item5 = labelBinding.get;
			item4.text = item5();
		}
	}

	private static string FormatValue(float value, float step)
	{
		if (!(step < 1f))
		{
			return value.ToString("0");
		}
		return value.ToString("0.#");
	}

	private static void BuildPanel()
	{
        if (!BetterSupermarketPlugin.IsHost) return;
		if (BetterSupermarketPlugin.Instance == null)
		{
			return;
		}
		BoolBindings.Clear();
		FloatBindings.Clear();
		LabelBindings.Clear();
		GameObject gameObject = new GameObject("AR2230_SettingsCanvas", typeof(Canvas), typeof(GraphicRaycaster));
		gameObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
		gameObject.GetComponent<Canvas>().sortingOrder = 9000;
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		_canvas = gameObject;
		GameObject gameObject2 = new GameObject("Backdrop", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)gameObject2.transform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		gameObject2.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.55f);
		_backdrop = gameObject2;
		_panel = NewRect(gameObject.transform, "Panel", new Vector2(1010f, 640f), Vector2.zero, new Color(0.1f, 0.12f, 0.15f, 0.97f));
		_page1 = NewStretch(_panel.transform, "Page1");
		_page2 = NewStretch(_panel.transform, "Page2");
		_page3 = NewStretch(_panel.transform, "Page3");
		NewText(_page1.transform, "Better Supermarket Together 設定", 20, new Vector2(-330f, 292f), new Vector2(600f, 30f), TextAnchor.MiddleLeft);
		AddButton(_page1.transform, "X", new Vector2(470f, 292f), new Vector2(44f, 32f), new Color(0.6f, 0.2f, 0.2f), TogglePanel);
		AddButton(_page1.transform, "第2頁：分類最低庫存", new Vector2(0f, 292f), new Vector2(240f, 32f), new Color(0.2f, 0.35f, 0.55f), () =>
		{
			ShowPage(2);
		});
		AddButton(_page1.transform, "員工改名", new Vector2(290f, 292f), new Vector2(160f, 32f), new Color(0.2f, 0.4f, 0.6f), () => ShowPage(3));
        NewText(_page1.transform, "F10 → 員工改名：可自行輸入或隨機改名", 13, new Vector2(0f, -260f), new Vector2(900f, 28f), TextAnchor.MiddleCenter);
        Text productionStatus = NewText(_page1.transform, "", 13, new Vector2(0f, -290f), new Vector2(900f, 28f), TextAnchor.MiddleCenter);
        LabelBindings.Add((productionStatus, () => "自動生產：" + ProductionAutomation.Status));
        List<(string, ConfigEntry<bool>)> list = new List<(string, ConfigEntry<bool>)>();
		BetterSupermarketPlugin.Instance.CollectBoolSettings(list);
		List<(string, Func<float>, Action<float>, float)> list2 = new List<(string, Func<float>, Action<float>, float)>();
		BetterSupermarketPlugin.Instance.CollectFloatSettings(list2);
		int num = 0;
		int num2 = 0;
		foreach (var item11 in list)
		{
			string item = item11.Item1;
			ConfigEntry<bool> item2 = item11.Item2;
			float x = ((num2 == 0) ? (-488) : 18);
			float y = 252 - num * 38;
			AddBoolRow(_page1.transform, item, item2, new Vector2(x, y));
			num++;
			if (num >= 8)
			{
				num = 0;
				num2 = 1;
			}
		}
		num = 0;
		num2 = 0;
		foreach (var item12 in list2)
		{
			string item3 = item12.Item1;
			Func<float> item4 = item12.Item2;
			Action<float> item5 = item12.Item3;
			float item6 = item12.Item4;
			float x2 = ((num2 == 0) ? (-488) : 18);
			float y2 = 252 - (8 + num) * 38;
			AddFloatRow(_page1.transform, item3, item4, item5, item6, new Vector2(x2, y2));
			num++;
			if (num >= 4)
			{
				num = 0;
				num2 = 1;
			}
		}
		GameObject gameObject3 = NewRect(_page1.transform, "row_staff", new Vector2(470f, 30f), new Vector2(-488f, -204f), new Color(1f, 0.85f, 0.4f, 0.1f));
		Text staffText = NewText(gameObject3.transform, "", 14, new Vector2(-95f, 0f), new Vector2(300f, 30f), TextAnchor.MiddleLeft);
		Func<string> staffLabel = () =>
		{
			string lastStaffPurchaseMessage = BetterSupermarketPlugin.Instance.LastStaffPurchaseMessage;
			return (!string.IsNullOrEmpty(lastStaffPurchaseMessage)) ? lastStaffPurchaseMessage : ("僱用上限 +1（$" + BetterSupermarketPlugin.Instance.ExtraSlotCost + "）［僅主機］");
		};
		AddButton(gameObject3.transform, "購買", new Vector2(195f, 0f), new Vector2(60f, 26f), new Color(0.25f, 0.5f, 0.3f), () =>
		{
			BetterSupermarketPlugin.Instance.PurchaseEmployeeSlotFromUi();
			staffText.text = staffLabel();
		});
		LabelBindings.Add((staffText, staffLabel));
		NewText(_page2.transform, "分類最低備用庫存（倉庫＋普通箱子＋托盤原料，不含銷售貨架）", 16, new Vector2(0f, 292f), new Vector2(900f, 30f), TextAnchor.MiddleCenter);
		List<(string, Func<float>, Action<float>, float)> list3 = new List<(string, Func<float>, Action<float>, float)>();
		BetterSupermarketPlugin.Instance.CollectGroupMinStockSettings(list3);
		num = 0;
		num2 = 0;
		foreach (var item13 in list3)
		{
			string item7 = item13.Item1;
			Func<float> item8 = item13.Item2;
			Action<float> item9 = item13.Item3;
			float item10 = item13.Item4;
			float x3 = ((num2 == 0) ? (-488) : 18);
			float y3 = 240 - num * 38;
			AddFloatRow(_page2.transform, item7, item8, item9, item10, new Vector2(x3, y3));
			num++;
			if (num >= 10)
			{
				num = 0;
				num2 = 1;
			}
		}
		AddButton(_page2.transform, "返回", new Vector2(470f, -286f), new Vector2(120f, 32f), new Color(0.2f, 0.35f, 0.55f), () =>
		{
			ShowPage(1);
		});
		NewText(_page2.transform, "「生產原料最低庫存」適用所有生產原料；與商品分類門檻取較高值。", 12, new Vector2(0f, -296f), new Vector2(800f, 24f), TextAnchor.MiddleCenter);
		_page1.SetActive(value: true);
		_page2.SetActive(value: false);
		_page3.SetActive(value: false);
		_panel.SetActive(value: false);
	}

	private static GameObject NewStretch(Transform parent, string name)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		return gameObject;
	}

	private static GameObject NewRect(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		rectTransform.sizeDelta = size;
		rectTransform.anchoredPosition = pos;
		gameObject.AddComponent<Image>().color = color;
		return gameObject;
	}

	private static Text NewText(Transform parent, string content, int fontSize, Vector2 pos, Vector2 size, TextAnchor anchor)
	{
		GameObject gameObject = new GameObject("text", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		rectTransform.sizeDelta = size;
		rectTransform.anchoredPosition = pos;
		Text text = gameObject.AddComponent<Text>();
		text.font = UiFont;
		text.fontSize = fontSize;
		text.color = Color.white;
		text.alignment = anchor;
		text.text = content;
		return text;
	}

	private static Button AddButton(Transform parent, string label, Vector2 pos, Vector2 size, Color color, Action onClick)
	{
		GameObject gameObject = NewRect(parent, "btn_" + label, size, pos, color);
		Button button = gameObject.AddComponent<Button>();
		ColorBlock colors = button.colors;
		colors.highlightedColor = new Color(color.r + 0.15f, color.g + 0.15f, color.b + 0.15f);
		colors.pressedColor = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f);
		button.colors = colors;
		button.onClick.AddListener(() =>
		{
			if (BetterSupermarketPlugin.IsHost) onClick();
		});
		if (!string.IsNullOrEmpty(label))
		{
			NewText(gameObject.transform, label, 14, Vector2.zero, size, TextAnchor.MiddleCenter).alignment = TextAnchor.MiddleCenter;
		}
		return button;
	}

	private static void AddBoolRow(Transform parent, string label, ConfigEntry<bool> entry, Vector2 pos)
	{
		GameObject gameObject = NewRect(parent, "row_" + label, new Vector2(470f, 30f), pos, new Color(1f, 1f, 1f, 0.06f));
		GameObject gameObject2 = NewRect(gameObject.transform, "toggle", new Vector2(22f, 22f), new Vector2(-222f, 0f), new Color(0.25f, 0.28f, 0.32f));
		Toggle toggle = gameObject2.AddComponent<Toggle>();
		toggle.targetGraphic = gameObject2.GetComponent<Image>();
		GameObject gameObject3 = NewRect(gameObject2.transform, "check", new Vector2(14f, 14f), Vector2.zero, new Color(0.3f, 0.75f, 0.35f));
		toggle.graphic = gameObject3.GetComponent<Image>();
        bool pricing = IsPricingSetting(entry);
        bool available = !pricing || BetterSupermarketPlugin.AutoPricingUnlocked;
        toggle.interactable = available;
		toggle.isOn = available && entry.Value;
		toggle.onValueChanged.AddListener((bool v) =>
		{
            if (!BetterSupermarketPlugin.IsHost) return;
            if (pricing && !BetterSupermarketPlugin.AutoPricingUnlocked) { RefreshValues(); return; }
			entry.Value = v;
		});
		BoolBindings.Add((toggle, entry));
        var text = NewText(gameObject.transform, label, 14, new Vector2(-5f, 0f), new Vector2(400f, 30f), TextAnchor.MiddleLeft);
        if (pricing) LabelBindings.Add((text, () => BetterSupermarketPlugin.AutoPricingUnlocked ? label : label + "（需 MART OS 2.0）"));
	}

    private static bool IsPricingSetting(ConfigEntry<bool> entry) =>
        entry.Definition.Section == "Pricing" && entry.Definition.Key == "Enable";

	private static void AddFloatRow(Transform parent, string label, Func<float> get, Action<float> set, float step, Vector2 pos)
	{
		GameObject gameObject = NewRect(parent, "row_" + label, new Vector2(470f, 30f), pos, new Color(1f, 1f, 1f, 0.06f));
		NewText(gameObject.transform, label, 14, new Vector2(-75f, 0f), new Vector2(310f, 30f), TextAnchor.MiddleLeft);
		Text valueText = NewText(gameObject.transform, FormatValue(get(), step), 14, new Vector2(160f, 0f), new Vector2(50f, 30f), TextAnchor.MiddleCenter);
		AddButton(gameObject.transform, "−", new Vector2(105f, 0f), new Vector2(34f, 26f), new Color(0.35f, 0.35f, 0.4f), () =>
		{
			set(get() - step);
			valueText.text = FormatValue(get(), step);
		});
		AddButton(gameObject.transform, "+", new Vector2(215f, 0f), new Vector2(34f, 26f), new Color(0.35f, 0.35f, 0.4f), () =>
		{
			set(get() + step);
			valueText.text = FormatValue(get(), step);
		});
		FloatBindings.Add((valueText, get, set, step));
	}
}

