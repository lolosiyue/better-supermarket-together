using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(EmployeesDataGeneration), "SpawnUIDismiss")]
internal static class EmployeeRandomNamePatch
{
	[HarmonyPatch(typeof(EmployeesDataGeneration), "SpawnUIAssignment")]
	internal static class EmployeeAssignmentRandomNamePatch
	{
		private static void Postfix(EmployeesDataGeneration __instance)
		{
			if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.RandomNameButtonEnabled)
			{
				return;
			}
			NPC_Manager npc = NPC_Manager.Instance;
			if (npc == null || __instance.assignmentParentOBJ == null)
			{
				return;
			}
			Transform transform = __instance.assignmentParentOBJ.transform;
			int rowStart = RepairPolicies.NewRowsStart(transform.childCount, npc.hiredEmployeesData.Length);
			for (int i = 0; i < npc.hiredEmployeesData.Length; i++)
			{
				GameObject rowGo = transform.GetChild(rowStart + i).gameObject;
				if (rowGo.GetComponentInChildren<Button>()?.name == "AR2230_RandomName")
				{
					continue;
				}
				Transform transform2 = rowGo.transform.Find("EmptyPosition");
				if (transform2 != null && transform2.gameObject.activeSelf)
				{
					continue;
				}
				Transform transform3 = rowGo.transform.Find("EmployeeNameField");
				Vector2 pos = new Vector2(0f, 0f);
				if (transform3 != null)
				{
					RectTransform rectTransform = (RectTransform)transform3;
					pos = rectTransform.anchoredPosition + new Vector2(rectTransform.sizeDelta.x * 0.5f + 26f, 0f);
				}
				GameObject gameObject = NewRect(transform3 ?? rowGo.transform, "AR2230_RandomName", new Vector2(56f, 26f), pos, new Color(0.2f, 0.4f, 0.6f));
				AnchorNameButton(gameObject);
				Button button = gameObject.AddComponent<Button>();
				int dataIndex = i;
				button.onClick.AddListener(() =>
				{
                    if (!BetterSupermarketPlugin.EnablePlugin) return;
					string text = PickRandomName(npc, dataIndex);
					EmployeeNaming.Rename(npc,dataIndex, text);
					Transform transform4 = rowGo.transform.Find("EmployeeNameField/TextArea/Placeholder");
					if (transform4 != null)
					{
						transform4.GetComponent<TextMeshProUGUI>().text = text;
					}
				});
				AddText(gameObject.transform, "改名", 12);
			}
		}
	}

	private static Font _uiFont;

	private static readonly System.Random Rng = new System.Random();

	private static Font UiFont
	{
		get
		{
			if (_uiFont == null)
			{
				_uiFont = Font.CreateDynamicFontFromOSFont(new string[4] { "Microsoft JhengHei", "Microsoft YaHei", "SimHei", "Segoe UI" }, 13);
			}
			return _uiFont;
		}
	}

	private static void Postfix(EmployeesDataGeneration __instance)
	{
		if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.RandomNameButtonEnabled)
		{
			return;
		}
		NPC_Manager npc = NPC_Manager.Instance;
		if (npc == null || __instance.dismissParentOBJ == null)
		{
			return;
		}
		Transform parent = __instance.dismissParentOBJ.transform;
		Transform previous = parent.parent.Find("AR2230_RerollAll");
		if (previous != null) { previous.gameObject.SetActive(false); UnityEngine.Object.Destroy(previous.gameObject); }
		GameObject gameObject = NewRect(parent.parent, "AR2230_RerollAll", new Vector2(190f, 34f), new Vector2(0f, 320f), new Color(0.2f, 0.4f, 0.6f));
		if (gameObject != null)
		{
			gameObject.AddComponent<Button>().onClick.AddListener(() =>
			{
                if (!BetterSupermarketPlugin.EnablePlugin) return;
				for (int i = 0; i < npc.hiredEmployeesData.Length; i++)
				{
					if (!string.IsNullOrWhiteSpace(npc.hiredEmployeesData[i]))
					{
						string text = PickRandomName(npc, i);
						EmployeeNaming.Rename(npc,i, text);
						RefreshRowName(parent, i, text);
					}
				}
			});
			AddText(gameObject.transform, "全部隨機改名", 14);
		}
		int rowStart = RepairPolicies.NewRowsStart(parent.childCount, npc.hiredEmployeesData.Length);
		for (int num = 0; num < npc.hiredEmployeesData.Length; num++)
		{
			GameObject rowGo = parent.GetChild(rowStart + num).gameObject;
			Transform transform = rowGo.transform.Find("EmptyPosition");
			if (transform != null && transform.gameObject.activeSelf)
			{
				continue;
			}
			Transform transform2 = rowGo.transform.Find("EmployeeName");
			Vector2 pos = new Vector2(0f, 0f);
			if (transform2 != null)
			{
				RectTransform rectTransform = (RectTransform)transform2;
				pos = rectTransform.anchoredPosition + new Vector2(rectTransform.sizeDelta.x * 0.5f + 30f, 0f);
			}
			GameObject gameObject2 = NewRect(transform2 ?? rowGo.transform, "AR2230_RandomName", new Vector2(56f, 26f), pos, new Color(0.2f, 0.4f, 0.6f));
			AnchorNameButton(gameObject2);
			Button button = gameObject2.AddComponent<Button>();
			int dataIndex = num;
			button.onClick.AddListener(() =>
			{
                if (!BetterSupermarketPlugin.EnablePlugin) return;
				string text = PickRandomName(npc, dataIndex);
				EmployeeNaming.Rename(npc,dataIndex, text);
				Transform transform3 = rowGo.transform.Find("EmployeeName");
				if (transform3 != null)
				{
					transform3.GetComponent<TextMeshProUGUI>().text = text;
				}
			});
			AddText(gameObject2.transform, "改名", 12);
		}
	}

    internal static string PickRandomName(NPC_Manager npc, int exceptIndex)
    {
        EmployeeGender gender = EmployeeNameCatalog.Resolve(npc, exceptIndex);
        bool english = BetterSupermarketPlugin.UsesEnglishRandomNames;
        var preferred = BetterSupermarketPlugin.GetRandomNamePool(gender);
        var expanded = EmployeeNameCatalog.ExpandedNames(gender, english);
        var existing = new List<string>();
        if (npc != null && npc.hiredEmployeesData != null)
        {
            for (int i = 0; i < npc.hiredEmployeesData.Length; i++)
            {
                string record = npc.hiredEmployeesData[i];
                if (!string.IsNullOrWhiteSpace(record))
                {
                    string[] data = record.Split('|');
                    if (data.Length > 9) existing.Add(data[9]);
                }
                // Include current display names too; native rename Commands complete asynchronously.
                if (npc.employeesArray == null || i >= npc.employeesArray.Length || npc.employeesArray[i] == null) continue;
                var employee = npc.employeesArray[i].GetComponent<NPC_Info>();
                if (employee != null) existing.Add(employee.NetworkNPCName);
            }
        }
        return EmployeeNamePicker.Pick(preferred, expanded, existing, english, Rng);
    }

	private static void RefreshRowName(Transform parent, int index, string name)
	{
		if (index < parent.childCount)
		{
			Transform transform = parent.GetChild(RepairPolicies.NewRowsStart(parent.childCount, NPC_Manager.Instance.hiredEmployeesData.Length) + index).Find("EmployeeName");
			if (transform != null)
			{
				transform.GetComponent<TextMeshProUGUI>().text = name;
			}
		}
	}

	private static void AnchorNameButton(GameObject button)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-2f, 0f);
        rect.SetAsLastSibling();
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

	private static void AddText(Transform parent, string content, int fontSize)
	{
		GameObject gameObject = new GameObject("text", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		Text text = gameObject.AddComponent<Text>();
		text.font = UiFont;
		text.fontSize = fontSize;
		text.color = Color.white;
		text.alignment = TextAnchor.MiddleCenter;
		text.raycastTarget = false;
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 10;
		text.resizeTextMaxSize = fontSize;
		ModText.Bind(text, content);
	}
}

