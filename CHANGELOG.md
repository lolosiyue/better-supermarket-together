# Changelog

## 1.4.1 — 2026-11-02

- Centralize companion plan labels, progress, errors, autonomy and cache captions in the shared 252-entry `ModText.Catalog.cs`. Remove whole-summary substring replacement and inline bilingual work messages.
- Separate explicit plan phases/event revisions from localized messages. Changing language between completion and handoff cannot change execution or duplicate an event.
- Persist optional keys/arguments for new execution notices and render them in the selected UI language after reload. Keep player/model dialogue verbatim, localize the autonomous marker independently, and retain raw fallback for legacy or invalid notices.
- Use canonical English tool/history text independently of UI language; keep full inspection JSON on the API side and show a localized inspection summary in chat.
- Pass 207 checks, including catalog coverage/formatting, language switching, memory compatibility, protected text, bounded notice parsing and the existing functional regressions. Build against the installed game assemblies; live UI acceptance remains pending. No real API was called for this release.

## 1.4.0 — 2026-11-02

- Add session-scoped autonomous work, changed-state gating, a default 120-second decision interval, a rolling 20-decision/hour cap, and exponential API-error backoff. Player messages preempt in-flight autonomous decisions. Autonomous tools cannot follow the host, alter memories, change modes or create indefinite plans.
- Add ordered plans of up to six steps, finite native work cycles/timers, ordinary retail product/shelf targets and NavMesh following of the host. Complete or fail a step before handing over safely; failure clears subsequent steps. Finite final steps rest instead of continuing unrelated work.
- Bind shelf IDs to object identity for each shop session. Retain native warehouse pickup, placement, box return and animation. Constrain both initial selection and retargeting to active goals, including when general smart restocking is disabled. Missing/relabelled targets fail without switching to an unrelated shelf.
- Add paginated shop inspection and up to three tool rounds plus a final reply, with at most eight accepted calls, one control action and one format correction. Keep the cache-friendly schema/choice stable across rounds.
- Add F7 autonomy, stop-plan and crosshair shelf selection, plus scrollable plan/memory readouts. Autonomy and plans are not resumed from disk after loading a shop.
- Validate with 176 automated checks, a build against the installed game assemblies, and four official-API synthetic scenarios (eight requests in the successful run). Native in-game movement, inventory and UI acceptance remain pending; no game window was controlled.

## 1.3.2 — 2026-11-02

- Keep a stable companion system prefix and append fresh memory/shop/language context at the end of previous exact conversation turns. Preserve raw tool calls, results and final-only instructions in a bounded session ledger.
- Rotate complete history batches at 20 retained turns or 32,000 serialized history characters; retain up to 16,000 characters of legacy chat when starting a session. No synthetic padding or background cache-warming requests are added to gameplay.
- Keep identical tools and `tool_choice=auto` during result summaries. Add a turn-scoped final-only instruction and reject additional tool calls client-side, then allow newly authorized tools on the next user turn; the measured `tool_choice=none` variant lost prefix reuse on this API.
- Show last/room cache rates, hit/miss tokens, input/output tokens and response latency in F7. Use provider usage data, compatible cached_tokens fallback, token-weighted totals and explicit unknown status when cache usage is absent. Log numeric usage only.
- Keep API configuration, long-term memory and employee binding. Reset session context on shop/binding/provider changes and safely close cancelled tool exchanges.
- Pass 100 automated checks. Four synthetic official-API requests in the final probe reported 60.8% aggregate cached input, including a cold first request; subsequent requests reported 72.6%, 77.1% and 79.7%. The next turn successfully requested a newly authorized tool. This is a small synthetic sample, not a production-game hit-rate or speed guarantee.

## 1.3.1 — 2026-11-02

- Fix the avatar gate that rejected all seven native employee models: their assets use Generic Bip01 rigs, not Humanoid avatars.
- Select URP-compatible materials for the game's render pipeline, including alpha clipping/blending, and avoid assigning nonexistent color properties on unlit shaders.
- Map native body bones to VRM humanoid metadata and transfer evaluated poses using mesh bind matrices. Preserve the original Animator and hand-IK evaluation instead of assigning Generic animation paths to unrelated VRM paths.
- Derive scale and floor alignment from the two skeletons; transfer hip motion, keep hidden native animation active, and restore original rendering/culling on unload or failure.
- Retry unavailable models/rigs without rebinding; back off heavy failures and retry again when the source character changes. Include diagnostic reasons in the log and update Chinese/English statuses.
- Pass 74 automated checks, including the seven asset-derived employee bone-name fixtures, retarget rotation and retry regressions. The separate engine probe could not run because the installed Unity editor lacks an activated license; in-game appearance/animation acceptance remains pending.

