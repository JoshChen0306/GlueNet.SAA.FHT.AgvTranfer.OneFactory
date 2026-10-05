# diff 邏輯比對報告 — feature/cleanup-legacy-area-code

> 產出日期：2026-10-02（T16）
> 比對範圍：`git diff glueDevelop...HEAD`（HEAD = `c25bc33`），只看已 commit 的內容
> 工作目錄的 `WebGui/SCP/appsettings.json` 有本機連線字串修改未提交，**本報告以 HEAD 為準**；分支對 `appsettings.json` 只提交了 `AreaRules` 區段（+20 行，其他區段無差異）。
> 對照文件：`spec.md`「行為變更清單」B1～B8、`殘留清單.md`、`工作計畫.md`（T6～T15）
> 本報告未執行建置與測試、未連任何 DB；測試結果引用 `工作計畫.md` 各任務的完成紀錄。

---

## 1. 摘要

### 1.1 數量

| 類別 | 檔案數 | 增 | 刪 |
|------|-------:|---:|---:|
| 產品程式碼／設定／畫面（本報告逐 hunk 比對對象） | 15 | 196 | 1,435 |
| 測試與測試設定（`svrPairTests/*`、`SCP.Tests/*`、`Recipe.ini`、兩個 csproj） | 7 | 1,249 | 2 |
| ToDo 文件（`ToDo/20261001_*/*.md`） | 5 | 715 | 0 |
| **合計** | **27** | **2,160** | **1,437** |

15 個產品檔：`Dispatch/cTest/Form1.cs`、`Form1.Designer.cs`、`Dispatch/svrPair/cPair.cs`、`WebGui/SCP/Controllers/CommonController.cs`、`DispatchController.cs`、`Helpers/AreaRuleProvider.cs`（新增）、`Resources/Views/Dispatch/Index.resx`、`Index.en.resx`、`Views/Dispatch/Index.cshtml`、`Views/Home/WarSituation.cshtml`、`Views/Shared/_Layout.cshtml`、`appsettings.json`、`wwwroot/js/Dispatch.js`、`wwwroot/js/Hitchhike.js`（刪除）。
（`Dispatch/svrPairTests/Recipe.ini`、`svrPairTests.csproj`、`SCP.Tests.csproj` 歸在測試類。）

### 1.2 Commit 對照

| Commit | 類型 | 任務 | 對應 |
|--------|------|------|------|
| `ba8621a` | docs | 計畫＋測試骨架 | 測試基礎 |
| `c6d660d` / `35f337a` / `3d49998` | test | T2 / T3 / T4 | 測試基礎 |
| `74503af` | refactor(dispatch) | T6 | 死碼刪除 |
| `4493828` | feat(dispatch) | T7 | B4 |
| `5d8fd8a` | refactor(dispatch) | T8 | 死碼刪除 |
| `3f76f82` | refactor(scp) | T9 | 死碼刪除 |
| `4039799` | feat(scp) | T9 | B4 |
| `219f1bd` | refactor(scp) | T10 | 死碼刪除（含 B4 的前端對應，見 §3.3） |
| `190d3d3` | feat(scp) | T11 | 新增元件 |
| `223428d` | feat(scp) | T12 | B1、B6、B7 |
| `f9b9598` | feat(scp) | T13 | B2、B3 |
| `c18ea75` | feat(dispatch) | T14 | B5 |
| `c25bc33` | fix(scp) | T15 | B8 |

### 1.3 結論

- 已逐 hunk 讀完 15 個產品檔的全部差異（§2），每一個 hunk 都歸到 B1～B8、死碼刪除、純結構、新增元件或測試基礎其中一類。
- **一廠現有資料與設定下，沒有發現結果會改變、卻沒列在 B1～B8 的邏輯差異。**
- 有 **4 項清單外觀察**（§3.2），都不會改變一廠網頁與 cPair 的派送結果，但應告知使用者：
  1. 首頁（WarSituation）的 Console 會多出 SignalR Warning（`SendMissionChange` 沒有接收端）。
  2. 物料視窗不再把工單內的 `^VCUT` 字樣去掉才顯示（B6 的前端對應，B 表沒有寫到）。
  3. Android `DispatchActivityNewC` 會直接寫 AssignFlag = `W` / `R` 的 oNeed，B4 刪除後這類資料永遠不會被處理（Android 不在本計畫範圍）。
  4. spec B7 說「一廠無這些區域」，但 M 區一廠有；只是 V-cut 勾選欄一直隱藏，這條路徑本來就走不到，結果不變。
- 另外有 6 項「B 項的附帶效果」已記在工作計畫裡，只是 spec B 表沒有寫出來（§3.1），例如 cPair 主迴圈每輪呼叫 `GenerateoRequireByoNeed()` 從 4 次減為 1 次。

---

## 2. 逐檔逐 hunk 比對

分類代號：**B1～B8**＝spec 行為變更清單｜**死碼**＝一廠走不到或沒有呼叫者（附 Grep 證據）｜**結構**＝註解、using、排版｜**新增元件**＝AreaRuleProvider / AreaRules 設定｜**測試基礎**

Grep 證據 G1：在全 repo 的 `*.cs/*.js/*.kt/*.cshtml` 搜尋 `updateStationCache|autoSelectEndStationInRange|filterBeginStationByDestination|GetHitchhikeStation|Hitchhike|ReLogin|SettingBlockUseFlag|machineScanRow|vcutTagRow|vcutCheckboxRow|registerLotVcut|RejectdButton|ChangeButton|UnloadButton`，結果只剩 `ToDo/`、根目錄二廠 `*_實作計畫.md`、`git_tracked_files.txt` 的文字。`UpdateoPort` 只剩 `PortController.UpdateoPort`（`Port.js` 呼叫 `/port/UpdateoPort`，是另一支 Controller）與 cPair 的 `UpdateoPortBgnToEnd`。
Grep 證據 G2：搜尋 `AssignFlag\W{1,6}["'](P|W|R)["']`，產品程式碼 0 筆（只剩 ToDo 文件）。

### 2.1 `Dispatch/svrPair/cPair.cs`（+6 / −324）

