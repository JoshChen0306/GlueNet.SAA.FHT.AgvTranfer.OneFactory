# spec.md — 一廠 AB 棟 1F 新增搬運路線與邏輯

> 建立日期：2026-07-26
> 需求來源：`高技一廠AB棟物流需求整理_20260720.xlsx`（表一項次 7 + 表二路線 1~4，負責廠商=格路）
> 工時評估清單：`一廠AB棟_SCP工作內容清單_20260726.md`（同需求資料夾）

## 主題與背景

高技一廠 AB 棟新增 1F 搬運需求：中光電貨架（A/B/C/D）與迅得貨架（1~7）之間由迅得 AGV 自動搬運。格路負責 SCP 派車系統與 ACC 後端的對應功能開發；SLAM 路線、RCS 地圖、任務模板、三色燈硬體、sensor 硬體整合由迅得（SAA）負責。

## 需求範圍

### 包含

1. **四條搬運路線與自動派送邏輯**：
   - R1：中光電 D → 迅得 2/3/4（檢查目的空位 → 搬運 → 更新物料資訊）
   - R2：迅得 2/3/4 → 中光電 C（多來源合併、**最舊物料優先 FIFO**）
   - R3：迅得 1 → 中光電 B
   - R4：中光電 A → 迅得 5/6/7（原文件 E 欄「檢查2/3/4」為筆誤，2026-07-26 已確認為 5/6/7）
2. **地圖**：1F 底圖納入 AB 棟連廊與新貨架區、appsettings 樓層/區域/MapCode 設定、地圖顯示
3. **貨架**：新區域代碼規劃、oPort 站點資料建置、儲位設定頁支援
4. **sensor 自動搬運**：沿用 B1 現行鏈路（ACC StockSensorLink 讀 PLC → 更新 oPort → SCP 輪詢），AutoDispatchService 重構為規則驅動並新增 R1~R4
5. **sensor 防呆**：派車前驗證 sensor 狀態（HaveFlag）與登記一致，不一致不派車
6. **貨架物料狀態顯示與 PLC 輸出**：前端顏色區分物料狀態；ACC 補 PLC 寫入能力，把貨架狀態輸出給 PLC（三色燈用）
7. **空搬偵測**：AGV 搬運到目的地後檢查目的地 sensor 訊號，非 ON 即判定空搬（人為介入），**寫 Warning Log**

### 不包含

- 三色燈硬體架設、接線、燈號呈現（迅得負責）
- SLAM 路線、RCS 地圖與任務模板、交管功能（迅得負責，表一項次 1~6、8~11）
- 中光電貨架 sensor 硬體整合方式（迅得負責，表一項次 9；我方只消費其反映到 oPort 的結果）
- 空搬異常的**畫面顯示**（2026-07-26 確認只寫 Log，不做前端顯示）
- SCP ↔ 中光電派車系統介接（未定案，見「待定案」）
- PLC 熱更新 / 路線規則網頁化管理（規則改 appsettings + 重啟服務即可）

## 限制條件

- SCP：ASP.NET Core MVC（net6.0）、EF Core、DB `agvDB_1400004`；ACC：`HikAGVWebAPI`（net48、IIS 站台）
- PLC 通訊沿用 MX Component ActUtlType COM；**假設對方 PLC 為三菱**，若非三菱，PLC 寫入方案需重估
- 讀單一 bit 用 `GetDevice2`；寫入需注意 bit（`SetDevice2`）/ word（`WriteDeviceBlock2`）裝置區別
- IIS 部署：集區身分需 LocalSystem（ApplicationPoolIdentity 無 DCOM 權限會 80070005）
- 路線規則設定改 `appsettings.json` 後需重啟 SCP 服務生效
- 新區域代碼不得與現有 Block 衝突（現有 A/B/G/I/J/K/L/M/O/P/S/N/Q/R 等已占用）；「中光電A貨架」≠ 現有 A 區，命名須明確區隔

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 讀貨架 sensor（有無物料） | 三菱 PLC | ACC `PlcDevice.GetDevice2`（既有，B1 鏈路） | 中斷去抖計數、不清空、記 Log（沿用現行） |
| sensor → 儲位狀態 | DB `oPort.HaveFlag` | ACC `StockSensorLinkManager` 更新（既有） | 記 Log、下輪重掃 |
| SCP 自動派送判斷 | DB `oPort` / `oNeed` / `oRequire` / `oMission` | `AutoDispatchService` 輪詢（30 秒） | 找不到空位 → 本輪跳過，下輪再試 |
| 寫三色燈狀態給 PLC | 三菱 PLC | ACC `SetDevice2`（**待新增**，bit 裝置） | 記 Warning Log，下輪依最新狀態重寫（自癒） |
| 空搬偵測 | DB `oPort.HaveFlag`（目的地） | 任務完成後檢查 | sensor 非 ON → 判定空搬，寫 Warning Log（不做畫面顯示） |

