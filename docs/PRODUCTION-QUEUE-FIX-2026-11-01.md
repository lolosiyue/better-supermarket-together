# 自動排產修正：1.2.9

## 本機確認的原因

使用者回報已開啟自動生產，但生產看板右側沒有排程。當時載入的版本是 1.2.8，主機端自動採購正常，`AutoQueueProducts = true`、`OnlyShelfRecipes = true`、`AutoQueueThreshold = 30`。

對目前遊戲按一次 F11 後，日誌列出的 30 個成品配方，計算庫存最低為 31，全部超過門檻 30。舊程式把銷售貨架的成品也加進門檻，因此「貨架有貨、倉庫沒有備貨」時仍不補產。不是因為沒有開啟功能或載入舊 DLL。

例如截圖中的餅乾：貨架 36，其餘庫存 0。舊判斷得到 36，會略過；修正後備用量是 0，符合門檻 30。後續截圖的貨架 23、倉庫 25，備用量則為 25，同樣符合門檻。

## 修正後行為

- 生產門檻使用成品倉庫＋地上成品箱＋員工／玩家手持成品箱，排除銷售貨架。
- 銷售貨架仍指定要補的「成品編號＋完整配方」，包括售罄但保留標籤的貨架。歷史配方不會單獨變成目標。
- 有中央生產看板時，先加入看板的待分配佇列，再由原生流程派到工作台；沒有看板才直接排工作台。
- 看板轉交工作台改為主機同步處理，避免命令尚未處理時，配方暫時不在任一佇列而重複排單。
- 中央佇列、工作台佇列與製作中的同配方，均阻止重複排程。追加後續工作時會保留首項已備妥的原料狀態。
- F11 的 `PRODUCTION` 記錄分列貨架、倉庫、地面、手持、備用總量，以及略過原因；設定頁顯示目標、缺備貨、待製作、本輪新增數量。

中央看板的右側是「待分配」佇列。工作台接收工作後，項目會離開中央佇列；此時應以工作台佇列或 F11 診斷確認，不能只看中央佇列是否為空。

## 驗證範圍

修正依據為本機 1.2.8 日誌、使用者截圖及目前遊戲組件的原生排程流程。1.2.9 執行 Release 編譯、3 個實際編譯庫存類別的回歸案例，以及中央伺服器 API／新增掛點的型別核對。這些檢查不會執行遊戲方法。

新 DLL 安裝後需要重開遊戲。尚未在使用者目前商店實機驗收新版本的排程及生產結果。

## English

Version 1.2.8 included sales-shelf quantities in the automatic production threshold. The observed store had 30 recipe stock totals, all above its threshold of 30, so production was skipped despite empty reserve stock. Version 1.2.9 uses same-recipe manufacturing warehouse plus ground/held finished boxes as reserve, keeps shelf labels as targets, schedules through the central desk, and prevents duplicates across desk queues, workbenches and active production. F11 explains each recipe's stock and skip reason. Compilation, three pure stock regression cases and metadata checks cover this change; live acceptance of the new version remains pending.