| # | 位置（base 行號） | 變更內容 | 分類 | 任務 |
|---|------------------|---------|------|------|
| 1 | 欄位 :24-27 | 刪 `mdtQuery`、`moPairWay`、`mPanelDoB2C`、`mUnloadAuto` | `moPairWay` 死碼（只寫入、沒有讀取）；`mdtQuery` 在 T7 後沒有使用者；其餘兩個屬 B4 | T6 / T7 |
| 2 | `SettingBlockUseFlag` :53-59 | 刪除 D/E 區啟用停用 | 死碼（唯一呼叫者為 `cTest/Form1` 的 4 個按鈕，同分支一併刪除；G1） | T8 |
| 3 | `Initial()` :69-70 | 不再讀 `Recipe.ini` 的 `PanelDoB2C`、`UnloadAuto` | B4 | T7 |
| 4 | `Initial()` :78 | 不再呼叫 `LoadBasicSettingToTables()` | 死碼（查詢結果 `moPairWay` 沒有人讀）；附帶：啟動 log 少一行「02.載入常用資料」 | T6 |
| 5 | `LoadBasicSettingToTables` :88-99 | 刪除整個方法，含空 catch | 死碼 | T6 |
| 6 | `MainProcess` :115-119 | 刪除已註解的 B→A 呼叫 | 結構（註解） | T6 |
| 7 | `MainProcess` :121-136 | 刪除 B→C／C→A/B/D、F→A/B/D、E→F 四個分支，以及其後的 3 次 `GenerateoRequireByoNeed()` | B4（附帶效果見 §3.1-1） | T7 |
| 8 | `MainProcess` :141-142 | 刪除已註解的 `RecyclingoRequireByAssignFlag()` 呼叫 | 結構（註解） | T6 |
| 9 | `#region 1-0a` 標題 | 註解文字改成「不分區域」 | 結構 | T14 |
| 10 | `ProcessSingleoNeed` :203-236 | 刪除 20 個 case 的區域字母 switch，改為一律呼叫 `ProcessoNeedToRequire`；Log 從「05.處理平板配對／系統配對」統一為「05.處理配對」 | B5 | T14 |
| 11 | `GenerateoNeedDataByMCSAsBtoC`、`GenerateoNeedDataByMCSEsBtoF` :311-341 | 刪除 | B4 | T7 |
| 12 | `GenerateoNeedDataByMCSAsBtoA` :344-359 | 刪除 | 死碼（呼叫早已註解；G1 外另以 Grep 方法名確認 0 個呼叫點） | T6 |
| 13 | `GenerateoNeedDataByMCSAsEtoABD` :361-375 | 刪除 | B4 | T7 |
| 14 | `GenerateoNeedDataByMCSAsCtoABD` :378-420 | 刪除 C 區換料（`W`）／退料（`R`）流程 | B4 | T7 |
| 15 | `GetoPort_HaveRack_NoPair_CanWork_Sort_ByBlock` :488-495 | 刪除 | 死碼（T7 後只剩已刪除的 BtoA / EtoABD 呼叫它） | T7 |
| 16 | `GetoPort_HaveWorkOrder_…` :518-526 | 刪除 | 死碼（沒有呼叫者） | T6 |
| 17 | `GetoPort_PartNoTheSame_…` :528-548 | 刪除 | 死碼（T7 後只剩已刪除的 BtoC / EsBtoF 呼叫它） | T7 |
| 18 | `InsertoNeed` :606-614 | 刪除 cPair 自建 oNeed（TaskSource=`MCS`） | 死碼（T7 後只剩已刪除的四個自動配對流程呼叫它） | T7 |
| 19 | `DeleteoRequireByAssignFlag` :645-650 | 刪除 | 死碼（只被 #20 使用） | T6 |
| 20 | `RecyclingoRequireByOkFlag` :679-683 | 刪除 `PanelDoB2C=T` 時把 AssignFlag `P` 改回 NULL 的動作 | B4（`P` 只由 #14 寫入；G2 確認沒有其他程式寫 `P`） | T7 |
| 21 | `RecyclingoRequireByAssignFlag` :694-722 | 刪除 | 死碼（呼叫已註解） | T6 |
| 22 | `GenerateoMissionDataByIdleShuttleFromoShuttle`、`GetBeginStationFromoPair` :747-774 | 刪除 | 死碼（沒有呼叫者；`GetBeginStationFromoPair` 內容本身是空迴圈） | T6 |

**cPair 主迴圈移除步驟的檢查**：刪除後的 `MainProcess` 只剩 `GenerateoRequireByoNeed` → `GenerateoMissionByoRequire` → `RecyclingoRequireByOkFlag` → `RecyclingoNeedByAssignFlag`，這四個步驟和 base 的順序、內容相同（只有 #20 刪掉 `P` 還原）。被刪掉的呼叫逐一對照：#6、#8 是註解；#7 的四個流程與 3 次多餘的 `GenerateoRequireByoNeed()` 屬 B4。**全部屬 B4 或死碼，沒有其他步驟被移除。**

保留未動：`ProcessoNeedToRequire`（A 區工單空白標 `C`）、`CheckoPortBgnToEndIsNullAndUseFlagAsY`、`GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock`（public，`cPairTests.cs` 有測試直接呼叫）、`HandleoNeedRowException` / `SafeCol` / 隔離機制。

### 2.2 `Dispatch/cTest/Form1.cs`（+1 / −28）、`Form1.Designer.cs`（+1 / −81）

| # | 位置 | 變更內容 | 分類 | 任務 |
|---|------|---------|------|------|
| 1 | `Form1.btnAuto_Click` 啟動分支 | 刪 `tlp2.Visible = false` | 死碼（`tlp2` 裡只有 4 顆 D/E 按鈕） | T8 |
| 2 | `Form1.btnAuto_Click` 停止分支 | 刪 `tlp2.Visible = true` 與 `this.Height = 199`；`EndPair()` 去掉行尾空白 | 死碼＋結構。附帶：按 STOP 後視窗不再拉高（原本拉高是為了顯示 D/E 按鈕；T8 已截圖確認 262×111、無空白） | T8 |
| 3 | `Form1` 4 個 `btnBlock*_Click` | 刪除 D/E 區啟用停用事件 | 死碼（D/E 區在一廠不存在） | T8 |
| 4 | `Designer.InitializeComponent` | 刪 4 顆按鈕與 `tlp2` 的建立、屬性、`SuspendLayout`/`ResumeLayout`、`Controls.Add` | 死碼 | T8 |
| 5 | `Designer` `frmMain` 註解行 | `// ` 改成 `//` | 結構（排版） | T8 |
| 6 | `Designer` 欄位宣告 | 刪 5 個欄位 | 死碼 | T8 |

