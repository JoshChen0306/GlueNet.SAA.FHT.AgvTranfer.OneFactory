# 需求規格書：庫位 SENSOR 觸發自動清空工單與庫位狀態

## 主題與背景

客戶在庫位安裝實體 SENSOR，訊號接入三菱（Mitsubishi）PLC 的 M 暫存器（bit device）。
**該 sensor 偵測的是平板（貨架）本體：bit ON = 平板在上面、bit OFF = 平板不在。**

系統定時掃描該 M 暫存器，當 **bit = OFF（平板不在）且軟體端仍認為該庫位佔用**（`oPort` 的 `RackId` / `WorkOrder` / `HaveFlag` 任一仍顯示有平板/料），**連續達 `ConfirmCount` 輪去抖確認後**，自動清空該庫位的工單與狀態（`Update_oPortEmpty`），使 oPort 回到「無料」。

採「level + 去抖」而非單純邊緣偵測的原因：
- AGV 送料任務結束（`UpdateEnd`）會**先寫入 WorkOrder / HaveFlag=3**，送料落位瞬間 sensor 若短暫 OFF，單純 level 會誤清剛送達的工單；去抖（連續 N 輪）可濾掉此瞬間。
- 送料是 `OFF→ON`、取走是 `ON→OFF`，去抖天然避開送料競態。
- 計數從 0 起算，ACC 重啟後若有殘留（OFF + 軟體仍佔用），連續達標即可**自我補清**。

目前客戶只有「一個」庫位要做這個處理，但設計需保留多庫位擴充能力（設定檔逗號分隔即可加點）。

## 需求範圍

### 包含
- 在 ACC（HikAGVWebAPI，.NET Framework 4.8）新增「PLC 庫位 SENSOR 監控」功能。
- 以設定檔設定 PLC 站號、輪詢間隔、`M 位址 ↔ 庫位 StationNo` 對應表。
- 定時掃描 PLC M 暫存器，採 level + 去抖：`bit=OFF` 且軟體仍佔用，連續達 `ConfirmCount` 輪才動作。
- 佔用判斷讀取 `oPort`（`SQLData.Select_oPort`），三欄位任一成立即視為佔用：`RackId` 非空、`WorkOrder` 非空、`HaveFlag ∈ {1,3}`。
- 確認後呼叫既有 `SQLData.Update_oPortEmpty(StationNo)` 清空 `RackId`、`WorkOrder` 並把 `HaveFlag` 設回 `0`。
- 讀取失敗（PLC 斷線）採安全策略：不動作、計數歸 0、只記 Warning Log。
- `ConfirmCount` 連續確認輪數可由設定檔調整。

### 不包含
- 不新增 UI（SCP 前端不改動）。
- 不改變既有 AGV 派工、跨樓層、callback 等流程。
- 不處理 `false → true`（放料）方向的任何動作。
- 不自行實作三菱通訊協定堆疊；沿用現場既有 MX Component（ActUtlType COM）通訊路徑。
- 不新增 NuGet 第三方 PLC 套件（不引入 HslCommunication，不參考 GlueNet.EquipmentTool.Core 整包）。

## 限制條件

### 技術限制
- PLC 讀取走三菱 **MX Component（`ActUtlTypeLib` ActUtlType COM 元件）**，僅能在 .NET Framework 執行；故功能落在 ACC（net48），不可放 SCP（net6）。
- ActUtlType 為 **STA COM 物件**：所有 COM 呼叫必須在同一條 STA 執行緒上完成，避免跨執行緒 marshaling。
- 因 STA 限制，排程**不使用 `System.Timers.Timer`（threadpool 回呼）**，改用「專屬 STA 背景執行緒 + 固定間隔 `Thread.Sleep`」（與既有 `Dispatch.Execute()` 同 pattern）。
- 連線以 MX Component 的 **Logical Station Number** 識別（非直接 IP）；佈署機器須事先安裝 MX Component 並在 Communication Setting Utility 設好站號。

### 環境前置（佈署相依）
- ACC 所在機器需安裝 MX Component runtime。
- 需於 MX Component 設定一個 Logical Station Number 指向目標 PLC，並把站號寫入設定檔。

