using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BetterSupermarketTogether;

// Ordinary products and manufactured products use separate, overlapping ID ranges.
// Only ordinary containers, boxes and ingredients belong in purchasing stock.
internal sealed class RestockStockSnapshot
{
    internal sealed class Stock
    {
        internal int Shelf, Warehouse, Ground, Held, Tray, Pending;
        internal bool Listed;
        internal int Reserve => Warehouse + Ground + Held + Tray + Pending;
        internal int OnHand => Shelf + Warehouse + Ground + Held + Tray;
    }

    private readonly Dictionary<int, Stock> _products = new();

    internal Stock Get(int productId)
    {
        if (!_products.TryGetValue(productId, out var stock))
            _products[productId] = stock = new Stock();
        return stock;
    }

    internal static RestockStockSnapshot Capture(NPC_Manager npc, ManagerBlackboard blackboard,
        ProductListing listing, bool includeTray)
    {
        if (npc == null || npc.shelvesOBJ == null || npc.storageOBJ == null || npc.boxesOBJ == null)
            return null;
        var snapshot = new RestockStockSnapshot();
        snapshot.ReadContainers(npc.shelvesOBJ.transform, false);
        snapshot.ReadContainers(npc.storageOBJ.transform, true);
        foreach (Transform child in npc.boxesOBJ.transform)
        {
            var box = child.GetComponent<BoxData>();
            if (box != null && box.productID >= 0 && box.numberOfProducts > 0)
                snapshot.Get(box.productID).Ground += box.numberOfProducts;
        }
        if (npc.employeeParentOBJ != null)
        {
            foreach (Transform child in npc.employeeParentOBJ.transform)
            {
                var employee = child.GetComponent<NPC_Info>();
                // NPC item 1 is an ordinary box; item 4 is a manufactured box.
                if (employee != null && employee.equippedItem == 1 &&
                    employee.boxProductID >= 0 && employee.boxNumberOfProducts > 0)
                    snapshot.Get(employee.boxProductID).Held += employee.boxNumberOfProducts;
            }
        }
        var manager = NetworkManager.singleton as CustomNetworkManager;
        if (manager != null)
        {
            foreach (var player in manager.GamePlayers)
            {
                if (player == null) continue;
                var network = player.GetComponent<PlayerNetwork>();
                var sync = player.GetComponent<PlayerSyncCharacter>();
                // Player item 1 is an ordinary box; item 16 is a manufactured box.
                if (network != null && network.equippedItem == 1 && sync != null &&
                    sync.syncedProductID >= 0 && sync.syncedNumberOfProducts > 0)
                    snapshot.Get(sync.syncedProductID).Held += sync.syncedNumberOfProducts;
            }
        }
        if (includeTray)
        {
            foreach (var table in Object.FindObjectsByType<ManufacturingProduction>(FindObjectsSortMode.None))
            {
                if (table == null || table.itemsInTray == null) continue;
                foreach (int ingredient in table.itemsInTray)
                    if (ingredient >= 0) snapshot.Get(ingredient).Tray++;
            }
        }
        if (blackboard != null && blackboard.idsToSpawn != null)
        {
            foreach (int product in blackboard.idsToSpawn)
            {
                if (product >= 0 && product < listing.productsData.Length && listing.productsData[product] != null)
                    snapshot.Get(product).Pending += Mathf.Max(1, listing.productsData[product].maxItemsPerBox);
            }
        }
        return snapshot;
    }

    private void ReadContainers(Transform root, bool warehouse)
    {
        foreach (Transform child in root)
        {
            var container = child.GetComponent<Data_Container>();
            var contents = container != null ? container.productInfoArray : null;
            if (contents == null) continue;
            for (int row = 0; row + 1 < contents.Length; row += 2)
            {
                if (contents[row] < 0) continue;
                var stock = Get(contents[row]);
                stock.Listed = true; // An empty labeled row still identifies a product the store uses.
                if (contents[row + 1] <= 0) continue;
                if (warehouse) stock.Warehouse += contents[row + 1];
                else stock.Shelf += contents[row + 1];
            }
        }
    }
}