### 2.3 `WebGui/SCP/Controllers/DispatchController.cs`（+35 / −227）

| # | 位置（base 行號） | 變更內容 | 分類 | 任務 |
|---|------------------|---------|------|------|
| 1 | using | 新增 `using SCP.Helpers;` | 結構 | T12 |
| 2 | `Index()` 舊群組篩選 :87-96 | 群組 6 從「C 或 D」、群組 5 從「F」改為空清單 | 死碼（結果相同，見 §3.3-2） | T9 |
| 3 | `Index()` 尾段 | 新增 `ViewBag.AreaRules`（`registerAreas` / `releaseAreas` / `clickableAreas`，鍵名寫成 camelCase） | 新增元件（給 B2/B3 前端使用） | T13 |
| 4 | `InsertoNeed` :187-201 | 不再讀 `Area` / `btnName` / `Status`；`assignFlag` 固定為 `""`；刪除 `RejectdButton` 退料分支（起訖站對調、找 B 區空位） | B4（影響見 §3.3-3） | T9 |
| 5 | `InsertoNeed` catch :219-222 | 空 catch 改記 `LogMgt.Logger?.Warn`（含起訖站與錯誤訊息），仍回 `Ok()` | B8 | T15 |
| 6 | `UpdateoPort` :227-246 | 刪除 F 區下料完成（含空 catch） | 死碼（唯一呼叫者為 `Dispatch.js` 的 area F 分支，同分支刪除；G1） | T9 |
| 7 | `ReLogin` :304-319 | 刪除帳密驗證 API | 死碼（唯一呼叫者為 `#reLoginButton`，同分支刪除；G1）。屬 C 區換料／退料流程的一部分 | T9 |
| 8 | `RegisterLot` :427 | 刪除讀取 `isVcutMaterial` | B7 | T12 |
| 9 | `RegisterLot` :434-437 | 工單必填不再有 R 區例外 | B7 | T12 |
| 10 | `RegisterLot` :441-445 | 貨架條碼必填從寫死 J/H/I/K/L 改為 `AreaRules.RackIdRequiredAreas`（一廠設定 K、L） | B7（一廠 K、L 結果不變，T3 案例未修改即通過） | T12 |
| 11 | `RegisterLot` :481-506 | 刪除移除 `^VCUT`/`^DONE`/`^NG`/`^RETURN` 的程式 | B6 | T12 |
| 12 | `RegisterLot` :481-506 | 刪除 T 區加 `^VCUT^DONE`、M 區勾選加 `^VCUT`、I 區加 `^NG` | B7（M 區說明見 §3.2-4） | T12 |
| 13 | `MarkEmptyTray` XML 註解 | 刪「適用於 O/P/S/N 區」 | 結構（註解） | T9 |
| 14 | `Release` XML 註解 | 刪 O/P/S/N、EE 區說明 | 結構（註解） | T9 |
| 15 | `Release` G 區分支 :705-722 | 刪除 G→J | 死碼（一廠沒有 G 區） | T9 |
| 16 | `Release` K / M / B 分支 :723-784 | 三段寫死的查詢改為依 `ReleaseRoutes` 逐區查詢；查詢條件與排序（`Priority` 由大到小、再 `Port` 由小到大）不變；無空位訊息改由 `AreaNames` 組成 | 新增元件／設定化。一廠結果不變：K→「L區（3F下料區）沒有可放置的空位」、M/B→「A區（1F備貨區）沒有可放置的空位」，與原字串完全相同；T2 案例未修改即通過 | T12 |
| 17 | `Release` I 區分支 :785-803 | 刪除 I→L1～L4 | 死碼（一廠沒有 I 區） | T9 |
| 18 | `Release` else 分支 :804-846 | 「其他區域依序找 M→Q→R」改為回 400「此區域未設定回送路線」 | B1 | T12 |
| 19 | `Release` :851-856 | 刪除 I 區回送寫入 `^RETURN`；WorkOrder 固定為 `""`；註解更新 | 死碼（只有 I 區會寫入） | T9 |

### 2.4 `WebGui/SCP/Controllers/CommonController.cs`（+0 / −19）

| # | 位置 | 變更內容 | 分類 | 任務 |
|---|------|---------|------|------|
| 1 | `GetHitchhikeStation` :152-170 | 刪除 G→J 順風車查詢 API | 死碼（唯一呼叫者 `Hitchhike.js` 同 commit 刪除；G1。一廠沒有 G/J 區，原本回傳的永遠是空字典） | T10 |

### 2.5 `WebGui/SCP/Helpers/AreaRuleProvider.cs`（新增 +91）

| # | 位置 | 變更內容 | 分類 | 任務 |
|---|------|---------|------|------|
| 1 | 整檔 | 建構子接 `IConfiguration`、讀 `AreaRules` 的 `RegisterAreas`、`RackIdRequiredAreas`、`ReleaseRoutes`、`AreaNames`；區段缺漏記 Warning、不拋例外；代號去空白並轉大寫 | 新增元件 | T11（`AreaNames` / `GetAreaLabel` 在 T12、`RegisterAreas` / `ReleaseAreas` 在 T13 加入） |

檔頭有 UTF-8 BOM（diff 第一行 `﻿using`）。不需要 DI 註冊，所以沒有動到無 BOM 的 `Program.cs`。

### 2.6 `WebGui/SCP/appsettings.json`（+20，HEAD 版本）

| # | 位置 | 變更內容 | 分類 | 任務 |
|---|------|---------|------|------|
| 1 | `Area` 與 `FloorSettings` 之間 | 新增 `AreaRules`：`RegisterAreas [A, L]`、`RackIdRequiredAreas [K, L]`、`ReleaseRoutes {K:[L], M:[A], B:[A]}`、`AreaNames {A,B,K,L,M}` | 新增元件 | T11 / T12 |

