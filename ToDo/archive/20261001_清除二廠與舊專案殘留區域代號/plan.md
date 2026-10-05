# 技術方案書 — 清除二廠與舊專案殘留的區域代號程式碼

> 建立日期：2026-10-01｜規格：`spec.md`｜任務清單：`工作計畫.md`

## 選定方案概述

**方案一：區域規則放 `appsettings.json`。** 分三階段進行：先以測試記錄一廠現有行為 → 分批刪除殘留（結果不變）→ 把寫死的字母清單換成讀設定（行為變更，獨立 commit）。

## 架構設計

### 新增設定 `AreaRules`（`WebGui/SCP/appsettings.json`）

```json
"AreaRules": {
  "RegisterAreas": [ "A", "L" ],
  "RackIdRequiredAreas": [ "K", "L" ],
  "ReleaseRoutes": {
    "K": [ "L" ],
    "M": [ "A" ],
    "B": [ "A" ]
  }
}
```

| 設定 | 取代的寫死邏輯 | 一廠設定值的由來 |
|------|---------------|-----------------|
| `RegisterAreas` | `Dispatch.js` 中「不在 releaseAreas 的 validAreas」 | 現行可做物料登記的是 A、L |
| `ReleaseRoutes` 的鍵 | `Dispatch.js:891` `releaseAreas` | 現行一廠回送區是 B、K、M |
| `ReleaseRoutes` 的值 | `DispatchController.Release` 的 if-else（`:705-846`） | K→L、M→A、B→A；值為陣列，依序找空位 |
| `RackIdRequiredAreas` | `DispatchController.cs:442` | 一廠在用的是 K、L |
| 可點選區域 | `Dispatch.js:57` `validAreas` | = `RegisterAreas` ∪ `ReleaseRoutes` 的鍵 |

### 元件

```
appsettings.json ─ AreaRules
        │
        ▼
AreaRuleProvider（新增，SCP/Helpers）
  ├─ IsRegisterArea(area) / IsReleaseArea(area) / IsRackIdRequired(area)
  ├─ GetReleaseTargets(area) → 目的區清單（依序）
  └─ ClickableAreas → 登記區 ∪ 回送區
        │
        ├─► DispatchController.Release      依目的區清單找空位；無規則回 400
        ├─► DispatchController.RegisterLot  貨架條碼必填判斷
        └─► DispatchController.Index        ViewBag.AreaRules → window.areaRules
                                                    │
                                                    ▼
                                            Dispatch.js 以 window.areaRules
                                            取代 validAreas / releaseAreas

cPair.ProcessSingleoNeed
  移除字母 switch → 一律進 ProcessoNeedToRequire
  （既有 CheckoPortBgnToEndIsNullAndUseFlagAsY 已要求起訖站存在、啟用、未註冊）
```

- `AreaRuleProvider` 放 `SCP/Helpers/`，比照既有 `ChargingStationProvider`、`MapCoordinateConverter` 的做法（讀 `IConfiguration`，純邏輯，可單元測試）。
- Release 找空位的查詢條件與排序沿用現行寫法：`HaveFlag == "0"`、`BgnToEnd` 為空、`UseFlag == "Y"`、排除 pending 終點、`OrderByDescending(Priority).ThenBy(Port)`。
- `cPair` 的 Log 文字原本分「05.處理平板配對」與「05.處理系統配對」，移除字母清單後統一為「05.處理配對」。

### 測試策略

| 對象 | 做法 | 是否連資料庫 |
|------|------|-------------|
| SCP `DispatchController` | `SCP.Tests` 加入 `Microsoft.EntityFrameworkCore.Sqlite`，以 SQLite 記憶體資料庫建立 `agvDB_1400004Context`，直接呼叫 Controller action | 否 |
| SCP `AreaRuleProvider` | 純單元測試，`ConfigurationBuilder.AddInMemoryCollection`（比照 `ChargingStationProviderTests`） | 否 |
| `cPair` | 沿用 `svrPairTests/IntegrationTestBase`（哨兵站號 + `TestCleanup` 清除） | 是，僅本機測試庫 `agvDB_1400004_1`，每次執行前告知 |
| `Dispatch.js` / 頁面 | 手動驗證清單，清理前後各執行一次 | 由使用者於測試環境操作 |

**SQLite 相容性為 T2 的第一個檢查點。** 若 `agvDB_1400004Context` 的模型或 `ExecuteSqlRaw` / `ExecuteUpdate` 在 SQLite 上無法運作，退回備案：先以純結構搬移把判斷邏輯抽成純函式（獨立 commit、不改邏輯），再對純函式寫快照測試。

