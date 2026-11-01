# Better Supermarket Together

English · [繁體中文](README.md)

A **BepInEx 5** quality-of-life mod for **Supermarket Together**, combining restocking, production, warehouse organization, pricing, promotions, cleaning, and employee management. Previously named **AutoRestock2230**.

This mod is **host-only**, including single-player hosting. Other players do not need it. If installed on a joining client, automation, mod UI, hotkeys, and translation overrides remain inactive. Host changes to game state still use the game's native multiplayer synchronization.

## Features

| Feature | What it does |
| --- | --- |
| Daytime and nighttime purchasing | Checks stock during configurable in-game hours and orders products that fall below their thresholds. Default daytime hours are 08:00–22:00; nighttime hours are 22:30–23:54. Daytime ordering can optionally require the store to be open. |
| Stock thresholds by category | Separate minimum-stock settings for general products, production ingredients, hygiene products, and cleaning supplies. Page 2 of the settings panel provides category controls. |
| Purchasing conditions | Configurable cash reserve, boxes per order, daily/nightly order limits, and cooldowns. By default, only products seen during the current day are considered, with repeated stock checks to reduce false shortages during handling. |
| Idle-employee gate | Can wait for an idle storage or restocking employee before placing orders. Uses a separate minimum reorder interval when enabled. |
| Smarter restocking | Assigns one restocking employee per whole shelf for both ordinary and manufactured products, covering pickup, travel, placement pauses, and retargeting leftover goods. Completion/cancellation releases the shelf; employees wait when no unclaimed task is available. Preserves low-stock shelf priority and selection of smaller remaining boxes. |
| Grouped warehouse placement | Employees prefer shelves already holding the same product, reducing scattered storage. |
| Nightly warehouse consolidation | Moves matching products toward their main storage shelf after closing. Defaults to 23:00 with a maximum of 30 box moves per night. |
| Automatic production queues | By default, replenishes only recipes assigned to sales shelves, including sold-out shelves retaining their labels. Stock is counted separately for each custom variant. Historical recipes and leftover boxes alone do not create production targets. |
| Automatic ingredient feeding | Transfers required base and optional ingredients from ordinary warehouse storage onto production trays. Production itself uses the game's employees and workbenches. |
| Automatic pricing | Requires **MART OS 2.0**. Until unlocked, the toggle is locked and enabled configuration values cannot change prices. Updates unlocked products at a scheduled time using current purchase costs, and reevaluates after product unlocks. The default selling price is 200% of cost, with configurable percentage and rounding. |
| Automatic promotions | Selects an eligible stocked product each day. Can estimate slow-moving stock from inventory and recent sales, falling back to inventory ranking when sales data is unavailable. Default discount: 15%. |
| Automatic order-department activation | Activates the unlocked order department when the supermarket opens. |
| Extra employee capacity | Buy additional hiring slots with in-game money. Default costs increase as $5,000, $10,000, $15,000, and so on. Slots are applied after employees finish loading. |
| Employee naming | Enter a name, randomize one employee, or randomize everyone. Both Chinese and English use male/female pools matched to the NPC; unidentified models use neutral names. Names have a 25-character limit; choosing a name stops the load-time default list from replacing it. |
| Cleaner robot patrol | Patrols while idle and looks for nearby unclaimed trash. Patrol radius is configurable. |
| Traditional Chinese text corrections | Includes an override table for selected in-game translations, applied when the game language is Chinese. |
| In-game controls and status | Manager-board plugin button, F10 settings, F8 status overlay, F9 report, and F11 diagnostic logging. |

## Separate ordinary and manufacturing workflows

| Flow | Behavior |
| --- | --- |
| Ordinary goods | Purchase → ordinary delivery boxes → storage employees → ordinary warehouse → restock employees → ordinary sales shelves. Matches ordinary product IDs. |
| Production ingredients | Uses ordinary inventory, but retains every required recipe group and retries reachable alternatives when ingredients are missing. Employee collection and automatic tray feeding claim workbenches separately. |
| Manufactured goods | Matches manufactured product ID plus the full recipe. Workbench → finished box → manufacturing employees → manufacturing storage/sales shelves. Carried finished goods and active production batches suppress duplicate production. |