其他區段（`ConnectionStrings`、`Area`、`FloorSettings` 等）在 HEAD 與 glueDevelop 之間沒有差異。工作目錄裡未提交的連線字串不在本報告範圍。

### 2.7 `WebGui/SCP/wwwroot/js/Dispatch.js`（+38 / −436）

| # | 位置（base 行號／函式） | 變更內容 | 分類 | 任務 |
|---|----------------------|---------|------|------|
| 1 | `bindStationLotEvents` :34-47 | 標記 `lot-manageable` 的區域從寫死 M/T/Q/R 改為 `clickableAreas`；新增 `getAreaRules()`、`getStationArea()`；刪除 console.log | 新增元件（前端讀設定）。附帶：A/B/K/L 也會加上 `lot-manageable` class 與 `cursor:pointer`，但沒有任何 CSS 用這個 class（Grep 只有這一處），而且 Bootstrap `.btn` 本來就是 `cursor: pointer`，畫面沒有變化 | T13 |
| 2 | `.station-btn` click :56-62 | `validAreas` 寫死 16 個字母改為 `clickableAreas`（A、L、B、K、M） | 新增元件。一廠可點選區域不變（C 原本就不可點），T13 瀏覽器回歸 16 站一致 | T13 |
| 3 | `$(function)` 區域變數 | 刪 `btnName` | B4（前端對應） | T10 |
| 4 | `#Floor change` 三處 | 刪 `$("#machineScanRow").hide()` | 死碼（元素同分支自 cshtml 刪除；G1） | T10 |
| 5 | `#Area change` :213-219 | 刪除 area C 顯示換料／退料按鈕 | B4（前端對應；按鈕本身同分支自 cshtml 刪除） | T10 |
| 6 | `#EndStation focus` `case "B"` :388-390 | 不再列出 C 區站點 | B3 | T13 |
| 7 | `#EndStation focus` `case "D"/"H"/"Q"/"R"` | 刪除 | 死碼（一廠沒有這些區；刪除後若出現會走 `default` 顯示全部，見 §3.3-5） | T10 |
| 8 | `#WorkOrder blur` :419-428 | 刪除 area D 工單代入 | 死碼 | T10 |
| 9 | `.ConfirmButton click` | 刪 `modal` / `btnName` 變數；Q 區提示文字分支改為固定「目標區域」；終點已有貨架的 `area !== "C"` 例外刪除 | 死碼（Q）＋ B4（C） | T10 |
| 10 | `.ConfirmButton click` 尾段 :487-494 | 非 `ConfirmButton` 時開帳密驗證視窗的分支刪除，一律開派車確認視窗 | B4（前端對應；只有 Change/Reject 按鈕會走到） | T10 |
| 11 | `#reLoginButton click` :501-528 | 刪除 | B4（前端對應；G1） | T10 |
| 12 | `#UnloadButton click` :530-555 | 刪除 F 區下料完成 | 死碼（G1） | T10 |
| 13 | `.SubmitButton click` | 不再送 `btnName`、`Status`；固定呼叫 `/Dispatch/InsertoNeed`（刪除 area F 改呼叫 `UpdateoPort`） | 死碼（F）＋ B4（後端已不讀，T9 先行） | T10 |
| 14 | 掃描機台功能 :680-801 | 刪除 `#machine-scan-btn`、`#machine-manual-btn`、`processManualMachineInput`、`startMachineScanner`、`initMachineScanner` | 死碼（`machineScanRow` 一律隱藏；內部呼叫不存在的 `filterBeginStationByDestination`；G1） | T10 |
| 15 | 物料視窗 :837-863 | 刪除 V-cut 判斷與 `#vcutTagRow` 顯示；**工單顯示不再去掉 `^VCUT`** | 死碼（只有 T 區會顯示）＋清單外 §3.2-2 | T10 |
| 16 | 物料視窗 :868-876 | 刪除 `#registerLotVcut` 清空與 `#vcutCheckboxRow` 隱藏（兩個分支都是 hide） | 死碼 | T10 |
| 17 | 物料視窗 :890-892 | `releaseAreas` 寫死 9 個字母改為 `getAreaRules().releaseAreas`（B、K、M） | 新增元件。一廠結果不變 | T13 |
| 18 | 物料視窗 :895-911 | 刪除 I 區特例（可同時 Release 與物料登記） | 死碼（一廠沒有 I 區） | T10 |
| 19 | 物料視窗 回送區分支 | 原 else 分支（O/P/S/N/G/K）提升為唯一分支，內容相同（標記空板／Release 權限檢查／空架不顯示） | 結構（合併） | T10 |
| 20 | 物料視窗 登記區分支 :930-940 | 刪除 T 區有料時依 `DONE` 決定按鈕 | 死碼（一廠沒有 T 區） | T10 |
| 21 | `#btnEditLot` 帶入現值 :971-980 | 刪除去掉 `^VCUT` 的處理與 `#vcutCheckboxRow` 隱藏；工單原樣帶入 | 死碼＋清單外 §3.2-2 | T10 |
| 22 | `submitLotForm` :1106、:1113-1120、:1132 | 刪除 `isVcutMaterial` 讀取與送出；工單必填不再有 R 區例外；刪 console.log 的 V Cut 欄位 | B7 | T12 |
| 23 | `updateStationCache` :1250-1265 | 刪除 | 死碼（沒有呼叫者；G1） | T10 |
| 24 | `filterBeginStationOptions` `case "L"` :1367-1397 | 刪除二廠版（只列 L1～L4、排除 `^RETURN`/`^NG`），原本被它遮蔽的一廠版 `case "L"` 生效 | B2 | T13 |
| 25 | :1466-1470 | 刪除殘留的空註解區塊 | 結構（註解） | T10 |
| 26 | `autoSelectEndStationInRange` :1583-1619 | 刪除 | 死碼（沒有呼叫者；G1） | T10 |

### 2.8 `WebGui/SCP/wwwroot/js/Hitchhike.js`（刪除 −153）