### 執行順序的理由

1. **P0 先測試**：重構安全規則要求先有現有行為的測試。
2. **P1 再刪除**：只刪一廠資料下走不到的分支；P0 的測試在每一批刪除後都必須全過。
3. **P2 最後改資料驅動**：這是行為變更，放在刪除之後，diff 才乾淨、容易審查，也能單獨退回。

### Commit 切分

| 類型 | 內容 | 前綴 |
|------|------|------|
| 測試 | 快照測試、測試基礎建設 | `test(scp):`、`test(dispatch):` |
| 純刪除 | 未呼叫函式、一廠走不到的分支、對應的頁面元素 | `refactor(scp):`、`refactor(dispatch):` |
| 行為變更 | B1～B7 各項 | `feat(scp):`、`feat(dispatch):` |
| 補 Log | B8 空 catch | `fix(scp):`、`fix(dispatch):` |
| 文件 | 計畫、驗證清單、報告 | `docs(todo):`、`docs:` |

## 方案分析

### 方案一：區域規則放 `appsettings.json`（採用）

1. **概述**：新增 `AreaRules` 設定區段，網頁前後端都透過 `AreaRuleProvider` 查規則；`cPair` 不看字母。
2. **架構**：見上方「架構設計」。
3. **優點**：不依賴現場資料庫內容；可完全以單元測試與記憶體資料庫驗證；與 AB 棟計畫的自動派送規則（`AutoDispatch:Routes` 亦放 `appsettings`）一致；上線只需換程式與設定檔。
4. **風險與缺點**：回送規則與 `pRoute` 路線表各記一份，新增回送路線時兩邊都要改；設定檔寫錯會直接影響回送目的地。
5. **技術債**：中。兩處設定需同步，但都是資料不是程式；日後可把 `AreaRuleProvider` 的來源換成 `pRoute` 而不動呼叫端。
6. **適用情境**：現場資料無法先驗證、希望改動風險最小。

### 方案二：區域規則取自路線表 `pRoute`（捨棄）

1. **概述**：以 `pRoute` 中 `DispatchMode = RELEASE` 路線的 `SourceAreas` / `TargetAreas` 決定回送區與目的區。
2. **優點**：單一來源；已有路線管理畫面可維護。
3. **捨棄原因**：現場 `pRoute` 內容未驗證。`Deploy_pRoute_Factory1.sql` 中清除二廠路線的語句是註解掉的，`Deploy_pUserRoute.sql` 也仍是二廠路線清單，現場很可能同時存在二廠的 `ROUTE_4F_2F`（K→H，RELEASE）與一廠的 `ROUTE_F1_K_RELEASE`（K→L），同一起點區會有兩個目的區而產生衝突。採用前必須先連現場資料庫確認並清資料，風險與前置成本都高於方案一。

### 保守做法：只刪除、字母清單維持寫死（捨棄）

- Release 的三條一廠路線維持 if-else，字母清單縮為 A / B / C / K / L / M。
- **捨棄原因**：既有邏輯完全不動，但 AB 棟新增區域時仍須回頭改這些清單，沒有解決「之後加庫位還要改程式」的問題。使用者於 2026-10-01 決定採方案一。

## 已知風險

| 風險 | 因應 |
|------|------|
| 現場 `Recipe.ini` 的 `PanelDoB2C` 若不是 `T`，B→C 自動配對實際上有在運作 | T7 開工前由使用者確認；未確認前 T7 標阻塞 |
| SQLite 與 SQL Server 行為差異使快照測試失真 | T2 先做相容性檢查；不相容則走備案（抽純函式） |
| `Hitchhike.js` 掛在首頁（`WarSituation.cshtml`）與 `_Layout.cshtml` 的確認視窗 | T10 做影響範圍掃描，連同視窗標記與 API 一併移除，並手動確認首頁載入正常 |
| `cTest/Form1.Designer.cs` 為 WinForms 設計檔，手動刪按鈕容易破壞版面配置 | T8 同步調整 `TableLayoutPanel` 配置，建置後啟動程式目視確認 |
| 舊群組篩選（群組 2 / 5 / 6）若直接刪除，群組 5、6 的帳號會從「無可派送區域」變成「全部區域」 | T9 保留結果：群組 2 → A 區；群組 5、6 → 空清單（不引用任何字母） |
