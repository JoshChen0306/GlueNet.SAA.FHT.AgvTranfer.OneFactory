# 需求規格書 — 清除二廠與舊專案殘留的區域代號程式碼

> 建立日期：2026-10-01
> 分支：`feature/cleanup-legacy-area-code`（由 `glueDevelop` 開出）
> 關聯計畫：`ToDo/20260726_一廠AB棟1F新路線與邏輯`（本計畫為其前置）

## 主題與背景

一廠程式以 **StationNo 的第一個字母**判斷站點屬於哪一區（`cPair.cs:203`、`DispatchController.cs:435,638`、`Dispatch.js:58`）。程式裡留有 2023 舊專案與二廠移植的區域判斷，這些區域在一廠資料庫不存在，所以目前不會執行；但日後新增庫位只要用到這些字母，就會被套上殘留規則（例如自動派車、強制必填、送往錯誤樓層）。

使用者要求：**先清除殘留，再新增庫位**，並讓之後新增區域只需要加資料、不必改程式。

### 現況依據（2026-10-01）

- 一廠 `oPort` 共 27 站，只有 A / B / C / K / L / M 六區（使用者提供之資料表截圖）。
  啟用中：A1、A2、B1、C1、C2、K1～K4、L1～L4、M1～M3。
- 一廠六條路線只用到 A / B / K / L / M（`Deploy_pRoute_Factory1.sql`、`Markdown/1廠完整派送邏輯分析.md`）。
- C1、C2 有資料且啟用，但沒有任何一廠路線使用；相關程式碼為 2023 舊專案的生產區流程。
- 已完整讀過：`cPair.cs`、`DispatchController.cs`、`Dispatch.js`、`AutoDispatchService.cs`。
  其餘 SCP Controller / js / Views、ACC 以搜尋確認；Android 未完整讀過。

## 需求範圍

### 包含

1. **刪除殘留區域判斷**：D、E、F、G、H、I、J、N、O、P、Q、R、S、T 共 14 個字母，以及 C 區的舊專案流程（B→C 自動配對、C 區換料／退料）。處理方式為直接刪除，不註解。
2. **刪除未被呼叫的函式與壞掉的功能**：`cPair` 內 5 個未呼叫函式、`Dispatch.js` 內 2 個未呼叫函式、掃描機台功能（呼叫不存在的 `filterBeginStationByDestination`）。
3. **字母清單改為依資料判斷**（方案一，規則放 `appsettings.json`）：
   - `cPair` 不看字母，只要起訖站在 `oPort` 存在且啟用就處理。
   - 地圖可操作區域、可 Release 區域、Release 目的區、貨架條碼必填區域，改由 `AreaRules` 設定決定。
4. **補上被動到方法內的空 catch 記錄**（全域規則：禁止空 catch）。

### 不包含

- Android 平板程式（一廠是否使用未確認，另案處理）。
- 一般派送的終點自動選擇（A→K/M、L→B）改為資料驅動 —— 維持現狀，由 AB 棟計畫 T9 處理。
- `AutoDispatchService`（M→K）—— 由 AB 棟計畫 T6 重構。
- C1、C2 的資料（`oPort` 內容不動）。
- 現場資料庫的 `pRoute` / `pUserRoute` 二廠殘留路線資料清理。
- 根目錄的二廠部署腳本與實作計畫文件（`Deploy_pRoute.sql`、`2F*.md`、`4F*.md` 等）。
- 版本號變更與現場部署。

## 限制條件