| # | 位置 | 變更內容 | 分類 | 任務 |
|---|------|---------|------|------|
| 1 | 整檔 | 刪除順風車視窗（訂閱 `SendMissionChange`，呼叫 `GetHitchhikeStation`） | 死碼（一廠沒有 G/J 區，API 永遠回空，視窗不會出現）。附帶影響見 §3.2-1 | T10 |

### 2.9 `WebGui/SCP/Views/Dispatch/Index.cshtml`（+4 / −109）

| # | 位置（base 行號） | 變更內容 | 分類 | 任務 |
|---|------------------|---------|------|------|
| 1 | :68-79 | 刪除 `#machineScanRow` | 死碼 | T10 |
| 2 | :83-96 | 依群組顯示按鈕：群組 5 原本只有「下料完成」→ 不顯示按鈕；群組 1、6 原本多「換料」「退料」→ 只剩「派車」；其他群組只有「派車」不變 | 死碼（下料完成）＋ B4（換料／退料） | T10 |
| 3 | :178-195 | 刪除 `#unloadModalToggle` | 死碼 | T10 |
| 4 | :196-219 | 刪除 `#RejectModalToggle`（含 `#Status`） | B4（前端對應） | T10 |
| 5 | :220-250 | 刪除 `#ReLoginModalToggle` | B4（前端對應） | T10 |
| 6 | :308-311、:335-343 | 刪除 `#vcutTagRow`、`#vcutCheckboxRow` | 死碼 | T10 |
| 7 | `<script>` 區 | 新增 `window.areaRules = @Html.Raw(Json.Serialize(ViewBag.AreaRules))` | 新增元件 | T13 |

### 2.10 其他畫面與資源檔

| 檔案 | 位置 | 變更內容 | 分類 | 任務 |
|------|------|---------|------|------|
| `Resources/Views/Dispatch/Index.resx` | :138、:153、:177 | 刪除 `Change`、`Reject`、`UnloadFinish` 字串 | 死碼（引用的按鈕已刪除） | T10 |
| `Resources/Views/Dispatch/Index.en.resx` | 同上 | 同上 | 死碼 | T10 |
| `Views/Home/WarSituation.cshtml` | :67 | 刪除引用 `Hitchhike.js` | 死碼 | T10 |
| `Views/Shared/_Layout.cshtml` | :51-88 | 刪除 `#hitchhikeModal` | 死碼 | T10 |

### 2.11 測試基礎（不影響產品行為）

| 檔案 | 變更內容 | 任務 |
|------|---------|------|
| `SCP.Tests/SCP.Tests.csproj` | 加 `Microsoft.EntityFrameworkCore.Sqlite 7.0.15` | T2 |
| `SCP.Tests/TestInfrastructure/ScpTestDb.cs` | SQLite 記憶體資料庫測試基底；T12 加入一廠 `AreaRules` 預設值（測試未帶 AreaRules 時自動帶入） | T2 / T12 |
| `SCP.Tests/Controllers/DispatchControllerReleaseTests.cs`、`DispatchControllerLotAndNeedTests.cs`、`Helpers/AreaRuleProviderTests.cs` | 快照測試與新行為測試 | T2 / T3 / T9 / T11 / T12 |
| `svrPairTests/GenerateoRequireByoNeed_AreaGateTests.cs`、`svrPairTests.csproj` | cPair 整合測試與 `Compile Include` | T4 / T14 |
| `svrPairTests/Recipe.ini` | 刪除 `PanelDoB2C=T`、`UnloadAuto=T` | T7 |

測試修改檢查（`git show 223428d 4039799 c18ea75` 的測試部分）：行為變更 commit 只把 `Assert.Inconclusive("TODO…")` 骨架換成實作，或新增案例；**T2、T3、T4 已有的快照案例沒有被修改**，符合工作計畫「只有 B1、B6、B7 對應的案例可調整」。

---

## 3. 清單外差異

### 3.0 檢查方法

1. 對 15 個產品檔逐 hunk 讀完 `git diff glueDevelop...HEAD`，每個 hunk 填入 §2 的分類欄；填不進 B1～B8、死碼、結構、新增元件的列入本節。
2. 被刪除的函式、元素 ID、API 以 G1 確認沒有其他呼叫點；`AssignFlag` 的 `P`/`W`/`R` 以 G2 與 Android 程式搜尋確認寫入來源。
3. 設定化的段落（Release、RegisterLot、前端區域清單）以一廠 HEAD 的 `appsettings.json` 代入，逐項確認結果與 base 寫死的值相同。
4. 行為變更 commit 的測試 diff 確認既有快照案例沒有被修改（§2.11）。

### 3.1 已記在工作計畫、但 spec B 表沒有寫出的附帶效果（歸入對應 B 項或死碼）

| # | 位置 | 差異 | 歸類 | 風險 |
|---|------|------|------|------|
| 1 | `cPair.MainProcess` | 每輪呼叫 `GenerateoRequireByoNeed()` 從 4 次減為 1 次 | B4（T7 紀錄「每輪多餘的 3 次」） | 低。`GenerateoRequireByoNeed` 每次都一次處理所有 AssignFlag 為空的 oNeed；多出來的 3 次只會處理被刪除流程剛產生的 oNeed，或在同一輪幾毫秒內由網頁新寫入的 oNeed。刪除後，後者最晚延到下一輪（間隔 50 ms 加上一輪的執行時間）才處理 |
| 2 | `DispatchController.InsertoNeed` | 請求沒有 `Area`/`btnName`/`Status` 欄位時，原本拋 `KeyNotFoundException`（500），現在正常寫入 | B4（T9 紀錄，有測試 `InsertoNeed_RequestWithoutLegacyFields_InsertsNeed`） | 低。目前唯一呼叫者是 `Dispatch.js` |
| 3 | `cPair.ProcessSingleoNeed` | Log 文字從「05.處理平板配對／05.處理系統配對」統一為「05.處理配對」 | B5（T14 紀錄） | 低。若現場有依 log 文字做監控，需要同步調整 |
| 4 | `cPair.Initial` | 啟動 log 少一行「02.載入常用資料 >> oPairWay」 | 死碼（T6 紀錄） | 無 |
| 5 | `cTest/Form1` | 按 STOP 後視窗不再拉高到 199 | 死碼（T8 紀錄、已截圖確認） | 無 |
| 6 | `DispatchController.Release` | 無空位訊息改由 `AreaNames` 組成 | 新增元件（T12 紀錄） | 無。一廠三條路線的訊息與原字串相同；未設定 `AreaNames` 的區域只會顯示「X區」 |

