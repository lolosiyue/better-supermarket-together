using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BetterSupermarketTogether;

// This inventory is exclusively manufactured products. Ordinary ingredient IDs never enter it.
internal sealed class ProductionStockSnapshot
{
    internal sealed class Stock
    {
        internal int Shelf, Warehouse, Ground, Held;
        internal bool OnSalesShelf;
        internal int Reserve => Warehouse + Ground + Held;
    }

    private readonly Dictionary<string, Stock> _stock = new();
    internal IEnumerable<KeyValuePair<string, Stock>> Products => _stock;
    internal Stock Get(string key) => _stock.TryGetValue(key, out var stock) ? stock : new Stock();

    internal static string Key(int product, string recipe)
    {
        recipe = RepairPolicies.NormalizeRecipe(recipe);
        return product < 0 || recipe == null ? null : product + "|" + recipe;
    }

    private Stock Add(int product, string recipe)
    {
        string key = Key(product, recipe);
        if (key == null) return null;
        if (!_stock.TryGetValue(key, out var stock)) _stock[key] = stock = new Stock();
        return stock;
    }

    internal static ProductionStockSnapshot Capture(NPC_Manager manager)
    {
        var result = new ProductionStockSnapshot();
        result.ReadContainers(manager.manufacturingShelvesOBJ, true);
        result.ReadContainers(manager.manufacturingStorageShelvesOBJ, false);
        if (manager.manufacturingBoxesOBJ != null)
            foreach (Transform child in manager.manufacturingBoxesOBJ.transform)
            {
                var box = child.GetComponent<ManufacturingBoxData>();
                if (box == null || box.numberOfProducts <= 0) continue;
                var stock = result.Add(box.manufacturedProductIndex, box.combinablesData);
                if (stock != null) stock.Ground += box.numberOfProducts;
            }
        if (manager.employeeParentOBJ != null)
            foreach (Transform child in manager.employeeParentOBJ.transform)
            {
                var employee = child.GetComponent<NPC_Info>();
                if (employee == null || employee.equippedItem != 4 || employee.boxNumberOfProducts <= 0) continue;
                var stock = result.Add(employee.boxProductID, employee.mBoxCombinableData);
                if (stock != null) stock.Held += employee.boxNumberOfProducts;
            }
        var network = NetworkManager.singleton as CustomNetworkManager;
        if (network != null)
            foreach (var player in network.GamePlayers)
            {
                var held = player != null ? player.GetComponent<PlayerNetwork>() : null;
                if (held == null || held.equippedItem != 16 || held.extraParameter2 <= 0) continue;
                var stock = result.Add(held.extraParameter1, held.extraParameter3);
                if (stock != null) stock.Held += held.extraParameter2;
            }
        return result;
    }

    private void ReadContainers(GameObject root, bool sales)
    {
        if (root == null) return;
        foreach (Transform child in root.transform)
        {
            var container = child.GetComponent<ManufacturingContainer>();
            if (container == null || container.productInfoArray == null || container.combinableInfoArray == null) continue;
            var contents = container.productInfoArray;
            for (int pair = 0; pair + 1 < contents.Length && pair / 2 < container.combinableInfoArray.Length; pair += 2)
            {
                if (contents[pair] < 0 || sales && contents[pair + 1] < 0) continue;
                var stock = Add(contents[pair], container.combinableInfoArray[pair / 2]);
                if (stock == null) continue;
                int count = Math.Max(0, contents[pair + 1]);
                if (sales) { stock.Shelf += count; stock.OnSalesShelf = true; }
                else stock.Warehouse += count;
            }
        }
    }
}
