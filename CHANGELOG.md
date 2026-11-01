# Changelog

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
