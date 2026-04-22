# 技術方案書

## 選定方案：方案 B（裁剪版）

**一句話**：字串修正 5 處 + 樓層頁籤改為從 `AllowedFloors` 動態產生，不抽 SiteCode 設定、不抽共用 Service。

## 架構設計

### 修正後資料流

```
PortController.Index(string floor)
  ├─ GetUserAllowedFloors()                    ← 複製自 CommonController
  │   ├─ 讀 appsettings.json `AgvSetting` 取得可用樓層 (["FHT1-1F", "FHT1-3F"])
  │   └─ 再依使用者路線權限 (pUserRoute + pRoute) 過濾
  ├─ ViewBag.AllowedFloors = [...]             ← 頁籤來源
  ├─ ViewBag.FloorDisplayNames = {...}         ← 頁籤顯示名稱 (如 "1F" / "3F")
  ├─ ViewBag.CurrentFloor = floor
  ├─ ViewBag.MapImage = $"/img/FHT1-{floor}.png"   ← FHT2→FHT1
  └─ area = $"FHT1-{floor}"                    ← 座標轉換 key
```

### 頁籤渲染改造（Views/Port/Index.cshtml）

**改造前**：
```razor
var floors = new[] { "1F", "2F", "3F", "4F" };  // 硬寫
```

**改造後**（對齊 `_MapPartial.cshtml` 的做法）：
```razor
@foreach (var area in (List<string>)ViewBag.AllowedFloors)    // 例如 ["FHT1-1F", "FHT1-3F"]
{
    var floor = area.Replace("FHT1-", "");                     // "1F" / "3F"
    var displayName = ViewBag.FloorDisplayNames[area] ?? floor;
    var isActive = ViewBag.CurrentFloor == floor;
    <a href="/Port?floor=@floor" class="nav-link @(isActive ? "active" : "")">@displayName</a>
}
```

### `floor` 參數容錯

若 query string 傳進來的 `floor` 對應的 `FHT1-{floor}` 不在 `AllowedFloors` 內，改用 `AllowedFloors` 第一個樓層（比照 `CommonController.ShowMap` L35-38）。

## 方案分析（六面向）

1. **方案概述**：保守修正 — 限縮改動到「剛好解決客戶問題 + 避免 2F/4F 頁籤造成新 bug」。
2. **架構設計**：字串修正不改架構；動態頁籤複用 `CommonController` 已有的 `AllowedFloors`/`FloorDisplayNames` 機制，與 `_MapPartial` 行為一致。
3. **優點**：
   - 解決客戶回報問題
   - 消除 2F/4F 頁籤破圖的潛在新 bug（一廠無對應圖檔）
   - 與全站其他頁面（`_MapPartial.cshtml`）行為一致
4. **風險與缺點**：
   - `GetUserAllowedFloors` 兩份同源邏輯（`CommonController` + `PortController`）
   - 短期可接受但需 TODO 註解追蹤
5. **技術債評估**：
   - 新增一筆「日後抽 `FloorService` 共用」的技術債
   - 風險低（邏輯穩定、不常變）
   - 建議在第三個頁面需要此機制時一併抽出
6. **建議適用情境**：客戶明確 bug、範圍清晰、快速交付不擴大重構。

## 被捨棄的方案

### 方案 A（救急版）

**內容**：只改 5 處 FHT2→FHT1 字串，不動頁籤。

**捨棄原因**：`Port/Index.cshtml` 硬寫 1F~4F 四個頁籤，一廠沒有 2F/4F 圖檔，使用者點進去會破圖，客戶會再回報一次。

### 方案 C（根治版）

**內容**：方案 B + 抽 `FHT1` 成 appsettings `SiteCode` 設定值 + 抽 `FloorService` 共用。

**捨棄原因**：邊際效益低（下次複製新廠不一定只差 SiteCode，可能還有其他字串殘留），且超出本次 bug 修復範圍。