## 待定案（阻塞項）

| # | 事項 | 影響 | 狀態 |
|---|------|------|------|
| 1 | R1 跨派車系統物料資訊：中光電 AGV 接續搬運，物料資訊人工雙邊建 or SCP↔中光電介接？ | 若需介接，另立工作包重新估價 | `[!]` 待客戶確認 |
| 2 | PLC 介面規格（站號、三色燈位址表、狀態值定義） | T17/T18 開工前置條件 | 待與迅得議定（T16） |

## 驗收標準

### AC-1 地圖與貨架基礎（T1~T4、T11、T12）

- [ ] 1F 地圖可顯示 AB 棟新區域底圖，新貨架站點依座標正確貼圖
- [ ] AGV 行經 AB 棟區域時，地圖即時位置顯示正確（MapCode 反查無 NaN）
- [ ] 儲位設定頁可依樓層/區域檢視與維護新貨架儲位（HaveFlag/UseFlag/WorkOrder 等）
- [ ] 新區域代碼與既有 Block 無衝突，oPort 站點資料建置 SQL 可重複執行（idempotent）

### AC-2 路線與手動派車（T5、T7、T9、T10）

- [ ] pRoute/pUserRoute 建置後，有權限的使用者在派送頁可見 R1~R4 對應起點區域
- [ ] R1~R4 手動 Release：目的區有空位（HaveFlag=0、非 BgnToEnd、未被 pending 指派）才建任務；無空位回應明確訊息
- [ ] R1/R4 多目的區依 Priority、Port 順序擇一

### AC-3 🧪 自動派送規則引擎（T6，Given-When-Then）

- [ ] Given appsettings 設定 R1 規則且 D 區有 HaveFlag=3 物料、2/3/4 區有空位，When 引擎執行一輪，Then 建立一筆 oNeed（TaskSource=Auto）且起訖站正確
- [ ] Given R2 多來源區（2/3/4）各有物料，When 引擎執行，Then 依 PutTime 跨區取最舊者優先派送
- [ ] Given 目的區全數無空位，When 引擎執行，Then 不建任務、不拋例外
- [ ] Given 來源站已存在 pending 任務（oNeed/oRequire/oMission），When 引擎執行，Then 該站被排除不重複派送
- [ ] Given 目的站已被其他任務指派為終點，When 引擎執行，Then 該站不被選為終點
- [ ] Given 來源站 HaveFlag 與登記狀態不一致（sensor 防呆條件），When 引擎執行，Then 不派車並記 Warning Log
- [ ] Given 規則 Enabled=false，When 引擎執行，Then 該路線整條跳過
- [ ] Given 既有 M→K 路線收斂為規則後，When 引擎執行，Then 行為與重構前一致（回歸）

### AC-4 🧪 物料資訊轉寫（T8，Given-When-Then）

- [ ] Given 任務完成事件，When 執行轉寫，Then 目的地 oPort 取得來源的 RackId/WorkOrder 並更新 PutTime，來源站清空
- [ ] Given 轉寫過程 DB 例外，When 執行轉寫，Then 記 Error Log 且不產生半套資料（來源與目的一致性）

### AC-5 貨架狀態顯示與 PLC 輸出（T14、T15、T17、T18）

- [ ] 狀態定義規格文件經迅得/客戶確認（含顏色 ↔ 狀態值 ↔ PLC 位址對應）
- [ ] 前端地圖/儲位頁依物料狀態顯示對應顏色
- [ ] 🧪 Given 貨架狀態變化，When 輸出服務輪詢，Then 以 SetDevice2 寫入對應 PLC 位址；Given 狀態無變化，Then 不重複寫入
- [ ] 🧪 Given PLC 寫入失敗，When 下一輪輪詢，Then 記 Warning Log 並依最新狀態重寫（自癒），不中斷服務
- [ ] 現場對測：三色燈狀態與 SCP 顯示一致（斷線恢復後燈號自動追上）

### AC-6 🧪 空搬偵測（T19，Given-When-Then）

- [ ] Given AGV 搬運任務完成且目的地 sensor 訊號 ON，When 偵測執行，Then 不記異常
- [ ] Given 任務完成但目的地 sensor 訊號非 ON，When 偵測執行，Then 判定空搬並寫 Warning Log（含任務號、起訖站、時間）
- [ ] 只寫 Log，不做畫面顯示（2026-07-26 確認）

### AC-7 整合驗證（T20、T22~T24）

- [ ] R1~R4 全流程（sensor 登記 → 自動派車 → 搬運 → 物料轉寫 → 狀態輸出）於測試環境跑通
- [ ] 與迅得 PLC/sensor 對測通過；與中光電聯測交接流程通過
- [ ] 部署依 deployment-checklist 5 項全數通過