With `Restock.SmartRestock` enabled, employees read live shelf data and require complete NavMesh paths for pickup, shelf replenishment, and storage. Tasks are revalidated after shelves move, disappear, or change recipes. Invalid paths and prolonged lack of movement trigger recovery and temporary avoidance. Cancelling an employee's uncommitted ingredient-collection job returns collected ingredients as ordinary boxes. Completely obstructed areas still require moving the obstruction in the store.

Automatic purchasing waits for an existing shopping cart to be cleared. Rejected orders or concurrent player edits remove only entries added by that automatic attempt. F11 includes each employee's workflow, path status, remaining distance, wait target, and per-product purchasing skip reasons. Empty labeled rows count as products used by the store.

See the [restocking and navigation audit](docs/RESTOCK-NAVIGATION-AUDIT-2026-11-01.md) for the reviewed flows and validation limits.

## Installation

Requirements: the Windows version of Supermarket Together and **BepInEx 5 x64** installed in the game directory. The local build uses BepInEx 5.4.23.2 and game assemblies from Unity 2023.2.22f1. Other versions have not been fully validated.

1. Close the game. If needed, install BepInEx 5 according to its instructions and launch the game once.
2. Extract the mod package and merge its `BepInEx` directory into the game installation.
3. Keep these two files together:

   ```text
   BepInEx/plugins/BetterSupermarketTogether/
   ├── BetterSupermarketTogether.dll
   └── BetterSupermarketTogether_i18n_zhTW.tsv
   ```

4. Restart the game, load your store, and open the plugin settings from the manager board.

When upgrading from AutoRestock2230, remove or disable the old `AutoRestock2230.dll` first to avoid loading both copies. The new version retains the existing plugin identifier and configuration file, preserving settings, learned recipes, and purchased employee slots. Keep your original configuration file.

## Controls

| Control | Action |
| --- | --- |
| Manager board → 插件, or F10 | Open/close settings. Page 1 contains the main options; page 2 contains category stock thresholds. |
| F10 → 員工改名 → 隨機風格 | Switch between `中文 / Chinese` and `英文 / English`, then use an individual random-name button or randomize everyone. Switching the style alone does not rename anyone. |
| F8 | Toggle the top-left status overlay. |
| F9 | Show the full status report. |
| F11 | Write employee, capacity, recipe, per-product stock, and purchase-skip diagnostics to `BepInEx/LogOutput.log`. |

Open the manager board before using the F10 panel so the cursor is available. Keyboard shortcuts are configurable.

## When a product is not automatically purchased

Purchasing compares the minimum against reserve stock: ordinary warehouse goods, ordinary boxes on the ground or held by employees/players, ingredients on workbench trays (when `IncludeProductionStock` is enabled), and purchased goods awaiting delivery. Sales-shelf stock is excluded from this reserve. Manufactured goods use a separate product-ID range and are never counted as ordinary goods. Products already awaiting delivery are evaluated again after delivery.

Press F11 and search the log for `RESTOCK #productID` or the product name. Each entry shows shelf, warehouse, ground, held, tray, pending-delivery and reserve quantities, the minimum, and the first current blocking condition: time window, sufficient reserve, shelf threshold, unseen product, shortage confirmation, daily/nightly order limit, idle employee, or cooldown. `lastOrder` records the most recent purchase success or failure. F11 does not purchase anything or advance confirmation counters.

Order limits and cooldowns still apply. The idle gate uses the employee's current action and wait target, avoiding stale data left by completed tasks. Restart the game after replacing the DLL; F9 shows the loaded version.

## Configuration

For compatibility with the previous mod name, the configuration file remains:

```text
BepInEx/config/local.supermarkettogether.autorestock2230.cfg
```

