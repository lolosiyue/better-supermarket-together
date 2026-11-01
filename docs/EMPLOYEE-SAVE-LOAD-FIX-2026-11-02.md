# Extra employee save loading / 額外員工讀檔修正

## 問題與修正

原生 `SaveBehaviour.LoadEmployeesDataValues` 逐項把存檔員工寫入 NPC 管理器的現有陣列。如果存檔有 28 個員工欄位、當前陣列仍是原版大小，載入會發生 `IndexOutOfRangeException`，後段員工不會完整交給初始 NPC 生成流程。

本機讀檔可能早於房主伺服器啟動，也可能在 `NetworkClient.active` 已為 true、但尚未連線時發生。這時用伺服器是否啟動或 client 是否 active 作為唯一判斷，會略過必要的陣列準備。

修正將員工資料、職務優先順序及 NPC 物件陣列在原生載入之前擴充，保留所有既有欄位；已連線的遠端客戶端仍不執行這項準備。遊戲內自動化及購買名額仍須房主權限。

## 這次的存檔證據

- 診斷時的主存檔與自動存檔各有 28 筆非空員工資料；第 19–28 人的資料仍在。
- 較早的第 121、122 天備份各有 28 個欄位，但只有 18 筆非空員工資料。這些歷史檔本身沒有後面十人的紀錄。
- 遊戲日誌記錄了 `LoadEmployeesDataValues` 的陣列越界例外。
- 修正不會改寫既有存檔。診斷使用複本；重開遊戲後，須載入含完整員工資料的檔案。

## 驗證範圍

針對實際的容量準備函式與載入 prefix，驗證離線、client active 但未連線、已連線遠端客戶端，以及已啟動房主的行為。以 28 筆資料模擬原生複製迴圈，確認尾端資料可完整載入。這是程式層級的回歸檢查，並非遊戲畫面的完整讀檔驗收。

## English

The native employee loader copies saved records into an existing array. Loading an expanded roster before the arrays are resized raises `IndexOutOfRangeException`, preventing later employees from reaching the initial spawn process.

Local loading can happen before the host server starts, even when `NetworkClient.active` is already true. The fix prepares employee records, job priorities, and NPC object arrays before the native copy. A connected remote client remains excluded, and gameplay automation still requires host authority.

At diagnosis, the main save and autosave both retained all 28 employee records. The earlier day-121/day-122 backups contained only 18 nonempty records. Existing saves were inspected through copies and were not rewritten. Reload a save containing the complete roster after restarting the game with the updated DLL.
