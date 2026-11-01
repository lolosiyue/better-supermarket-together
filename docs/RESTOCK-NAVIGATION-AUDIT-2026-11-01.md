# 補貨與尋路審查紀錄 / Restocking and navigation audit

日期：2026-11-01。修正版：Better Supermarket Together 1.2.6。

## 範圍與分工

審查本機插件原始碼，以及目前安裝遊戲組件中的相關方法。遊戲反編譯內容只供本機查核，不包含在公開原始碼或發佈包內。

| 流程 | 商品與容器 | 員工角色／動作 |
| --- | --- | --- |
| 普通採購 | 普通商品 ID、送貨佇列、`BoxData` | 採購決策與購物車，不等同於搬貨派工 |
| 普通入倉 | `BoxData` → `Data_Container` | 倉儲員，角色 3 |
| 普通補架 | 普通倉庫 → 普通銷售貨架 | 補貨員，角色 2 |
| 生產取料 | 普通商品原料 → 工作台／員工托盤 | 生產員工，角色 7、子任務 0；自動補托盤另行認領 |
| 成品入倉 | `ManufacturingBoxData` → `ManufacturingContainer` | 生產員工，角色 7、子任務 1 |
| 成品補架 | 成品倉庫 → 成品銷售貨架 | 生產員工，角色 7、子任務 2；必須同成品 ID、同完整配方 |

普通商品 ID 與成品 ID 可以數字相同，兩者仍是不同商品。尋路及狀態保護共用工具；商品資料、容器存取、配方與取料扣帳分別處理。

## 確認問題與修正

| 問題 | 觸發情況與影響 | 修正位置及行為 |
| --- | --- | --- |
| 派工清單過期及不完整 | 原生清單分批更新，忙碌或已補滿的前段任務可遮住其他缺貨貨架。 | `SmarterRestockerTaskPatch`、`SmarterManufacturerTaskPatch` 讀取即時貨架和來源；排除滿架，依缺貨比例排序。 |
| 派工未驗證完整路徑 | 取貨處或銷售架被隔斷，仍派工。單純在附近找到 NavMesh 點不代表能走到。 | `EmployeeNavigation` 驗證員工到取貨點、取貨點到目的地的完整路徑，使用該員工的 agent 類型和區域遮罩。 |
| 地上箱認領資料殘留 | 只要其他員工的 `randomBox` 還有參照就排除箱子，忽略任務是否已結束。 | `GroundBoxHelper` 只採用仍在對應普通／成品拾箱階段的認領；兩種箱子都驗證可達性。 |
| 滿倉略過可合箱的地上箱 | 沒有空排但有同商品／同配方的未滿箱時，仍忽略可搬箱子。 | `StorageSlots` 分開判斷普通／成品的空位與合箱空間；空紙箱可進回收流程。 |
| 容器索引因搬動／移除而改變 | 走到一半移動貨架，舊數字索引可能指到另一個物件。 | `RestockAssignments` 保留實體容器參照，重新解析索引；目標消失時釋放或重新選架。生產工作台與取料來源也保留物件參照。 |
| 補貨間隔內標籤／配方變更 | 開始補架後只增加數量，未再次確認目前商品或成品配方。 | `EmployeeWorkGuard` 在普通補架及成品補架的每次放貨前，驗證商品、配方、容量與占用狀態。 |
| 回倉時重新選架但沒有走過去 | 原流程抵達架 A 後重新選到架 B，能直接把剩貨存到 B。 | 回倉前重新確認站位；若選到新位置，先尋路移動再存放。合箱與入倉均驗證目前容器參照。 |
| 部分／失效路徑或長時間不動 | 員工卡在路徑末端或動態障礙，持續保留任務。 | `EmployeeWorkGuard` 對搬運階段監測路徑與移動進展，暫避失敗位置 30 秒；已持有的普通箱／成品箱保留並重走補架或回倉。 |
| 等待程序結束但狀態仍為等待 | 員工停在狀態 -1，沒有實際等待協程。 | 記錄等待目標，恢復對應狀態；正常放貨、取料與生產等待不列為尋路卡住。 |
| 必要原料因缺貨而被省略 | 原生生產員工在建立取料清單時只加入當下有庫存的必要原料組，缺少的一組可被略過。 | 生產取料保留每個必要組及替代選項；缺料等待、換可達來源，不在缺少必要組的情況下投產。 |
| 生產取消造成已領原料消失 | 手動改排程、刪工作台或更換工作後，已領取原料沒有退回。 | `ProductionLifecycle` 記錄本插件保護流程實際領取的原料，任務取消時退成普通原料箱；已接受投產的原料不再退回。 |
| 自動加料命令與檢查不同步 | 呼叫網路 Command 後立刻檢查托盤，會把尚未執行的命令當成失敗。 | 在主機呼叫原生伺服器實作，同步處理排程及加料，保留原生同步／視覺更新。 |
| 最後一份原料被立即消耗 | 托盤完成配方後立即清走原料，單靠托盤長度增加判斷會誤判失敗，漏扣倉庫。 | 以原生接受加料事件確認成功；成功後扣掉確切一份來源原料。 |
| 員工取料與自動托盤互相搶工作 | 一方已供料，另一方仍另外領取一整份。 | 自動補托盤和員工分開認領工作台；員工可先使用已有的托盤原料。工作台認領有世代與角色檢查，舊監看程序不會清掉新任務。 |
| 補產忽略手持箱及製作中批次 | 成品暫時不在倉庫、地面或排程清單中，便再排同配方。 | 按成品 ID＋完整配方計入員工／玩家手持箱，並追蹤製作中的批次。 |
| 生產倉位判斷漏看配方 | 空標籤判斷只比較成品 ID。 | 成品倉位、合箱及空標籤都比較正規化完整配方；增加容器排數與實體箱位的邊界檢查。 |
| 原料耗盡／夜間搬箱抹去標籤 | 最後一份原料或箱子移除後，一律把商品 ID 設成 -1。 | 保留固定標籤；夜間整理避開有效普通取箱、回倉和生產取料來源，忽略過期任務參照。 |
| 讀檔時所有庫存已空就不採購 | 只認正數庫存為當天見過的商品，完全售罄但仍有標籤的商品可能被排除。 | 普通商品的空標籤也視為使用中的商品，不混入成品標籤。 |
| 自動採購混入手動購物車／失敗後殘留 | 原流程追加並買掉整個購物車；遊戲拒絕時又可重複追加。 | 等手動購物車清空；送出前檢查自己的項目與金額。失敗或玩家同時編輯時只移除本次自動項目，不扣成功次數。 |