### 3.2 清單外觀察（共 4 項，皆不改變一廠派送結果）

1. **WarSituation 頁 Console 多出 SignalR Warning**（`Hitchhike.js` 刪除，T10）
   伺服器端 `Services/Service.cs:109` 仍在 oMission 變動時廣播 `SendMissionChange`。原本只有 `Hitchhike.js` 訂閱這個事件，刪除後 `wwwroot/js` 沒有任何訂閱者（Grep `SendMissionChange` 只剩 `Service.cs`）。`hub.js` 沒有設定 logging，SignalR 用預設的 `ConsoleLogger(LogLevel.Information)`（`common/signalr.js:409`），收到沒人接的事件時會記一筆 Warning「No client method with the name 'sendmissionchange' found.」（`signalr.js:1745`）。
   - 影響：只是 Console 的 Warning，不是錯誤，功能不受影響。T10 回歸時檢查的是「Console 無錯誤」，所以不衝突。派送頁本來就沒有訂閱這個事件，那頁的 Warning 在 base 就已經存在。
   - 建議：視需要另開小任務，選擇移除 `Service.cs` 的 `SendMissionChange` 廣播與對應的 `RegisterDependency`，或在 `hub.js` 設定 `configureLogging(signalR.LogLevel.Error)`。本分支可以不處理。
2. **物料視窗不再把工單內的 `^VCUT` 字樣去掉才顯示**（`Dispatch.js` §2.7 #15、#21，T10 refactor commit）
   base 會把 `^VCUT^DONE`、`^VCUT` 從顯示文字與修改預填值中去掉，現在原樣顯示與帶入。這是 B6（後端不再增刪標記）的前端對應，但 B 表只寫了後端。
   - 影響：一廠沒有程式會寫入 `^VCUT`（T 區不存在、M 區的勾選欄一直隱藏，見第 4 點；HEAD 全專案 Grep `\^(NG|RETURN|VCUT|DONE)` 已沒有寫入端），只有作業員掃到的條碼本身含這段字串時才看得到差異。
   - 建議：在 spec B6 補一句「前端顯示也不再過濾」。
3. **Android 平板寫入的 `W` / `R` oNeed 不會再被處理**（B4，cPair §2.1 #14、#20）
   `Android/.../dispatch/DispatchActivityNewC.kt:455-460` 在起點為 B 區時，以 AssignFlag `W`（送出）或 `R`（換料）直接寫 oNeed。base 的 `GenerateoNeedDataByMCSAsCtoABD`（`PanelDoB2C=T`）會處理這兩種資料；刪除後 cPair 只處理 AssignFlag 為空（`GenerateoRequireByoNeed`）或 `E/X/C`（`RecyclingoNeedByAssignFlag`）的資料，**`W` / `R` 會永遠留在 oNeed**，對應的 B→C 搬運也不會產生。（派送頁 `GetoNeed` 雖然回傳全部 oNeed，但前端 `beginSations` 只有賦值、沒有使用，所以不影響起點清單。）
   - 影響：只有一廠真的使用 Android 的 NewC 畫面時才會發生。Android 不在本計畫範圍（spec「不包含」），使用狀況未確認。
   - 建議：在 spec B4 或「不包含」註明這項；若一廠確定不用平板就不需處理；若會用，另開任務讓 Android 改寫空 AssignFlag，或停用該畫面。
4. **spec B7 的前提「一廠無這些區域」不完全正確**（`DispatchController.RegisterLot` §2.3 #12）
   M 區在一廠存在。base 的規則是「M 區且 `isVcutMaterial == "true"` 時附加 `^VCUT`」，但 base 前端兩個分支都會隱藏 `#vcutCheckboxRow`（`Dispatch.js` base :868-876、:979），而且 M 屬於回送區，不會打開物料登記表單，所以送出的值永遠是 `"false"`。**這條路徑本來就走不到，結果不變。**
   - 建議：把 spec B7 的說明改成「T/I 區一廠不存在；M 區的 V-cut 勾選欄一廠一直隱藏」。

### 3.3 指定項目的檢查結果

1. **`Dispatch.js` 除了 B2、B3，有沒有其他前端行為改變**：已逐 hunk 檢查 §2.7 的 26 列。除了 B2（#24）、B3（#6）、B7（#22）以及 B4 的前端對應（#3、#5、#9、#10、#11、#13）之外，其餘都是一廠走不到的死碼，或一廠設定下結果相同的設定化（#1、#2、#17）。唯一可見但不影響派送的差異是 §3.2-2 的 `^VCUT` 顯示。`#1` 的 `lot-manageable` 沒有 CSS 規則、游標本來就是 pointer，畫面不變。`.ConfirmButton` 原本以 `event.target.id` 判斷要開哪個視窗；`#ConfirmButton` 是 `<a>`，裡面只有文字、沒有子元素，所以 base 也一定開派車視窗，結果相同。
   - 補充：T10 是 `refactor` commit，但 #5、#9（C 部分）、#10、#11 與 cshtml 的換料／退料按鈕和視窗，其實是 B4（T9 `feat`）的前端對應。工作計畫 T10 已註明「C 區前端為 T9 B4 後端的前端對應，與其他刪除同一 commit」。一廠派送區域下拉選單沒有 C（`appsettings.json` 的 `Area` 只有 A/B/K/L/M），C 區按鈕在一廠永遠不會出現，所以這個 refactor commit 在一廠沒有可觀察的行為變更。對 AC-4「每個 commit 單一目的」來說，這是一處需要使用者知道的灰色地帶。
