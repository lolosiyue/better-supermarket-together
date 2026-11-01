using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

internal static class ProductionAutomation
{
	internal static bool Warned;
    private static ModText.Message _status = "等待生產部載入";
    internal static string Status { get => _status.ToString(); private set => _status = value; }
    private static bool _dirty;

    internal static void Reset()
    {
        KnownVariants.Clear();
        _variantsLoaded = false;
        _ingredientPids = null;
        _queueFailure = null;
        Status = "等待生產部載入";
    }

    private static void SaveDiscovered()
    {
        if (!_dirty) return;
        PersistVariants?.Invoke(ExtractForPersist());
        _dirty = false;
    }


	internal static int QueueThreshold = 0;

    internal static bool OnlyShelfRecipes = true;

	private static MethodInfo _cmdQueue;
    private static MethodInfo _cmdDeskQueue;
    private static string _queueFailure;

    private sealed class Target
    {
        internal string Key, Recipe;
        internal int Product;
        internal ProductionStockSnapshot.Stock Stock;
    }

	private static bool _variantsLoaded;

	internal static readonly Dictionary<string, string> KnownVariants = new Dictionary<string, string>();

	internal static Func<Dictionary<string, string>> LoadVariants;

	internal static Action<Dictionary<string, string>> PersistVariants;

	private static HashSet<int> _ingredientPids;