Common options can be changed in-game. Close the game before editing the file manually.

To use English random names and customize their pool:

```ini
[Naming]
RandomNameStyle = English
MaleEnglishNames = James Parker,Daniel Brooks,Oliver Hayes
FemaleEnglishNames = Emma Wilson,Olivia Bennett,Sophia Turner
NeutralEnglishNames = Alex Morgan,Taylor Reed,Jordan Parker
```

Use `RandomNameStyle = Chinese` with `MaleChineseNames`, `FemaleChineseNames`, and `NeutralChineseNames` for Chinese names. A new installation defaults to Chinese. Legacy mixed lists (`RandomNames` and `EnglishRandomNames`) are retained as data but no longer select random names; move custom entries into the appropriate male/female/neutral lists. Updating does not rename existing employees. Use individual or bulk randomization to replace previously assigned names.

| Setting | Fresh-install default | Purpose |
| --- | --- | --- |
| `General.OnlyHost` (legacy) | Always host-only | Retained for compatibility; `false` cannot bypass host authority. The in-game toggle has been removed. |
| `Daytime.OnlyWhenOpen` | `false` | Allow daytime restocking before opening the store. |
| `Safety.CheckIntervalSeconds` | `10` | Stock-scan interval in real seconds. |
| `Restock.GateOnIdleEmployee` | `true` | Wait for an idle storage/restocking employee. |
| `Restock.MinTotalStockDefault` | `0` | Baseline minimum stock for ordinary products. |
| `Restock.MinTotalStockProduction` | `100` | Ingredient restock threshold. Adjust the last item on F10 page 2 (生產原料最低庫存) in steps of 10. The higher of this and the product category threshold applies. |
| `Restock.GroupMinStock` | `4:100,5:100` | Category thresholds; 4 is hygiene and 5 is cleaning supplies. |
| `Production.AutoQueueThreshold` | `0` | Queue production when production-area stock is at or below this value. |
| `Production.OnlyShelfRecipes` | `true` | Replenish recipes assigned to sales shelves; `false` restores all historical recipes as targets. |
| `Pricing.Percentage` | `200` | Selling price as a percentage of current cost. |
| `Promo.Discount` | `15` | Promotion discount percentage. |
| `Staff.ExtraSlotBaseCost` | `5000` | Base cost for additional hiring slots, increasing with each purchase. |
| `Naming.RandomNameStyle` | `Chinese` | Random-name style: `Chinese` or `English`. |

These are defaults for new installations; existing values are preserved during an upgrade. `ExtraSlotsPurchased`, `LearnedCombinables`, and `KnownVariants` are maintained by the mod. Extra slots and learned recipes are stored in the local configuration and are currently shared across stores using that configuration.

## Build and package

Requires a .NET SDK (the local build uses .NET 10), plus your installed game and BepInEx assemblies. Game and BepInEx DLLs are not included in this source repository.

```powershell
./build.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Supermarket Together'
./package.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Supermarket Together'
```

`build.ps1` builds Release. `package.ps1` builds and produces `artifacts/BetterSupermarketTogether-1.2.8.zip`. You can also set `SUPERMARKET_TOGETHER_DIR` or call `dotnet build -c Release -p:GameDir="path to game"` directly.

## Scope and current status

- Purchasing spends in-game money. Adjust stock thresholds and the cash reserve as needed. Production requires recipes, ingredients, workbenches, and production employees.
- Only the host can use this mod. Native server authority is required and cannot be disabled through configuration. Leaving a room clears mod UI and pending session state.
- Version 1.2.8 passed compilation. No new full-match or GUI acceptance run was performed. Updating does not remove existing products or cancel existing/manual production queues.
- This README provides English documentation. Most mod settings labels are currently in Traditional Chinese.
- When reporting an issue, include the game version, mod version, and relevant log excerpts.

This is an unofficial mod and is not affiliated with the game's developer. Code is available under the [MIT License](LICENSE). See [CHANGELOG.md](CHANGELOG.md) for version history.