## 診斷及驗證界線

- F11 顯示 `ordinary-restock`、`ordinary-storage`、`production-ingredients`、`manufactured-storage`、`manufactured-restock`；另列路徑狀態、距離、等待目標及配方。`Employee recovery` 記錄重派原因。
- 本輪以本機遊戲原始方法對照、Release 編譯及新增 Harmony 掛點的組件中繼資料檢查為驗證範圍。沒有執行新的完整對局、GUI 或多人體驗驗收。
- 第一輪編譯發現 `standSpot` 是 `Transform` 而非 `GameObject`，修正後 Release 編譯通過：0 錯誤、2 個既有未使用欄位警告。
- 16 個新增 Harmony 目標的存在性、參數及回傳型別核對通過；另確認 3 個伺服器實作方法與 2 個生產狀態欄位存在。此項檢查只讀取組件中繼資料，沒有執行遊戲程式。
- 尋路保護依賴遊戲的 NavMesh。店面布局確實封路、員工不在有效 NavMesh、或地圖未提供可走區域時，不會強制瞬移員工穿越障礙。
- 取消退料僅針對本次執行中已記錄、尚未投產的員工領料；不是跨讀檔的原料交易復原系統。

## English overview

The audit separates ordinary purchasing/storage/restocking, ordinary ingredient collection for production, and manufactured-product storage/restocking. Manufactured products always match both their product ID and recipe. Fixes cover stale task lists and claims, complete route checks, container identity changes, shelf edits during placement, unreachable or stalled work, return-rack reselection, omitted required ingredients, cancelled ingredient collection, asynchronous command accounting, last-ingredient consumption, competing workbench owners, carried/in-progress production stock, labeled rows, and manual shopping-cart preservation.

Validation is limited to source/installed-assembly review, compilation, and metadata checks of the new Harmony targets. Live pathfinding, GUI, complete-game and multiplayer behavior still require in-game observation. No game assemblies or decompiled game sources are distributed.