## 1.3.0 — 2026-11-02

- Add a host-only DeepSeek employee companion: F7 chat, one explicitly bound employee, per-shop local conversation and preference memory, and natural-language native-job assignment through validated tool calls.
- Use background, cancellable HTTP with bounded responses; keep API keys outside normal configuration/logs/packages, reject stale responses across shop/employee/authority changes, and report native task outcomes independently of model narration.
- Include the pinned DeepSeek-chan VRM under its separate CC BY-NC-SA 4.0 license and full credits. Add a minimal host-local base-texture/skinned humanoid loader using employee animations, with native visual fallback; MToon, springs and expressions are not implemented.
- Prepare expanded employee arrays during local save loading before host networking starts, preserving the active-client guard.
- Add 47 automated checks. The initial DLL loaded in-game; GUI/model/live-API acceptance remains incomplete after capture timeouts and the user's Computer Use stop.

## Unreleased

- Expand employee data, job-priority and NPC arrays before native save loading even when the local client is already active but the host server has not started. Prevent the load-time IndexOutOfRangeException that hides extra employees; connected remote clients remain excluded.

- Add English and Traditional Chinese mod UI with an immediate F10 language switch and persistent `Localization.UILanguage` setting; Auto follows Chinese game text and uses English otherwise.
- Translate settings, stock categories, employee naming controls, production summaries, status reports and companion UI messages while preserving employee names and existing chat/memory content.
- Add bilingual configuration descriptions and document UI language separately from random-name style.
- Fit longer English labels within existing controls and display the current plugin version in the status report.

## 1.2.10 — 2026-11-01

- Expand Chinese/English male, female and neutral name pools into thousands of given-name/surname combinations per group.
- Recognize unmodified legacy defaults automatically without expanding the configuration file; prioritize custom names and use the matching built-in pool after custom names run out.
- Exclude saved and currently displayed full names case-insensitively, including the employee being rerolled, and prefer unused given names.
- Never fall back to an already-used full name when a small pool is exhausted; retain the 25-character limit and the existing NPC-gender/style selection.
- Preserve all existing employee names until individual or bulk randomization is requested.

## 1.2.9 — 2026-11-01

- Trigger automatic production from same-recipe reserve stock (manufacturing warehouse plus ground/held finished boxes), excluding sales-shelf quantities.
- Keep sales-shelf labels as production targets, including empty labeled shelves, and prioritize recipes with the lowest reserve/shelf quantities.
- Submit jobs through the native central manufacturing desk so pending work appears in its queue; retain direct-workbench fallback for stores without a desk.
- Dispatch desk jobs into workbenches synchronously on the host, preserving already prepared ingredients when appending later jobs.
- Prevent duplicate recipes across the central queue, workbench queues and active production.
- Add per-recipe stock components and skip reasons to F11, plus an in-game production summary with target, shortage, pending and newly queued counts.

## 1.2.8 — 2026-11-01

- Enforce native host authority for the entire mod; the legacy `OnlyHost` value can no longer enable client-side operation.
- Hide mod controls and overlays, ignore mod hotkeys, and leave localization and employee hooks inactive on joining clients.
- Remove the optional host-only toggle while retaining its configuration key for upgrades.
- Clear injected UI and session state when leaving/changing rooms, and recheck authority before pending purchases or production cleanup.
- Preserve native multiplayer synchronization of game state changed by the host.

## 1.2.7 — 2026-11-01

- Require the native MART OS 2.0 unlock (`softwareUpgradePerk`, perk 29) before automatic pricing can run, including when configuration enables it.
- Lock the in-game pricing toggle and show the unlock requirement in settings and status reports; refresh availability after unlocking.
- Reset pricing schedule state when leaving a store so another store is evaluated independently.

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
