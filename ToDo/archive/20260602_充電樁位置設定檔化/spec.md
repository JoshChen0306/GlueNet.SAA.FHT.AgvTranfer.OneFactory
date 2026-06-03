# 需求規格書：充電樁位置設定檔化

## 主題與背景

SCP 即時地圖的充電樁（充電站）目前寫死在 `WebGui/SCP/Views/Shared/_MapPartial.cshtml:33`：

```html
@if (ViewBag.CurrentArea == "FHT1-1F")
{
    <div class="position-absolute" style="top:64%; left:12%"><img src="/img/ChargingStation-off.png" /></div>
}
```

問題：
- 位置寫死（`top:64% left:12%`），不是座標驅動 → 不會隨地圖校正（appsettings 的 `AgvSetting`）連動，改校正後就對不上。
- 只有 1F、只有 1 個（樓層與數量都寫死）。
- 現場需要能自行調整充電樁位置，且 1F、3F 都可能有充電樁，數量不定。

目標：把充電樁改成**設定檔座標驅動**，沿用庫位（`oPort.Remark`）相同的 `"X,Y,角度"` 格式與 `ConvertX/ConvertY`（含 `swapXY`）邏輯，支援多樓層、多顆，改設定檔即可調位置。

## 需求範圍

**包含：**
- `appsettings.json` 新增 `ChargingStations` 區段（以 area 為 key，值為 `"X,Y,角度"` 字串陣列）。
- 後端依 area 讀取、解析、換算（3F 須套 `swapXY`），輸出到 `ViewBag.ChargingStations`。
- `_MapPartial.cshtml` 改為迴圈渲染，移除 `area=="FHT1-1F"` 樓層寫死。
- 充電樁依角度 `rotate` 顯示。
- 將 `ConvertX/ConvertY` 抽成可共用、可測試的工具（純搬移重構）。

**不包含：**
- 充電樁「充電中亮燈」狀態切換（維持靜態 `ChargingStation-off.png`）。
- 充電樁真實 RCS 座標蒐集（本次先填虛擬座標 + TODO，由使用者後續替換）。
- 地圖校正值（`AgvSetting`）調整、底圖 PNG 重畫（另案處理）。

## 限制條件

- 純搬移 `ConvertX/ConvertY` 不得改變現有換算行為（重構需先補測試鎖定，獨立 commit）。
- 3F 設有 `swapXY:true`，充電樁換算必須與該樓層庫位套用相同的 swap 規則。
- 設定字串格式錯誤時須容錯（略過該筆 + 記 Warn log），不可中斷其他充電樁或丟例外讓畫面壞掉。
- 既有庫位 / AGV 車 / 其他樓層顯示行為不得受影響。

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 讀充電樁座標 | `appsettings.json` → `ChargingStations:{area}` | `IConfiguration.GetSection().Get<string[]>()` | 該樓層無設定 → 回傳空清單，不畫任何充電樁 |
| 解析 `"X,Y,角度"` | 設定字串 | `Split(",")` 取 3 段 | 段數不足 / 非數值 → 略過該筆 + 記 Warn log |
| swapXY（3F） | `AgvSetting:{area}:swapXY` | 與 `GetTrac` 相同：`(posX,posY)=(posY,posX)` | 同庫位邏輯 |
| 座標換算 | 解析後 X,Y | `MapCoordinateConverter.ConvertX/ConvertY` | 同庫位邏輯 |

## 驗收標準

### 🧪 子任務 1：抽取 `MapCoordinateConverter`（重構，鎖定既有行為）
- [ ] Given FHT1-1F 校正設定，When `MapCoordinateConverter.ConvertX(cfg,"FHT1-1F","198700")`，Then 回傳 `"17%"`（minX→minPercentX）
- [ ] Given FHT1-1F 校正設定，When `ConvertX(cfg,"FHT1-1F","214000")`，Then 回傳 `"91.5%"`（maxX→maxPercentX）
- [ ] Given FHT1-1F 校正設定，When `ConvertY(cfg,"FHT1-1F","196350")`，Then 回傳 `"14%"`（minY→minPercentY）
- [ ] Given 抽取前後，When 對相同輸入呼叫，Then `CommonController` 經由新工具產出的庫位/車百分比與重構前完全一致（0 邏輯變更）

### 🧪 子任務 2：`ChargingStationProvider.Get`
- [ ] Given 設定檔 `ChargingStations:FHT1-1F` 有 1 筆 `"X,Y,0"`，When `Get(cfg,"FHT1-1F")`，Then 回傳 1 個 Position，Left/Bottom = 對該座標換算結果、Transform = `rotate(0deg)`
- [ ] Given 設定檔 `ChargingStations:FHT1-1F` 有 3 筆，When `Get`，Then 回傳 3 個 Position
- [ ] Given 設定檔 `ChargingStations:FHT1-3F` 且 `AgvSetting:FHT1-3F:swapXY=true`，When `Get(cfg,"FHT1-3F")`，Then 座標經 X/Y 交換後再換算
- [ ] Given 設定檔該樓層無 `ChargingStations`，When `Get`，Then 回傳空清單（Count=0）
- [ ] Given 設定字串格式錯誤（段數不足 / 非數值），When `Get`，Then 略過該筆、不拋例外、其餘正常筆數仍回傳
- [ ] Given 角度為 90 的設定，When `Get`，Then 該 Position 的 Transform = `rotate(90deg)`

### 一般子任務 3～5
- [ ] `appsettings.json` 含 `ChargingStations` 區段，包含 `FHT1-1F` 與 `FHT1-3F` 的虛擬座標並標註 `TODO 待替換真實座標`
- [ ] `_MapPartial.cshtml` 依 `ViewBag.ChargingStations` 迴圈渲染，已移除 `area=="FHT1-1F"` 寫死條件
- [ ] 設定 N 顆充電樁 → 畫面顯示 N 顆，位置與設定座標換算一致
- [ ] 切換到 3F → 顯示 3F 設定的充電樁，且位置正確（swap 後）
- [ ] 充電樁維持使用 `ChargingStation-off.png`，並依設定角度 `rotate`
- [ ] 既有庫位 / AGV 車顯示不受影響（回歸）