### 資料流

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 讀取庫位 SENSOR（平板）狀態 | 三菱 PLC M 暫存器（bit） | `PlcDevice.TryReadBit(addr)` → `ActUtlType.ReadDeviceBlock2` | 回傳讀取失敗，**計數歸 0、不清空**，記 Warning Log |
| 讀取庫位佔用狀態 | SQL Server `agvDB` 的 `oPort` 表 | `SqlPortStateReader.GetPort` → `SQLData.Select_oPort`（既有） | 回 null（視為未佔用，不清），記 Warning Log |
| 清空庫位工單與狀態 | SQL Server `agvDB` 的 `oPort` 表 | `SqlStockClearer.ClearStock` → `SQLData.Update_oPortEmpty`（既有） | 沿用既有方法行為，例外記 Log |
| 讀取監控設定 | `Config/FHtSetting.config` 的 `<SectionSensorClear>` | 自訂 `ConfigurationSection` + `ConfigurationManager` | Section 缺失或 `Enable=false` → 不啟動監控，記 Log |

## 預設假設（佈署時可在設定檔調整）

- M 語意：**bit ON = 平板在、bit OFF = 平板不在**。
- 佔用判斷：`RackId 非空 OR WorkOrder 非空 OR HaveFlag ∈ {1,3}`（`HaveFlag` 有料狀態 1/3，對應 `UpdateEnd` 的 `WorkOrder空?"1":"3"`）。
- 庫位對應：設定檔右值 = `oPort.StationNo` 真實值，直接傳入 `Update_oPortEmpty`。
- 預設輪詢間隔：`1000ms`；預設連續確認輪數：`ConfirmCount = 3`（≈3 秒）。
- 預設站號：`StationNumber = 1`。
- 範例對應：`SensorMap = "M10:B1"`。

## 驗收標準

### 🧪 佔用判斷 IsOccupied（三欄位）

- [ ] Given `RackId` 非空，When 呼叫 IsOccupied，Then 回傳 true
- [ ] Given `WorkOrder` 非空，When 呼叫 IsOccupied，Then 回傳 true
- [ ] Given `HaveFlag = "1"`（有料無工單），When 呼叫 IsOccupied，Then 回傳 true
- [ ] Given `HaveFlag = "3"`（有料有工單），When 呼叫 IsOccupied，Then 回傳 true
- [ ] Given 三欄位皆空且 `HaveFlag = "0"`，When 呼叫 IsOccupied，Then 回傳 false
- [ ] Given `HaveFlag = "E"`，When 呼叫 IsOccupied，Then 回傳 false
- [ ] Given port 為 null（查無此庫位），When 呼叫 IsOccupied，Then 回傳 false

### 🧪 掃描與去抖核心邏輯（StockSensorLinkManager.ScanOnce）

- [ ] Given bit=ON（平板在），When 掃描，Then 計數歸 0、不呼叫 Update_oPortEmpty
- [ ] Given bit=OFF 但軟體未佔用（oPort 已空），When 掃描，Then 計數歸 0、不清空
- [ ] Given bit=OFF 且佔用、ConfirmCount=1，When 掃描一輪，Then 對該 StationNo 呼叫一次 Update_oPortEmpty
- [ ] Given bit=OFF 且佔用、ConfirmCount=3，When 連續掃描，Then 前兩輪不清、第三輪清且只清一次
- [ ] Given 去抖累積中途出現一輪 bit=ON，When 後續再 OFF，Then 計數自 ON 後重新起算（不誤清）
- [ ] Given 讀取失敗，When 掃描，Then 計數歸 0、不清空，連續性中斷（恢復後需重新累積至 ConfirmCount）
- [ ] Given ACC 重啟（計數從 0）、開機即 bit=OFF 且佔用殘留，When 連續達 ConfirmCount 輪，Then 補清一次（自我修復）
- [ ] Given 已清空後 oPort 變空，When 後續掃描，Then 不再重複清
- [ ] Given 多筆對應（M10:B1 OFF 且佔用、M11:B2 ON），When 掃描，Then 只清 B1、不影響 B2
- [ ] Given `ConfirmCount ≤ 0`，When 建構並掃描，Then 視為至少 1 輪即清（防呆）

### 一般驗收項

- [ ] `<SectionSensorClear>` 可正確被讀取，`SensorMap="M10:B1"` 能解析為對應清單；含空白/格式錯誤片段會被略過
- [ ] `Enable="false"` 或 Section 缺失時，Bootstrap 回 null，不建立 PLC 連線、不啟動掃描執行緒
- [ ] `PlcDevice` 以指定 StationNumber 初始化並可 Open / TryReadBit / Close，所有 COM 呼叫在同一 STA 執行緒
- [ ] `Global.asax` `Application_Start` 啟動監控、`Application_End` 正常停止掃描執行緒（無殘留 thread）
- [ ] 既有 ACC 測試與建置不被破壞（0 error、回歸測試通過）