- **重構安全規則**：先補一廠現有行為的測試，測試全過才開始刪除；「刪除死碼」與「行為變更」必須分開 commit。
- **不可擅自連線**：SCP 測試一律使用記憶體資料庫；`cPair` 整合測試僅可連本機測試庫 `agvDB_1400004_1`（127.0.0.1，測試基底已有白名單防呆），每次執行前先告知使用者。不連現場資料庫、PLC、RCS。
- **檔案編碼**：修改 `.cs` / `.cshtml` 前先確認 BOM。已確認帶 UTF-8 BOM：`cPair.cs`、`DispatchController.cs`、`CommonController.cs`、`Dispatch.js`、`Dispatch/Index.cshtml`、`cTest/Form1.cs`、`cTest/Form1.Designer.cs`。`appsettings.json` 無 BOM（JSON 慣例），修改前需確認為 UTF-8。
- **T7 前置確認**：刪除 `cPair` 舊自動配對流程前，使用者需確認現場 `Recipe.ini` 的 `PanelDoB2C`、`UnloadAuto` 值（repo 內皆為 `T`）。
- `Dispatch.js` 沒有前端測試框架，以手動驗證清單把關。

## 資料流

本計畫不涉及 PLC、感測器或外部 API，只改程式碼與設定檔。

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 讀取區域規則 | `appsettings.json` 的 `AreaRules` | `IConfiguration` 於啟動時綁定 | 區段缺漏 → 視為空規則並記 Warning Log，不拋例外 |
| Release 找目的站 | DB `oPort`（依設定的目的區） | EF Core 查詢（沿用現行條件與排序） | 無空位 → 回 400 與訊息（沿用現行） |
| Release 區域未設定規則 | `AreaRules.ReleaseRoutes` | 記憶體查表 | 回 400「此區域未設定回送路線」（新行為） |
| `cPair` 判斷是否處理 oNeed | DB `oPort`（起訖站皆啟用且未註冊） | 既有 `CheckoPortBgnToEndIsNullAndUseFlagAsY` | 不符合 → oNeed 標 `E`（沿用現行） |

## 行為變更清單（有意為之）

以下為改完後結果與現在不同之處，皆為一廠正常操作走不到的路徑：

| # | 項目 | 現在 | 改完 |
|---|------|------|------|
| B1 | 未設定回送路線的區域做 Release | 走二廠規則依序找 M → Q → R 區空位 | 回 400「此區域未設定回送路線」 |
| B2 | L 區派送起點清單 | 實際執行二廠版：只列 L1～L4，排除含 `^RETURN` / `^NG` 的物料 | 一廠版：列出所有有料的 L 站 |
| B3 | B 區當派送起點時的終點清單 | 列出 C 區站點（舊專案 B→C） | 不再列出 |
| B4 | `cPair` 舊自動配對（B→C、C→A/B/D、F→A/B/D、E→F） | 每輪執行查詢，一廠無對應資料故無動作 | 刪除 |
| B5 | `cPair` 遇到清單外字母的 oNeed | 略過不處理，oNeed 永久殘留 | 起訖站存在且啟用就處理，否則標 `E` |
| B6 | 物料登記時移除工單內的 `^VCUT`、`^DONE`、`^NG`、`^RETURN` 字樣 | 會移除 | 不再移除（一廠不使用這些標記） |
| B7 | R 區工單選填、J/H/I 區貨架條碼必填、T/M/I 區自動加標記 | 存在 | 刪除（一廠無這些區域） |
| B8 | `DispatchController` 空 catch | 靜默吞掉例外 | 記 Warning Log，流程不變 |

## 驗收標準

### AC-1 一廠現有行為不變（快照測試，🧪）

