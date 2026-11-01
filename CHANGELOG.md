# Changelog

## 1.2.6 — 2026-11-01

- Audit ordinary purchasing, ordinary box storage/restocking, production ingredient collection, and manufactured box storage/restocking as distinct flows.
- Select tasks from live shelves instead of partial/stale native lists; exclude full rows and require complete pickup-to-shelf routes.
- Preserve physical container references across index changes, validate labels/recipes before placement, and recover from removed targets or stalled paths while retaining carried goods.
- Release stale ground-box claims, check routes for both box types, and recognize compatible merge space when storage is full.
- Select reachable storage and ingredient sources; walk to a newly selected return rack before depositing.
- Keep every required ingredient group during employee collection, retry reachable alternatives, use existing tray ingredients, and return collected ingredients when a job is cancelled.
- Apply production commands on the host synchronously and acknowledge accepted tray additions even when the final ingredient is consumed immediately.
- Avoid employee/automatic feeding races and stale workbench ownership; include carried finished boxes and active production in duplicate-queue prevention.
- Preserve labeled warehouse rows when their last ingredient/box is removed and avoid active ingredient-pickup slots during consolidation.
- Recognize empty labeled ordinary rows for purchasing; preserve manual shopping carts and clean up rejected automatic entries.
- Extend F11 employee navigation diagnostics and both READMEs.

## 1.2.5 — 2026-11-01

- Separate ordinary purchasing inventory from manufactured-product IDs, including carried boxes; count actual tray ingredients instead of finished goods.
- Use the same corrected on-hand counts for promotion ranking.
- Detect idle restock/storage employees from current actions and wait targets, ignoring stale completed-task fields.
- Count pending cargo and wait for delivery before purchasing the same product again.
- Charge order limits and cooldowns only after the game accepts a purchase.
- Retry ingredient classification after production data loads, and recognize known optional ingredients.
- Add bilingual per-product F11 stock and skip-reason diagnostics, including the last purchase outcome.

## 1.2.4 — 2026-11-01

- Reserve whole destination shelves for ordinary and manufactured-product restocking, rather than individual rows.
- Return an empty task when all eligible work is occupied instead of falling through to vanilla assignment.
- Check shelf ownership again when an employee redirects leftover goods to another shelf.
- Retain ownership during placement pauses and release it after completion, cancellation, or failed pickup.
- Keep warehouse source slots reserved only while the assigned employee is collecting that box.

## 1.2.3 — 2026-11-01

- Add the production-ingredient minimum-stock control to the end of settings page 2, bound directly to `Restock.MinTotalStockProduction` in steps of 10.
- Explain that ingredient and category thresholds use the higher value.

## 1.2.2 — 2026-11-01

- Match both Chinese and English random employee names to the NPC model's male/female metadata.
- Add separate configurable male, female, and neutral pools for both naming styles.
- Use neutral names when a model cannot be identified; never fall back to the old mixed pool.
- Apply the same selection to individual and bulk random naming, avoiding duplicates while unused names remain.
- Preserve existing chosen names and legacy configuration data.

## 1.2.1 — 2026-11-01

- Replenish only recipes assigned to sales shelves by default, including empty labeled shelves.
- Keep historical recipes and leftover boxes as stock information without automatically replenishing obsolete variants.
- Include the exact recipe and target source in automatic-production logs.
- Preserve existing inventory and queued jobs; `Production.OnlyShelfRecipes = false` restores historical-target behavior.

## 1.2.0 — 2026-11-01

- Rename the mod and its assembly to Better Supermarket Together.
- Add Chinese/English random employee-name styles and a configurable English name pool.
- Add a saved style selector to the employee-naming page; individual and bulk randomization use the selected style.
- Preserve the existing plugin identifier and configuration for upgrades from AutoRestock2230.
- Add Traditional Chinese and English documentation, portable build configuration, and a package script.

## 1.1.1 — 2026-11-01

- Correct random-name buttons after employee UI rows are rebuilt.
- Add a dedicated naming page with manual and random naming.
- Preserve chosen names in employee save records.
- Apply purchased slots after employee loading and maintain employee-array capacity.
- Restore legacy production recipes, preserve variants, honor stock thresholds, and feed optional ingredients.

## 1.1.0

- Earlier local AutoRestock2230 version with purchasing, production, pricing, promotions, warehouse, employee, cleaner, and localization features.
