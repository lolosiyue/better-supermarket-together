using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class ProductionAutomation
{
	internal static bool Warned;
    internal static string Status = "等待生產部載入";
    private static bool _dirty;

    internal static void Reset()
    {
        KnownVariants.Clear();
        _variantsLoaded = false;
        _ingredientPids = null;
        Status = "等待生產部載入";
    }

    private static void SaveDiscovered()
    {
        if (!_dirty) return;
        PersistVariants?.Invoke(ExtractForPersist());
        _dirty = false;
    }


	internal static int QueueThreshold = 0;

	private static MethodInfo _cmdQueue;

	private static MethodInfo _cmdTray;

	private static bool _variantsLoaded;

	internal static readonly Dictionary<string, string> KnownVariants = new Dictionary<string, string>();

	internal static Func<Dictionary<string, string>> LoadVariants;

	internal static Action<Dictionary<string, string>> PersistVariants;

	private static HashSet<int> _ingredientPids;

	internal static bool IsProductionIngredient(int productId)
	{
		if (_ingredientPids == null)
		{
			_ingredientPids = new HashSet<int>();
			ManufacturingBase instance = ManufacturingBase.Instance;
			if (instance != null && AccessTools.Field(typeof(ManufacturingBase), "baseRecipes")?.GetValue(instance) is string[] array)
			{
				string[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					string[] array3 = array2[i].Split(new char[1] { '|' });
					for (int j = 0; j < array3.Length; j++)
					{
						string[] array4 = array3[j].Split(new char[1] { '-' });
						for (int k = 0; k < array4.Length; k++)
						{
							if (int.TryParse(array4[k], out var result) && result >= 0)
							{
								_ingredientPids.Add(result);
							}
						}
					}
				}
			}
		}
		return _ingredientPids.Contains(productId);
	}

	public static void LoadPersistedVariants()
	{
		if (_variantsLoaded)
		{
			return;
		}
		_variantsLoaded = true;
		if (LoadVariants == null)
		{
			return;
		}
		foreach (KeyValuePair<string, string> item in LoadVariants())
		{
			string key = item.Key;
			if (!KnownVariants.ContainsKey(key))
			{
				KnownVariants[key] = item.Value;
			}
		}
	}

	private static void Learn(int productId, string combinable)
	{
		LoadPersistedVariants();
		if (productId < 0) return;
        combinable = RepairPolicies.NormalizeRecipe(combinable);
        if (combinable == null) return;
        string key = productId + "|" + combinable;
		if (!KnownVariants.ContainsKey(key))
		{
			KnownVariants[key] = combinable;
			_dirty = true;
		}
	}

	private static Dictionary<string, string> ExtractForPersist()
	{
		return new Dictionary<string, string>(KnownVariants);
	}

	public static void Run(NPC_Manager npc, ManagerBlackboard mb, int maxQueue, int maxTray, bool notify)
	{
		ManufacturingBase instance = ManufacturingBase.Instance;
		if (instance == null || !instance.isServer || instance.productsData == null || npc.storageOBJ == null || npc.manufacturingShelvesOBJ == null || npc.manufacturingStorageShelvesOBJ == null)
		{
			return;
		}
		ManufacturingProduction[] array = UnityEngine.Object.FindObjectsByType<ManufacturingProduction>(FindObjectsSortMode.None);
		if (array == null || array.Length == 0)
		{
			Status = "沒有可用的生產工作台";
			return;
		}
		if (_cmdQueue == null)
		{
			_cmdQueue = AccessTools.Method(typeof(ManufacturingProduction), "CmdAddToProductionQueue");
		}
		if (_cmdTray == null)
		{
			_cmdTray = AccessTools.Method(typeof(ManufacturingProduction), "CmdAddItemToTable");
		}
		if (_cmdQueue == null || _cmdTray == null)
		{
			return;
		}
		LoadPersistedVariants();
		if (npc.manufacturingBoxesOBJ != null)
		{
			Transform transform = npc.manufacturingBoxesOBJ.transform;
			for (int i = 0; i < transform.childCount; i++)
			{
				ManufacturingBoxData component = transform.GetChild(i).GetComponent<ManufacturingBoxData>();
				if (component != null)
				{
					Learn(component.manufacturedProductIndex, component.combinablesData);
				}
			}
		}
		ManufacturingProduction[] array2 = array;
		foreach (ManufacturingProduction manufacturingProduction in array2)
		{
			if (manufacturingProduction.productQueue != null && manufacturingProduction.combinableQueue != null)
			{
				for (int k = 0; k < manufacturingProduction.productQueue.Count && k < manufacturingProduction.combinableQueue.Count; k++)
				{
					Learn(manufacturingProduction.productQueue[k], manufacturingProduction.combinableQueue[k]);
				}
			}
		}
		if (npc.manufacturingDesksList != null)
		{
			foreach (GameObject manufacturingDesks in npc.manufacturingDesksList)
			{
				if (manufacturingDesks == null)
				{
					continue;
				}
				ManufacturingDesk component2 = manufacturingDesks.GetComponent<ManufacturingDesk>();
				if (!(component2 == null) && component2.toSendProductsQueue != null && component2.toSendCombinableQueue != null)
				{
					for (int l = 0; l < component2.toSendProductsQueue.Count && l < component2.toSendCombinableQueue.Count; l++)
					{
						Learn(component2.toSendProductsQueue[l], component2.toSendCombinableQueue[l]);
					}
				}
			}
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		AccumulateVariantStock(npc.manufacturingStorageShelvesOBJ.transform, dictionary);
		AccumulateVariantStock(npc.manufacturingShelvesOBJ.transform, dictionary);
		if (npc.manufacturingBoxesOBJ != null)
		{
			Transform transform2 = npc.manufacturingBoxesOBJ.transform;
			for (int m = 0; m < transform2.childCount; m++)
			{
				ManufacturingBoxData component3 = transform2.GetChild(m).GetComponent<ManufacturingBoxData>();
				if (!(component3 == null))
				{
					string key = component3.manufacturedProductIndex + "|" + RepairPolicies.NormalizeRecipe(component3.combinablesData);
					dictionary[key] = MfgS(dictionary, key) + component3.numberOfProducts;
				}
			}
		}
		SaveDiscovered();
		Status = $"已知配方 {KnownVariants.Count}，門檻 {QueueThreshold}";
		int num = 0;
		foreach (KeyValuePair<string, string> knownVariant in KnownVariants)
		{
			if (num >= maxQueue)
			{
				break;
			}
			string key2 = knownVariant.Key;
			int num2 = key2.IndexOf('|');
			if (num2 <= 0 || !int.TryParse(key2.Substring(0, num2), out var result))
			{
				continue;
			}
			string text = key2.Substring(num2 + 1);
            if (instance.unlockedBaseProducts == null || result >= instance.unlockedBaseProducts.Length || !instance.unlockedBaseProducts[result]) continue;
			bool flag = false;
			array2 = array;
			foreach (ManufacturingProduction manufacturingProduction2 in array2)
			{
				if (manufacturingProduction2.productQueue == null || manufacturingProduction2.combinableQueue == null)
				{
					continue;
				}
				for (int n = 0; n < manufacturingProduction2.productQueue.Count && n < manufacturingProduction2.combinableQueue.Count; n++)
				{
					if (manufacturingProduction2.productQueue[n] == result && RepairPolicies.NormalizeRecipe(manufacturingProduction2.combinableQueue[n]) == text)
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					break;
				}
			}
			if (flag)
			{
				continue;
			}
			bool waitingAtDesk = false;
            if (npc.manufacturingDesksList != null)
                foreach (var deskObject in npc.manufacturingDesksList)
                {
                    var desk = deskObject?.GetComponent<ManufacturingDesk>();
                    if (desk?.toSendProductsQueue == null || desk.toSendCombinableQueue == null) continue;
                    for (int j = 0; j < desk.toSendProductsQueue.Count && j < desk.toSendCombinableQueue.Count; j++)
                        if (desk.toSendProductsQueue[j] == result && RepairPolicies.NormalizeRecipe(desk.toSendCombinableQueue[j]) == text) waitingAtDesk = true;
                }
            if (waitingAtDesk) continue;
            int num3 = MfgS(dictionary, key2);
			if (num3 > QueueThreshold)
			{
				continue;
			}
			ManufacturingProduction manufacturingProduction3 = null;
			int num4 = int.MaxValue;
			array2 = array;
			foreach (ManufacturingProduction manufacturingProduction4 in array2)
			{
				if (manufacturingProduction4.productQueue != null && manufacturingProduction4.productQueue.Count < num4)
				{
					num4 = manufacturingProduction4.productQueue.Count;
					manufacturingProduction3 = manufacturingProduction4;
				}
			}
			if (!(manufacturingProduction3 == null))
			{
				int previousQueue = manufacturingProduction3.productQueue.Count;
                _cmdQueue.Invoke(manufacturingProduction3, new object[2] { result, text });
                if (manufacturingProduction3.productQueue.Count <= previousQueue) continue;
                num++;
				BetterSupermarketPlugin.Log.LogInfo($"Auto-production: queued '{ManufacturingName(result)}' variant (production stock {num3} <= {QueueThreshold}).");
			}
		}
		if ((num > 0) & notify)
		{
			BetterSupermarketPlugin.PendingNotification = "`Better Supermarket: queued " + num + " product(s)";
		}
		if (maxTray <= 0)
		{
			return;
		}
		Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
		Dictionary<int, int[]> dictionary3 = new Dictionary<int, int[]>();
		Transform transform3 = npc.storageOBJ.transform;
		for (int num5 = 0; num5 < transform3.childCount; num5++)
		{
			Data_Container component4 = transform3.GetChild(num5).GetComponent<Data_Container>();
			if (component4 == null || component4.productInfoArray == null)
			{
				continue;
			}
			int[] productInfoArray = component4.productInfoArray;
			for (int num6 = 0; num6 + 1 < productInfoArray.Length; num6 += 2)
			{
				int num7 = productInfoArray[num6];
				int num8 = productInfoArray[num6 + 1];
				if (num7 >= 0 && num8 > 0)
				{
					dictionary2[num7] = MfgI(dictionary2, num7) + num8;
					if (!dictionary3.ContainsKey(num7))
					{
						dictionary3[num7] = new int[3] { num5, num6, num8 };
					}
				}
			}
		}
		int num9 = 0;
		array2 = array;
		foreach (ManufacturingProduction manufacturingProduction5 in array2)
		{
			if (num9 >= maxTray)
			{
				break;
			}
			if (manufacturingProduction5.productQueue == null || manufacturingProduction5.productQueue.Count == 0 || manufacturingProduction5.itemsInTray == null || manufacturingProduction5.combinableQueue == null || manufacturingProduction5.combinableQueue.Count == 0)
			{
				continue;
			}
			int whichIndex = manufacturingProduction5.productQueue[0];
			string text2 = instance.RetrieveBaseRecipe(whichIndex);
			if (string.IsNullOrEmpty(text2))
			{
				continue;
			}
			string combination = RepairPolicies.NormalizeRecipe(manufacturingProduction5.combinableQueue[0]);
            if (combination != null && combination != "999")
                foreach (string ingredient in combination.Split('-')) text2 += "|" + ingredient;
            string[] array3 = text2.Split(new char[1] { '|' });
			foreach (string text3 in array3)
			{
				if (num9 >= maxTray)
				{
					break;
				}
				bool flag2 = false;
				List<int> list = new List<int>();
				string[] array4 = text3.Split(new char[1] { '-' });
				for (int num11 = 0; num11 < array4.Length; num11++)
				{
					if (int.TryParse(array4[num11], out var result2))
					{
						list.Add(result2);
						if (manufacturingProduction5.itemsInTray.Contains(result2))
						{
							flag2 = true;
							break;
						}
					}
				}
				if (flag2)
				{
					continue;
				}
				int num12 = -1;
				int num13 = 0;
				foreach (int item in list)
				{
					int num14 = MfgI(dictionary2, item);
					if (num14 > num13)
					{
						num13 = num14;
						num12 = item;
					}
				}
				if (num12 < 0)
				{
					continue;
				}
				int[] array5 = null;
                for (int shelfIndex = 0; shelfIndex < transform3.childCount && array5 == null; shelfIndex++)
                {
                    var source = transform3.GetChild(shelfIndex).GetComponent<Data_Container>();
                    if (source?.productInfoArray == null) continue;
                    for (int slot = 0; slot + 1 < source.productInfoArray.Length; slot += 2)
                        if (source.productInfoArray[slot] == num12 && source.productInfoArray[slot + 1] > 0)
                        { array5 = new int[] { shelfIndex, slot, source.productInfoArray[slot + 1] }; break; }
                }
                if (array5 == null) continue;
				Data_Container component5 = transform3.GetChild(array5[0]).GetComponent<Data_Container>();
				if (component5 == null)
				{
					continue;
				}
				int num15 = array5[1];
				int num16 = component5.productInfoArray[num15 + 1];
				if (num16 > 0)
				{
					int count = manufacturingProduction5.itemsInTray.Count;
					_cmdTray.Invoke(manufacturingProduction5, new object[2] { num12, null });
					if (manufacturingProduction5.itemsInTray.Count <= count || !manufacturingProduction5.itemsInTray.Contains(num12))
					{
						break;
					}
					if (num16 == 1)
					{
						component5.EmployeeUpdateArrayValuesStorage(num15, -1, -1);
					}
					else
					{
						component5.EmployeeUpdateArrayValuesStorage(num15, num12, num16 - 1);
					}
					dictionary2[num12] = Math.Max(0, MfgI(dictionary2, num12) - 1);
					array5[2] = num16 - 1;
					num9++;
					BetterSupermarketPlugin.Log.LogInfo("Auto-production: fed tray with '" + ManufacturingName(num12) + "' (from storage).");
				}
			}
		}
	}

	private static void AccumulateVariantStock(Transform containers, Dictionary<string, int> stock)
	{
		for (int i = 0; i < containers.childCount; i++)
		{
			ManufacturingContainer component = containers.GetChild(i).GetComponent<ManufacturingContainer>();
			if (component == null || component.productInfoArray == null || component.combinableInfoArray == null)
			{
				continue;
			}
			int[] productInfoArray = component.productInfoArray;
			string[] combinableInfoArray = component.combinableInfoArray;
			for (int j = 0; j + 1 < productInfoArray.Length; j += 2)
			{
				int num = productInfoArray[j];
				int num2 = productInfoArray[j + 1];
				if (num >= 0 && num2 >= 0)
				{
					string text = ((j / 2 < combinableInfoArray.Length) ? combinableInfoArray[j / 2] : "");
					Learn(num, text);
					string key = num + "|" + RepairPolicies.NormalizeRecipe(text);
					stock[key] = MfgS(stock, key) + num2;
				}
			}
		}
	}

	private static int MfgS(Dictionary<string, int> dict, string key)
	{
		if (!dict.TryGetValue(key, out var value))
		{
			return 0;
		}
		return value;
	}

	private static int MfgI(Dictionary<int, int> dict, int key)
	{
		if (!dict.TryGetValue(key, out var value))
		{
			return 0;
		}
		return value;
	}

	private static string ManufacturingName(int productId)
	{
		try
		{
			if (LocalizationManager.instance != null)
			{
				string localizationString = LocalizationManager.instance.GetLocalizationString("mfactureproduct" + productId);
				if (!string.IsNullOrEmpty(localizationString) && localizationString != "LocError")
				{
					return localizationString;
				}
				localizationString = LocalizationManager.instance.GetLocalizationString("product" + productId);
				if (!string.IsNullOrEmpty(localizationString) && localizationString != "LocError")
				{
					return localizationString;
				}
			}
		}
		catch
		{
		}
		return "#" + productId;
	}
}