- [ ] Given K 區站點為空板且 L 區有空位，When 呼叫 Release，Then 建立 oNeed，終點為 L 區 Priority 最高、同 Priority 取 Port 最小之空位
- [ ] Given M 區站點為空板且 A 區有空位，When 呼叫 Release，Then 終點為 A 區空位
- [ ] Given B 區站點為空板且 A 區有空位，When 呼叫 Release，Then 終點為 A 區空位
- [ ] Given 目的區無空位，When 呼叫 Release，Then 回 400 且不建立 oNeed
- [ ] Given 站點 HaveFlag 不為 1，When 呼叫 Release，Then 回 400「請先標記為空板」
- [ ] Given 使用者有路線權限但無該區 RELEASE 權限，When 呼叫 Release，Then 回 403
- [ ] Given A 區空架站點與工單，When 呼叫 RegisterLot，Then HaveFlag=3 且寫入 WorkOrder / RackId / PutTime
- [ ] Given K 或 L 區站點且 RackId 為空，When 呼叫 RegisterLot，Then 回 400「請輸入貨架條碼」
- [ ] Given RackId 為 `-1`（隱藏欄位哨兵值），When 呼叫 RegisterLot，Then 通過驗證
- [ ] Given 派車請求 WorkOrder 為空，When 呼叫 InsertoNeed，Then 以起點站 oPort 的 WorkOrder 寫入 oNeed
- [ ] Given 起點為 A / B / K / L / M 任一區且起訖站皆啟用未註冊的 oNeed，When `cPair.GenerateoRequireByoNeed()`，Then 產生 oRequire、oNeed 標 `Y`、兩站 BgnToEnd 註冊
- [ ] Given 終點站停用，When `cPair.GenerateoRequireByoNeed()`，Then oNeed 標 `E`
- [ ] Given A 區起點且工單空白，When `cPair.GenerateoRequireByoNeed()`，Then oNeed 標 `C`

### AC-2 殘留已清除

- [ ] `cPair.cs`、`DispatchController.cs`、`CommonController.cs`、`Dispatch.js`、`Dispatch/Index.cshtml` 內不再有以 D / E / F / G / H / I / J / N / O / P / Q / R / S / T 作為區域代號的判斷（以 Grep 驗證，結果附於報告）
- [ ] C 區舊流程已刪除：`GenerateoNeedDataByMCSAsBtoC`、`GenerateoNeedDataByMCSAsCtoABD`、area C 的 `W` / `R` AssignFlag、`RejectdButton`、`ChangeButton`
- [ ] 未被呼叫的函式已刪除（`cPair` 5 個、`Dispatch.js` 2 個），且以 Grep 確認全專案無其他呼叫點
- [ ] 掃描機台功能、V-cut 欄位、下料完成按鈕、Hitchhike（G→J）已自頁面與程式移除，頁面載入無 JavaScript 錯誤
- [ ] FleetManager 畫面的 D 區／E 區啟用停用按鈕已移除，程式可正常啟動

### AC-3 依資料判斷（🧪）

- [ ] Given `AreaRules.ReleaseRoutes` 設定 `K→L`、`M→A`、`B→A`，When 查詢 K 的回送目的區，Then 回傳 `["L"]`
- [ ] Given 未設定回送路線的區域，When 呼叫 Release，Then 回 400「此區域未設定回送路線」且不建立 oNeed
- [ ] Given 在設定新增一筆回送路線（測試用假區域），When 呼叫 Release，Then 不改程式即可依設定找到目的站
- [ ] Given `AreaRules` 區段缺漏，When 服務啟動並查詢規則，Then 回傳空規則、記 Warning Log、不拋例外
- [ ] Given 起點為清單外字母但起訖站皆存在且啟用的 oNeed，When `cPair.GenerateoRequireByoNeed()`，Then 正常產生 oRequire
- [ ] Given 起點站不存在於 oPort 的 oNeed，When `cPair.GenerateoRequireByoNeed()`，Then oNeed 標 `E`
- [ ] 地圖上可點選的區域與顯示 Release 按鈕的區域由 `AreaRules` 決定；一廠設定下結果與現在相同（A、L 為登記區；B、K、M 為回送區；C 不可點選）

### AC-4 流程與品質

- [ ] 「刪除死碼」與「行為變更」分屬不同 commit，每個 commit 單一目的
- [ ] 清理前後執行完整測試套件，除有意的行為變更（B1～B8）外結果一致
- [ ] 附上 `git diff` 邏輯比對報告，逐項對應 B1～B8，無清單外的邏輯差異
- [ ] 前端手動驗證清單（六條路線各一次＋地圖點選）清理前後結果一致，由使用者於測試環境確認
- [ ] 被動到的方法內沒有空 catch
