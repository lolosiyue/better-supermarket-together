# Changelog

## Unreleased

- Add facial expressions: the loader now imports the face mesh's 23 morph targets (115 blend shapes across 5 primitives) with a fast dense reader and maps the VRM 1.0 preset expressions onto them. A face layer blends a mood/energy resting face, short event reactions (requested work done/failed, rice snack, strike, greeting wave, stare reactions, relationship stage up), natural blinking (2-6s, occasional double, slower when tired, damped by strong emotions per the VRM blend override) and talking mouth shapes timed to each chat line she sends. Replies may start with a hidden [happy]/[sad]/[angry]/[surprised]/[relaxed] tag that sets her expression. Eye bones are not used (only 42 vertices are weighted to them). Eating keeps a neutral face. 15 new checks (434 total); in-game appearance is unverified.
- Add relationship and daily diary, stored in the existing per-shop companion memory (older files load with empty data; damaged values are repaired). Affinity 0-1000 in five stages grows only from observed interaction with a 60-point daily cap (first chat, chats, new game day, requested work done, answering her greeting, company walks); stage shapes tone via context, follow/company distance shrinks to 2.2m from close friend, and at the top stage she comes over when the host is AFK for 5 minutes. She notices real-day absences. At closing a tool-free API call writes a one-to-two sentence first-person diary entry from the day's notes; 14 days are kept and older weeks are condensed into a long-term memory. Recent entries are sent as shared_memories and shown in F7. Remark replies are no longer labelled as autonomous decisions. 28 new checks (419 total); live diary text and tone are unverified.
- Talk to the companion from the native game chat: host messages starting with `@` (or full-width `＠`) are sent to the companion, with an optional leading alias (`DS`, `AI`, `DeepSeek`, the bound employee's name) stripped; `@<other player>` stays a normal mention. Replies appear in the game chat under the employee's name, split into at most 8 lines of 200 characters with rich-text markers neutralised. Errors and busy notices are shown only to the host. `[Companion] GameChat` turns it off; `GameChatBroadcast = false` keeps replies host-only. 14 new chat parsing checks pass; in-game chat delivery is unverified.
- Make the companion feel more human: a fixed VOICE section asks for short, casual, in-character chat lines with feelings and opinions (no lists, no AI self-reference, no unconfirmed work claims). A local inner state tracks energy (drains while working, recovers at rest and each game day) and mood (requested work done/failed, rice snack, strike, being talked to, loneliness after 20 minutes) and is sent as coarse words in `inner_state`. Replies are sent line by line with a length-based typing pause (`GameChatTyping`). Up to `ChatRemarksPerHour` (default 6, minimum 3-minute gap) tool-free unprompted chat lines react to those events; the model may answer SKIP. 16 new checks (367 total); live tone and remark frequency are unverified in game.
- Companionship behaviors, all local: natural eye contact (neck/head look-at toward the host camera within 5m, holding then glancing away; brief glances while working); after-hours or resting company walks to within ~3.5m for 60 seconds at a time without notices; a wave when the host arrives or returns from 3+ minutes AFK; a curious tilt or shy look-away after being stared at for 2 seconds (60-second cooldown); yawning when energy is low. New on/off preferences 看著我, 下班陪我, 迎接我, 被注視反應, 打哈欠; the gesture master switch silences greet/stare/yawn. 24 new checks (391 total); poses and navigation are unverified in game.
- Free the cursor and pause first-person look/movement whenever the mod panel is open, not only in F7. Opening from F10 or the manager board now behaves like the companion page, returning from F7 to settings keeps the cursor, and the previous cursor/player state is restored on close. Game actions stay captured while any panel page is open; F10 still closes the panel.
- Scale the panel with resolution: unchanged up to 1080p, larger on 1440p/4K, smaller only when the screen cannot fit it.
- Close the panel with Escape (the first press leaves a focused text field), by clicking outside it, or with a shared X button visible on every page. Escape is kept from reaching the game while it is held.
- "Randomize all names" requires a second press on the same page. Employee paging disables Previous/Next at the ends and shows "page x / y".
- Companion chat: Enter sends and Shift+Enter inserts a newline; pasted newlines and IME commits do not send. The message box shows a hint placeholder. The clear-memory confirmation resets on close and after four seconds.
- 337 companion checks and 18 input-capture checks passed; release compiled against installed game assemblies. Cursor capture on F10 confirmed in game. Escape versus the game's pause menu and IME Enter behavior remain unverified in game.

## 1.4.9 — 2026-11-02

- Add player-only assistant_task tools for persistent follow, personal targeted restocking, exact-shelf coworker delegation, product/shelf guidance, local inspection and return-to-job. Preserve the original job throughout assistant errands; finite outcomes resume follow with bounded retry delay. Explicit return, manual job changes, plugin disable or scene reset exit assistant mode.
- Capture the crosshair shelf when F7 opens. Product/name lookup reports ambiguity instead of choosing a random location. Inspection reports observed shelf/warehouse quantities, companion path reachability and other employee claims.
- Delegation chooses a reachable, idle resting coworker without an existing temporary assignment, targets the exact shelf and restores that worker's role safely. Reject duplicate shelf dispatches. Guide waits if the player falls behind, finishes when the player arrives nearby, and gives up after 180 seconds before resuming follow.
- Follow targets a side/rear offset. Local proactive help, idle routines and rice behavior yield to assistant mode. No approach-player completion reports.
- 337 companion checks passed, including assistant schema, authority and tool budget tests. Release compiled against installed game assemblies. Live API interpretation, navigation and multi-worker handoff remain unverified.

## 1.4.8 — 2026-11-02

- Add a short nearby nod on entering a visible 3.5m encounter, with a 90-second cooldown; head-only overlay does not stop navigation or work. No repeat while standing beside the player.
- Add a brief shoulder/arm release after at least 30 seconds of work-state activity with confirmed experience progress, followed by three seconds of genuine idle. Five-minute cooldown; no approaching the player or reports.
- Remember a preferred reachable native rest-area position per shop. Override only the bound companion's native rest destination while no explicit plan, API autonomy or snack owns the employee. Fall back when occupied, unreachable or the rest area moved; do not change job or force an off-duty worker to rest. Clear-all memory also clears the spot.
- Add on/off behavior preferences for nodding, post-work relaxation and fixed rest position. Gesture master-off also suppresses nodding and relaxation.
- 322 companion checks passed including 14 new encounter/work-evidence/cooldown/preference/rest-coordinate checks. Runtime pose appearance, line-of-sight and native destination integration remain unverified.

## 1.4.7 — 2026-11-02

- Connect explicit saved behavior preferences to local proactive help, rice snacking, gesture frequency and individual stretch/look/skirt gestures. Recognize a bounded canonical vocabulary and exact legacy phrases; arbitrary prose remains conversational memory.
- Replace conflicting versions of the same setting on remember; forget removes all recognized versions of that setting so older values cannot silently return. Existing per-shop memory format is unchanged. Config disable remains a master switch.
- Show effective behavior settings in F7 and current model context. Disabling local help requests safe handoff of an active local help task. Fewer gestures use 3–6 minute opportunities instead of 1–2 minutes; existing random chance and idle requirements remain.
- 308 companion checks passed, including 15 new preference/forget/config/persistence/schema checks. Live model interpretation and in-game acceptance remain pending.

## 1.4.6 — 2026-11-02

- Add local proactive help: after at least 15 seconds of confirmed idle time, select one ordinary shelf row within 12 metres with reachable unreserved warehouse stock. Attempt at most every three minutes. Stop selecting new targets after confirmed delivery; finish safe stock handoff and restore the original role. Abandon empty/contested targets or a 90-second unsuccessful attempt. Manual role changes take precedence.
- Add occasional local idle stretching, looking around and skirt-smoothing hand gestures. Each lasts about 3–5 seconds, with 60–120 second opportunity intervals and a 65% chance. Blend out on native activity or player intervention; never take over navigation or change jobs for a gesture.
- Dedicated checkout staff, security and production are not borrowed. Storage, repair and order staff require conservative no-work checks; restock staff retain native restocking. Disable local help/gestures with Companion.ProactiveHelp / Companion.IdleGestures. Both default on and require no API call; existing API autonomy takes precedence.
- No approach-player reporting. Existing F7 plan outcomes remain available. No changes to preference-memory semantics.
- Release build and 293 companion checks passed, including 18 new eligibility/cooldown/schema checks. Unity animation and live gameplay acceptance remain pending.

## 1.4.5 — 2026-11-02

- Show a DeepSeek model portrait for the bound employee in native assignment, dismissal list and employee detail panels. Match the live employee object and slot, never replace shared prefab sprites or applicant portraits.
- Render one 256px portrait from the authored model during avatar loading. Restore native icons on unbind, disable or employee replacement; preserve native icons if portrait generation fails. Temporary camera/clone/render target are cleaned up.
- Release build verified against installed game assemblies. In-game framing and UI appearance remain unverified; portrait becomes available after the companion avatar loads.

## 1.4.4 — 2026-11-02

- Clarify the supermarket-assistant prompt: current player work/delegation requests already authorize supported finite actions. Inspect then execute without redundant approval; retain clarification for ambiguous targets and unsupported actions.
- Capture Rewired button and axis reads while F7 is open or a mod text field has focus. Preserve Unity text/mouse events, suppress mod shortcuts, and wait for held keyboard/mouse input plus its release frame before restoring gameplay input.
- Restore panel/player state when the plugin is destroyed. No live game or live API acceptance yet.

## 1.4.3 — 2026-11-02

- Default staff dispatch to one confirmed work cycle followed by safe restoration of the original role. Add optional duration and explicit permanent continuous assignments; temporary rest defaults to 120 seconds. Preserve the initial role across replacement temporary orders, respect manual overrides, bound restoration retries, and time out no-progress cycles after 15 minutes.
- Finite personal companion plans also return to the original role instead of always resting; assign_work defaults to one cycle (rest/autonomy use 120 seconds). Cancelling temporary duties schedules safe restoration.
- Add a rare idle-only rice break: choose uniformly among reachable, stocked Rice/Basmati Rice/Long Grain Rice, walk to the shelf, consume one unit through native NPCGetsItemFromRow, and play a local eight-second procedural bowl/spoon animation. First and subsequent opportunities are 20–40 minutes apart, at most once per game day. Missing stock/path/visuals do not debit inventory.
- Add RiceEasterEgg configuration (enabled by default), interruption cleanup and localized F7 consumption notices. Model context receives the latest rice event. This is a stylized visual gag using a rice product unit; it does not model cooking.
- Pass 275 checks and Release build against local game assemblies. New coverage includes original-role restoration, delayed handoff, explicit durations, cancellation/manual overrides, rare-event gating, native rest state 1 and exact one-unit consumption. No live API, game stock or save was used for these tests. Live animation/network acceptance remains pending.

## 1.4.2 — 2026-11-02

- Add inspect_staff, assign_staff and cancel_staff: player-directed, host-only assignments to other hired employees, using non-recycled session IDs tied to employee objects and UI slots. Validate the whole batch before queueing; exclude customers and the bound companion.
- Queue up to 64 role changes, wait for safe handoff, expire after 180 game seconds, and verify the native role/priority after invocation. Manual overrides, dismissal/replacement, rebind, shop reset and disable cancel stale work. Native autonomous mode retains its existing bound-employee scope.
- Show pending counts and localized outcomes in F7; include recent dispatch outcomes in the next model context. Applied roles remain in effect after pending commands are cancelled.
- Include foot/grip IK and authored VRM secondary-motion preview from this session. Runtime animation and staff-command acceptance are still pending.
- Pass 244 checks and Release build against local game assemblies. Catch test-runner failures, print diagnostics and return exit code 1 instead of allowing Windows crash dialogs; controlled failure-path probe passed. No live API requests or game-save changes.

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
