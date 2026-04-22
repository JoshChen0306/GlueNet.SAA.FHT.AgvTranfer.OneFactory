# SCP 儲架設定頁面顯示二廠地圖 - 修正規格書

## 主題與背景

客戶反映 `WebGui/SCP` 專案的「儲架設定」（`Port/Index`）頁面顯示的是二廠（SecondFactory）地圖，應顯示一廠（OneFactory）地圖。

**根因**：`PortController.cs` 從二廠專案複製過來後 `FHT2-` 字串未替換為 `FHT1-`。全專案 `WebGui/SCP` 範圍內共 **5 處** FHT2 殘留（Controller / View / JS），另外 `Port/Index.cshtml` 樓層頁籤寫死 1F~4F，與一廠實際支援範圍（僅 1F / 3F）不一致。

## 需求範圍

### In Scope

1. 修正 `WebGui/SCP` 全範圍 5 處 FHT2 殘留
2. 改造 `Port/Index.cshtml` 樓層頁籤為動態產生（依 `ViewBag.AllowedFloors`）

### Out of Scope

- 抽出 `FHT1` SiteCode 成 appsettings 設定值（邊際效益低，留待未來有第三廠時再處理）
- 修改 `appsettings.json`（`AgvSetting` 現有設定已符合需求）
- `Dispatch/` 與 `ACC/` 專案（本次 bug 不涉及車隊調度/RCS 介接層）
- `_MapPartial.cshtml:31` 充電站硬寫座標（本次只修字串，座標是否適用於一廠地圖由客戶驗收時判斷）

## 限制條件

- 技術限制：`GetUserAllowedFloors()` 目前是 `CommonController` 的 private 方法，`PortController` 無法直接呼叫。本次採保守策略：在 `PortController` 複製同一套邏輯並加 TODO 註解，等未來第三個頁面需要時再抽成共用 Service。
- 一廠實際支援樓層：1F 與 3F（對應 `FHT1-1F` / `FHT1-3F`），不存在 2F / 4F。
- `appsettings.json` 的 `AgvSetting` 已定義 `FHT1-1F`、`FHT1-3F` 兩個樓層，剛好符合需求。

## 驗收標準

- [ ] `Port` 頁面地圖圖片路徑為 `/img/FHT1-1F.png` 或 `/img/FHT1-3F.png`，不再出現 `FHT2-`
- [ ] `Port` 頁面樓層頁籤只顯示使用者 `AllowedFloors` 範圍內的樓層（一廠預期為 1F、3F 兩個）
- [ ] 點擊 1F / 3F 頁籤後地圖正確切換、站點位置正確（座標轉換 `area` 使用 `FHT1-xF`）
- [ ] 一廠 1F 地圖的充電站圖示正常顯示（`_MapPartial.cshtml` 的 `FHT2-1F` 條件改為 `FHT1-1F` 後能進入判斷分支）
- [ ] Dispatch 派送頁 `refreshMap` 的降級預設值為 `FHT1-1F`
- [ ] 全專案 `WebGui/SCP` 範圍內 `grep "FHT2"` 結果為 0 行
- [ ] `SCP.csproj` build 0 error；現有 SCP 相關測試全部通過
- [ ] `git diff` 逐項比對：所有變更皆為預期內的 FHT2→FHT1 或頁籤動態化改造

## 風險

| # | 風險 | 處理策略 |
|---|------|---------|
| 1 | `_MapPartial.cshtml:31` 充電站硬寫位置 `top:64%; left:12%` 是否適用於 FHT1-1F 地圖？ | 先完成本次修正，由客戶驗收時確認；若位置不對另開任務追蹤（或考慮移到 appsettings） |
| 2 | `PortController.Index` 的 `floorBlocks` 字典（L22-28）列出 1F~4F 的 Block 清單 | 保留不動；頁籤動態化後不會有 2F/4F 被點到，字典是否清掉不影響行為；保留以利未來擴充 |
| 3 | `GetUserAllowedFloors` 邏輯複製到 `PortController` 造成兩份同源程式碼 | 加 TODO 註解標記技術債，等第三個頁面需要時再抽共用 |