2. **`DispatchController` 群組 5、6 改為空清單的影響**：只有在使用者**沒有任何路線權限**（`userRoutes` 為空）時才會走到這個 switch。`areas` 來自 `appsettings.json` 的 `Area` 區段，HEAD 只有 A/B/K/L/M（`appsettings.Development.json` 沒有覆寫 `Area`，已 Grep 確認）。base 的群組 6 篩 C/D、群組 5 篩 F，在這份設定下本來就是空集合，**結果完全相同**；T3 快照測試（群組 2/5/6/其他）鎖住了這點。另外，群組 5 在 cshtml 不顯示任何按鈕，與 base 只顯示「下料完成」且該按鈕在一廠無法完成操作（需 area F）的實際效果一致。
3. **`InsertoNeed` 不再讀 `assignFlag` 的影響**：base 只在 `area == "C"` 時才會設 `W`（ConfirmButton）或 `R`（ChangeButton），其他情況都是 `""`。一廠派送區域下拉沒有 C（同上），所以網頁送出的 oNeed 在 base 就一定是空的 AssignFlag，**結果相同**。`RejectdButton` 分支需要按下只在 area C 才會顯示的退料按鈕；按鈕預設是可見的，但頁面載入時樓層 change 會觸發區域 change 而把它隱藏，而且派車前的必填驗證需要先選好區域與起訖站，所以在一廠實際上走不到。`W` / `R` 的另一個來源是 Android（§3.2-3）。
4. **cPair 主迴圈移除的步驟是否都屬 B4 或死碼**：是，詳見 §2.1 表後的說明。補充一廠資料下的推演：E→F（無條件執行）需要 F 區空位，一廠沒有 F 區，所以不會動作；F→A/B/D（`UnloadAuto=T`）找 F 區有貨架的站，一廠沒有，所以不會動作；B→C（`PanelDoB2C=F` 時）一廠 C1、C2 啟用，若現場 `PanelDoB2C=F` 會產生 B→C 搬運，刪除後不會再發生（T7 已記錄，使用者同意）；C→A/B/D（`PanelDoB2C=T` 時）只處理 `W` / `R` oNeed（見 §3.2-3）。
5. **`#EndStation focus` 刪除 D/H/Q/R 分支**：這些字母在一廠沒有站點，所以走不到。日後若用這些字母新增區域，派送終點會走 `default`（顯示全部站點），不會被套上二廠的終點規則，符合「新增區域只加資料」的目標。

**結論：清單外差異 4 項（§3.2），都不會改變一廠網頁與 cPair 的派送結果；沒有需要退回修改的程式差異。**

---

## 4. 殘留區域字母 Grep 驗證（HEAD）

### 4.1 使用的 pattern

| # | pattern（ripgrep） | 目的 |
|---|-------------------|------|
| P1 | `(==\|!=)\s*["'][CDEFGHIJNOPQRST]["']\|["'][CDEFGHIJNOPQRST]["']\s*(==\|!=)\|(StartsWith\|startsWith\|charAt\(0\)\s*===?)\s*\(?\s*["'][CDEFGHIJNOPQRST]["']\|case\s+["'][CDEFGHIJNOPQRST]["']\|["']'[CDEFGHIJNOPQRST]'\|filterEndStationOptions\(["'][CDEFGHIJNOPQRST]["']\|autoSelectEndStation\w*\(["'][CDEFGHIJNOPQRST]["']\|\[\s*["'][A-Z]["']\s*,` | 比較、StartsWith、case、SQL `'X'`、終點函式參數、寫死字母陣列 |
| P2 | `["'][CDEFGHIJNOPQRST]["']` | 所有單一字母字串（最寬，避免 P1 漏掉） |
| P3 | `[^A-Za-z][CDEFGHIJNOPQRST]\s?區\|\[[CDEFGHIJNOPQRST]\]\|Block\s*(==\|in)` | 註解與 SQL `Block` 條件 |

字母集合：C、D、E、F、G、H、I、J、N、O、P、Q、R、S、T（spec 的 14 個字母加上 C）。搜尋檔案：`cPair.cs`、`DispatchController.cs`、`CommonController.cs`、`Dispatch.js`、`Views/Dispatch/Index.cshtml`。

### 4.2 命中結果與判定

| 檔案:行 | 內容 | 命中 pattern | 判定 |
|---------|------|-------------|------|
| `cPair.cs:239` | `UpdateoNeedAssignFlag("C", …)` | P2 | 旗標值（AssignFlag C＝取消） |
| `cPair.cs:258` | `UpdateoNeedAssignFlag("E", …)` | P2 | 旗標值（E＝異常） |
| `cPair.cs:335` | `UpdateoRequireAssignFlag(…, "C")` | P2 | 旗標值 |
| `cPair.cs:407` | `OkFlag in('Y','X','C')` | P2 | 旗標值（OkFlag） |
| `cPair.cs:414` | `case "C":`（`OkFlag` switch） | P1/P2 | 旗標值 |
| `cPair.cs:438` | `AssignFlag in('E','X','C')` | P2 | 旗標值 |
| `cPair.cs:443`、`:445` | `case "E":`、`case "C":`（`AssignFlag` switch） | P1/P2 | 旗標值 |
| `cPair.cs:446` | 已註解的 `DeleteoNeedByAssignFlag(…, "E")` | P2 | 註解＋旗標值 |
| `cPair.cs:104`、`:114` | `#region` 標題描述 [C]、[D]、[E] 舊流程 | P3 | 註解（見 §5-4） |
| `cPair.cs:294` | `GetoPort_NoRack_…(string Block) //通常是找A、B、C、D` | P3 未命中，人工確認 | 註解；方法本身以參數查詢，沒有寫死字母 |
| `cPair.cs:301`、`:306` | `Block IN (" + Block + ")`（:306 為註解） | P3 | 參數化區域，沒有寫死字母 |
| `DispatchController.cs:187-188` | `item.AssignFlag == "C"` | P1/P2 | 旗標值（任務列表狀態文字） |
| `DispatchController.cs:268`、`:272` | `SetProperty(…OkFlag, "C")` | P2 | 旗標值 |
| `DispatchController.cs:307`、`:313`、`:331`、`:336`、`:429-430`、`:487-488` | `OkFlag == "R"` | P1/P2 | 旗標值（R＝執行中） |
| `DispatchController.cs:361` | `Reserve = … "N" : "Y"` | P2 | 旗標值（Reserve） |
| `DispatchController.cs:93` | 「群組 5、6 原本對應舊專案的 F 區與 C、D 區…」 | P3 | 註解（本分支新增的說明） |
| `DispatchController.cs:646` | `p.Block == targetArea` | P3 | 依設定的區域，沒有寫死字母 |
| `CommonController.cs:180`、`:207` | `Reserve = … "N" : "Y"` | P2 | 旗標值 |
| `Dispatch.js:270` | `reserve: … \|\| "N"` | P2 | 旗標值 |
| `Dispatch.js:310` | `autoSelectEndStationWithFallback("K", "M", "0", "N")` | P2 | 一廠在用的 A→K/M（spec「不包含」）；`"N"` 是 Reserve 旗標 |
| `Dispatch.js:318` | `autoSelectEndStation("B", "0", "N")` | P2 | 一廠在用的 L→B；`"N"` 是 Reserve 旗標 |
| `Dispatch.js:25` | 「為 M 區和 T 區站點綁定點擊事件」 | P3 | 註解（過時，見 §5-4） |
| `Dispatch.js:729`、`:753` | 「O/P/S/N 區操作：標記空板／Release 回送」 | P3 | 註解（過時，見 §5-4） |
| `Dispatch.js:955` | 「C 區：只顯示 B 區且 HaveFlag = 3 的站點」（`case "B"`） | P3 | 註解（過時；`case "B"` 是一廠在用的 B 區起點邏輯） |
| `Index.cshtml:71` | 「群組 5 原本只有舊專案 F 區的『下料完成』」 | P3 | 註解（本分支新增的說明） |
| `Index.cshtml:191` | `@* 站點物料操作 Modal (M區/T區) *@` | P3 | 註解（過時，見 §5-4） |