    internal static bool IsProductionIngredient(int productId)
    {
        if (_ingredientPids == null)
        {
            var instance = ManufacturingBase.Instance;
            // Retry after loading rather than permanently caching an empty ingredient set.
            if (instance == null || !(AccessTools.Field(typeof(ManufacturingBase), "baseRecipes")?.GetValue(instance) is string[] recipes) || recipes.Length == 0)
                return false;
            _ingredientPids = new HashSet<int>();
            foreach (string recipe in recipes)
            {
                foreach (string token in (recipe ?? "").Split('|', '-'))
                    if (int.TryParse(token, out int id) && id >= 0 && id != 999) _ingredientPids.Add(id);
            }
        }
        if (_ingredientPids.Contains(productId)) return true;
        LoadPersistedVariants();
        foreach (string recipe in KnownVariants.Values)
        {
            foreach (string token in recipe.Split('-'))
                if (int.TryParse(token, out int id) && id != 999 && id == productId) return true;
        }
        return false;
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
        if (!BetterSupermarketPlugin.EnablePlugin) return;
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
        var stock = ProductionStockSnapshot.Capture(npc);
        foreach (var entry in stock.Products)
        {
            int split = entry.Key.IndexOf('|');
            if (int.TryParse(entry.Key.Substring(0, split), out int product)) Learn(product, entry.Key.Substring(split + 1));
        }
        SaveDiscovered();
        var targets = Targets(stock);
        _queueFailure = null;
        int queued = 0, shortage = 0, pending = 0;
        foreach (var target in targets)
        {
            if (target.Stock.Reserve <= QueueThreshold) shortage++;
            string reason = SkipReason(instance, npc, array, target);
            if (reason != null)
            {
                if (reason.StartsWith("pending:")) pending++;
                continue;
            }
            if (queued >= maxQueue) continue;
            if (!Queue(npc, array, target.Product, target.Recipe, out string destination)) continue;
            queued++;
            BetterSupermarketPlugin.Log.LogInfo($"Auto-production: queued '{ManufacturingName(target.Product)}' recipe={target.Recipe}, target={(OnlyShelfRecipes ? "sales-shelf" : "history")} reserve={target.Stock.Reserve} (warehouse={target.Stock.Warehouse}, ground={target.Stock.Ground}, held={target.Stock.Held}; shelf={target.Stock.Shelf} excluded), threshold={QueueThreshold}, destination={destination}.");
        }
        _status = new ModText.Message("目標 {0} 配方｜備貨不足 {1}｜待製作 {2}｜本輪新增 {3}", targets.Count, shortage, pending + queued, queued);
        if (queued == 0 && _queueFailure != null) _status = new ModText.Message("{0} | {1}", _status, new ModText.Message(_queueFailure));
        if (queued > 0 && notify)
            BetterSupermarketPlugin.PendingNotification = "`Better Supermarket: queued " + queued + " recipe(s)";
		if (maxTray <= 0)
		{
			return;
		}
		Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
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
            if (!manufacturingProduction5.isServer || manufacturingProduction5.assignedToEmployee ||
                ProductionLifecycle.ReadyOrProducing(manufacturingProduction5)) continue;
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
				if (num9 >= maxTray || ProductionLifecycle.ReadyOrProducing(manufacturingProduction5))
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
                    if (!ProductionLifecycle.TryFeed(manufacturingProduction5, num12)) break;
					if (num16 == 1)
					{
						component5.EmployeeUpdateArrayValuesStorage(num15, component5.transform.Find("CanvasSigns") != null ? num12 : -1, -1);
					}
					else
					{
						component5.EmployeeUpdateArrayValuesStorage(num15, num12, num16 - 1);
					}
					dictionary2[num12] = Math.Max(0, MfgI(dictionary2, num12) - 1);
					array5[2] = num16 - 1;
					num9++;
					BetterSupermarketPlugin.Log.LogInfo("Auto-production: fed tray with '" + OrdinaryName(num12) + "' (from storage).");
				}
			}
		}
	}

    private static List<Target> Targets(ProductionStockSnapshot snapshot)
    {
        var keys = new HashSet<string>(KnownVariants.Keys, StringComparer.Ordinal);
        foreach (var entry in snapshot.Products) keys.Add(entry.Key);
        var targets = new List<Target>();
        foreach (string key in keys)
        {
            int split = key.IndexOf('|');
            if (split <= 0 || !int.TryParse(key.Substring(0, split), out int product) || product < 0) continue;
            string recipe = RepairPolicies.NormalizeRecipe(key.Substring(split + 1));
            if (recipe == null) continue;
            string normalized = ProductionStockSnapshot.Key(product, recipe);
            var stock = snapshot.Get(normalized);
            if (OnlyShelfRecipes && !stock.OnSalesShelf) continue;
            targets.Add(new Target { Key = normalized, Product = product, Recipe = recipe, Stock = stock });
        }
        targets.Sort((a, b) =>
        {
            int reserve = a.Stock.Reserve.CompareTo(b.Stock.Reserve);
            if (reserve != 0) return reserve;
            int shelf = a.Stock.Shelf.CompareTo(b.Stock.Shelf);
            if (shelf != 0) return shelf;
            return StringComparer.Ordinal.Compare(a.Key, b.Key);
        });
        return targets;
    }

    private static bool Contains(List<int> products, List<string> recipes, int product, string recipe)
    {
        if (products == null || recipes == null) return false;
        for (int i = 0; i < products.Count && i < recipes.Count; i++)
            if (products[i] == product && RepairPolicies.NormalizeRecipe(recipes[i]) == recipe) return true;
        return false;
    }

    private static string SkipReason(ManufacturingBase manufacturing, NPC_Manager manager, ManufacturingProduction[] tables, Target target)
    {
        if (!BetterSupermarketPlugin.ProductionQueueEnabled) return "auto queue disabled / 自動排程已關閉";
        int product = target.Product;
        if (manufacturing.unlockedBaseProducts == null || product >= manufacturing.unlockedBaseProducts.Length ||
            !manufacturing.unlockedBaseProducts[product] || product >= manufacturing.productsData.Length || manufacturing.productsData[product] == null)
            return "recipe locked or unavailable / 配方未解鎖或資料未載入";
        if (manufacturing.productsData[product].needsDistilling && !manufacturing.hasDistilleryAddon)
            return "distillery locked / 尚未解鎖蒸餾";
        foreach (var table in tables)
        {
            if (table == null || !table.isServer) continue;
            if (ProductionLifecycle.HasInFlight(table, product, target.Recipe)) return "pending: producing / 正在製作";
            if (Contains(table.productQueue, table.combinableQueue, product, target.Recipe)) return "pending: workbench / 工作台已排程";
        }
        if (manager.manufacturingDesksList != null)
            foreach (var obj in manager.manufacturingDesksList)
            {
                var desk = obj != null ? obj.GetComponent<ManufacturingDesk>() : null;
                if (desk != null && desk.isServer && Contains(desk.toSendProductsQueue, desk.toSendCombinableQueue, product, target.Recipe))
                    return "pending: management desk / 中央看板已排程";
            }
        return target.Stock.Reserve > QueueThreshold ? "reserve sufficient / 備用成品足夠" : null;
    }

    private static bool Queue(NPC_Manager manager, ManufacturingProduction[] tables, int product, string recipe, out string destination)
    {
        destination = null;
        ManufacturingDesk selected = null;
        if (manager.manufacturingDesksList != null)
            foreach (var obj in manager.manufacturingDesksList)
            {
                var desk = obj != null ? obj.GetComponent<ManufacturingDesk>() : null;
                if (desk == null || !desk.isServer || desk.toSendProductsQueue == null || desk.toSendCombinableQueue == null ||
                    desk.toSendProductsQueue.Count != desk.toSendCombinableQueue.Count) continue;
                if (selected == null || desk.toSendProductsQueue.Count < selected.toSendProductsQueue.Count) selected = desk;
            }
        if (selected != null)
        {
            if (_cmdDeskQueue == null) _cmdDeskQueue = AccessTools.Method(typeof(ManufacturingDesk), "UserCode_CmdAddToQueue__Int32__String");
            if (_cmdDeskQueue == null) return QueueFailure("中央看板排程方法不可用");
            int before = selected.toSendProductsQueue.Count;
            _cmdDeskQueue.Invoke(selected, new object[] { product, recipe });
            if (selected.toSendProductsQueue.Count <= before) return QueueFailure("中央看板未接受排程");
            destination = "management-desk:" + selected.name;
            return true;
        }
        // Stores without a management desk retain direct workbench scheduling.
        ManufacturingProduction bench = null;
        foreach (var table in tables)
        {
            if (table == null || !table.isServer || table.productQueue == null || table.combinableQueue == null ||
                table.productQueue.Count != table.combinableQueue.Count || table.productQueue.Count > 2) continue;
            if (bench == null || table.productQueue.Count < bench.productQueue.Count) bench = table;
        }
        if (bench == null) return QueueFailure("等待可用工作台");
        if (!TryQueueWorkbench(bench, product, recipe)) return QueueFailure("工作台未接受排程");
        destination = "workbench:" + bench.name;
        return true;
    }

    private static bool QueueFailure(string reason)
    {
        _queueFailure = reason;
        if (!Warned) { Warned = true; BetterSupermarketPlugin.Log.LogWarning("Auto-production: " + reason); }
        return false;
    }

    internal static bool TryQueueWorkbench(ManufacturingProduction table, int product, string recipe)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || table == null || !table.isServer || product < 0 ||
            ManufacturingBase.Instance == null || product >= ManufacturingBase.Instance.productsData.Length) return false;
        if (_cmdQueue == null) _cmdQueue = AccessTools.Method(typeof(ManufacturingProduction), "UserCode_CmdAddToProductionQueue__Int32__String");
        if (_cmdQueue == null) return false;
        string prepared = ProductionLifecycle.PreparedRecipe(table);
        _cmdQueue.Invoke(table, new object[] { product, recipe });
        ProductionLifecycle.PreservePreparedRecipe(table, prepared);
        return true;
    }

    internal static void DumpDiagnostics(NPC_Manager manager)
    {
        if (!BetterSupermarketPlugin.IsHost || manager == null || ManufacturingBase.Instance == null) return;
        LoadPersistedVariants();
        var tables = UnityEngine.Object.FindObjectsByType<ManufacturingProduction>(FindObjectsSortMode.None);
        var snapshot = ProductionStockSnapshot.Capture(manager);
        var targets = Targets(snapshot);
        BetterSupermarketPlugin.Log.LogInfo($"PRODUCTION targets={targets.Count} known={KnownVariants.Count} threshold={QueueThreshold} enabled={BetterSupermarketPlugin.ProductionQueueEnabled} onlyShelfRecipes={OnlyShelfRecipes} tables={tables.Length}; reserve=warehouse+ground+held, sales shelves excluded; lastQueueFailure={_queueFailure ?? "none"}");
        foreach (var target in targets)
        {
            var stock = target.Stock;
            string reason = tables.Length == 0 ? "no workbench / 沒有工作台" : SkipReason(ManufacturingBase.Instance, manager, tables, target) ?? "ready for next scan / 下次掃描可排程";
            BetterSupermarketPlugin.Log.LogInfo($"  PRODUCTION #{target.Product} {ManufacturingName(target.Product)} recipe={target.Recipe} shelf={stock.Shelf} warehouse={stock.Warehouse} ground={stock.Ground} held={stock.Held} reserve={stock.Reserve} threshold={QueueThreshold} reason={reason}");
        }
        foreach (var table in tables)
        {
            BetterSupermarketPlugin.Log.LogInfo($"  PRODUCTION TABLE {table.name} queued={table.productQueue?.Count ?? 0} assigned={table.assignedToEmployee} readyOrProducing={ProductionLifecycle.ReadyOrProducing(table)} autoTrayClaim={ProductionLifecycle.AutoClaimed(table)}");
            if (table.productQueue == null || table.combinableQueue == null) continue;
            for (int i = 0; i < table.productQueue.Count && i < table.combinableQueue.Count; i++)
                BetterSupermarketPlugin.Log.LogInfo($"    job={i} product={table.productQueue[i]} recipe={table.combinableQueue[i]}");
        }
    }

    private static string OrdinaryName(int product)
    {
        return LocalizationManager.instance != null ? LocalizationManager.instance.GetLocalizationString("product" + product) : "#" + product;
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

// The native management desk calls a network Command from its server dispatch loop.
// Complete the handoff synchronously so the job never disappears between the two queues.
[HarmonyPatch(typeof(ManufacturingProduction), "AddItemFromManufacturingDesk")]
internal static class SynchronousProductionDeskHandoffPatch
{
    private static bool Prefix(ManufacturingProduction __instance, int __0, string __1)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || !__instance.isServer) return true;
        return !ProductionAutomation.TryQueueWorkbench(__instance, __0, __1);
    }
}
