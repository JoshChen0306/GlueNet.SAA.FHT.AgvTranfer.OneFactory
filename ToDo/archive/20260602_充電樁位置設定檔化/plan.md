# 技術方案書：充電樁位置設定檔化

## 選定方案：A — 抽出可測試的 helper

把「讀設定 → 解析 → swapXY → 換算」抽成獨立、可單元測試的純邏輯元件，Controller 只負責呼叫與塞 ViewBag。

## 架構設計

```
appsettings.json (ChargingStations:{area})
        │
        ▼
ChargingStationProvider.Get(IConfiguration, area)  ← 新增，純邏輯
        │  ├─ 解析 "X,Y,角度"
        │  ├─ swapXY（讀 AgvSetting:{area}:swapXY，與 GetTrac 一致）
        │  └─ MapCoordinateConverter.ConvertX/ConvertY  ← 由 CommonController 抽出
        ▼
List<Position> (Left, Bottom, Transform)
        │
        ▼
CommonController.ShowMap → ViewBag.ChargingStations
        │
        ▼
_MapPartial.cshtml  →  @foreach 迴圈渲染（取代寫死區塊）
```

**資料流向**：設定檔（唯一資料來源，非 DB）→ Provider 純函式 → ViewBag → View 迴圈。與庫位差別僅在「來源是 appsettings 而非 oPort」，換算與 swapXY 完全共用。

## 方案分析（六面向）

1. **方案概述**：抽出 `MapCoordinateConverter`（共用換算）+ `ChargingStationProvider`（充電樁專屬讀取/解析），Controller 瘦身為呼叫端。
2. **架構設計**：見上圖。`MapCoordinateConverter` 為靜態工具（吃 `IConfiguration` + area + 座標值）；`ChargingStationProvider` 為靜態/可注入元件（吃 `IConfiguration` + area）。
3. **優點**：
   - `swapXY`（3F 唯一真風險）可用單元測試鎖定，擋住靜默錯位。
   - 換算邏輯單一來源，庫位/車/充電樁共用，避免複製。
   - Controller 維持瘦身，職責清楚。
4. **風險與缺點**：
   - 需把 `ConvertX/ConvertY` 由 private 提為可共用 → 動到既有結構（純搬移，零邏輯變更，須先補測試）。
   - 需新建 `SCP.Tests` 測試專案（目前無），會改動 `SCP.sln`。
5. **技術債評估**：低。未來新增樓層、改成狀態燈、或讓庫位也走同一條讀取流程都易擴充。
6. **建議適用情境**：需要驗收「3F 對齊」且要回歸保護的情況——即本需求。

## 被捨棄的方案

### 方案 B：Controller 內加 private `GetChargingStations`
- 與 `GetTrac/GetAgv` 同款 private 風格、改動最少、不動 `ConvertX/ConvertY` 可見性。
- **捨棄原因**：方法 private + 依賴 Controller 的 DI（DbContext），`swapXY` 正確性無法單獨測試，只能手動驗證。本需求選了走 spec 驗收流程，需要可測性，故採 A。

## 重構安全（針對子任務 1）

依 CLAUDE.md 重構規則：
1. 先對 `ConvertX/ConvertY` 現有行為補測試（鎖定 minX/maxX/minY 邊界與一般值）。
2. 測試綠燈後，純搬移到 `MapCoordinateConverter`，`CommonController` 改呼叫之，**不改任何數值邏輯**。
3. 搬移 commit 與功能新增 commit 分開。
4. 重構後重跑測試，結果須與重構前完全一致。

## 測試專案

- 目前無測試專案 → 自動建立 `SCP.Tests`（net6.0、MSTest + Moq、加 `SCP` ProjectReference、`dotnet sln add` 進 `SCP.sln`）。
- 測試以 in-memory `IConfiguration`（`ConfigurationBuilder.AddInMemoryCollection`）餵入 `AgvSetting` + `ChargingStations`，不需 DB。