`CommonController.cs` 的 P1：0 筆；`Index.cshtml` 的 P1/P2：0 筆；`Dispatch.js` 的 P1：0 筆。

**判定：5 個檔案已沒有以 D/E/F/G/H/I/J/N/O/P/Q/R/S/T 或 C 作為區域代號的程式判斷。** 剩下的命中都是 AssignFlag / OkFlag / Reserve 旗標值、依設定或參數的區域比對，或註解。符合 AC-2 第一項。
（`Dispatch.js` 仍寫死 A/B/K/L/M 的派送起訖規則，例如 `case "A"/"B"/"K"/"L"/"M"`，這是 spec「不包含」的部分，由 AB 棟計畫 T9 處理。）

---

## 5. 已知未處理事項

| # | 項目 | 位置 | 現況 | 建議 |
|---|------|------|------|------|
| 1 | SCP 其他 Controller 的 CS0168（catch 了 `ex` 卻沒有使用） | SCP 其他 Controller，共 16 處（T15 建置警告觀察；本報告未重新建置） | 不是本分支動到的方法；其中可能有空 catch，違反全域「禁止空 catch」規則 | 另開任務逐一補 Warning Log（純補記錄、不改流程，比照 B8），先用 Grep `catch \(Exception ex\)\s*\{\s*\}` 列清單 |
| 2 | `cPair.SafeCol` 的 `catch { return ""; }` | `cPair.cs:181-185` | 本分支沒有動到。它是例外處理流程中取欄位值的保護，回傳空字串本身合理，但沒有記錄 | 改成 `catch (Exception ex) { 記 Warning; return ""; }`，或限定 `ArgumentException`；屬範圍外小修，可併入 #1 |
| 3 | 依工單標記顯示特殊圖示 | `CommonController.cs:316-340`（`GetStationImgSrc`：`^NG`→ng、`^RETURN`→return、`^VCUT^DONE`、`^VCUT`）、`PortController.cs:203-216`（只判斷 `^VCUT^DONE` / `^VCUT`）、`Map.js:80-95`（前端再判斷一次，四種都有） | 三處已讀。這些是依**工單字串**判斷，不是依區域字母，所以不在 AC-2 範圍內。HEAD 全專案已沒有寫入這些標記的程式（Grep `\^(NG\|RETURN\|VCUT\|DONE)` 只剩這三處讀取端與一個測試）。加上 B6 之後 RegisterLot 不再去掉作業員輸入中的這些字樣，若掃到的條碼剛好含 `^NG…` 等片段，地圖會顯示錯誤的圖示。原本「Release 回送」的 `^RETURN` 也是二廠 I 區專用，已在 T9 刪除 | **建議不在本分支處理，另開一個獨立的 `feat` commit（可記為 B9）**，三處一起刪除，改為只依 `TracStatus` 決定圖示。理由：這是行為變更（對含這些字樣的工單，圖示會改變），依「死碼與行為變更分開 commit」不宜混在 T16；而且三處要同時改，否則 `Map.js` 會蓋掉後端的結果。在此之前，風險僅限「條碼含這些字樣」的少見情況，可以接受 |
| 4 | 過時註解 | `cPair.cs:104-111`（`#region Item 0.X` 與 7 行說明，描述 A→B、C→ABD、D→E、E→ABD、B→A、B→C 舊流程）、`cPair.cs:114`（`#region Item 1.X` 標題描述 B→C）、`cPair.cs:294`、`Dispatch.js:25`、`:729`、`:753`、`:955`、`Index.cshtml:191` | 只是註解，不影響行為 | 併入 T17 文件同步，或另開一個 `docs`/`refactor` 純註解 commit；`cPair.cs:104-111` 可以整段刪除（只有 #region 標題與註解，沒有程式碼），`:114` 標題改為描述 oNeed→oRequire 配對 |
| 5 | Android 平板 `W` / `R` oNeed | `DispatchActivityNewC.kt:455-460` | 見 §3.2-3 | 先確認一廠是否使用平板，再決定是否另案處理 |
| 6 | `SendMissionChange` 沒有接收端 | `Services/Service.cs:105-109` | 見 §3.2-1 | 可選擇性處理 |
| 7 | 工作計畫內的待辦 | T16：使用者於測試環境重跑 T5 手動驗證清單；T17：`Markdown/1廠完整派送邏輯分析.md` 與 AB 棟計畫文件同步 | 未完成 | 依工作計畫進行 |
