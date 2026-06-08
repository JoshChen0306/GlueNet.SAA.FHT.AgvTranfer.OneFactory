# 技術方案書：庫位 SENSOR 自動清空

## 選定方案概述

在 **ACC（HikAGVWebAPI，.NET Framework 4.8）** 新增兩個核心類別，沿用現場既有 MX Component 通訊路徑，只挑「開連線 + 讀單點」最小子集自寫，避免引入 `GlueNet.EquipmentTool.Core` 整包（含 WPF / Dragablz 等用不到的肥肉）。

- `PlcDevice`：單純的 PLC 連線封裝（sensor=平板，ON=在/OFF=不在），以 StationNumber 初始化，負責 Open / 讀單一 bit / Close。
- `StockSensorLinkManager`：功能大腦，讀設定檔、起 STA 背景執行緒定時掃描、做 level + 去抖判斷（bit=OFF 且 `IsOccupied` 連續達 `ConfirmCount` 輪）、觸發既有 `Update_oPortEmpty` 清空庫位。

## 架構設計

### 元件關係與資料流向

```
Global.asax  Application_Start
   └─ stockSensorLink = StockSensorLinkBootstrap.Create(configPath);
       stockSensorLink?.Start();
                 │
   StockSensorLinkManager（排程 + level 去抖 + 庫位處理）
       ├─ 由 Bootstrap 解析 <SectionSensorClear>：StationNumber / PollIntervalMs / ConfirmCount / SensorMap
       ├─ Start()：起一條專屬 STA 背景執行緒 → Execute()
       ├─ Execute()：於 STA 由 factory 建 PlcDevice；while(!_stop){ ScanOnce(); Sleep(間隔); }
       └─ ScanOnce()：對每個對應
            ├─ plc.TryReadBit("M10", out bool on)
            ├─ 讀取失敗 → counter=0、記 Warning、continue
            ├─ on（平板在）→ counter=0、continue
            ├─ off → portReader.GetPort("B1") → IsOccupied?
            │        ├─ 否 → counter=0、continue
            │        └─ 是 → counter++；達 ConfirmCount → clearer.ClearStock("B1") + counter=0
            └─ IsOccupied = RackId非空 OR WorkOrder非空 OR HaveFlag∈{1,3}
   PlcDevice : IBitReader（單一 PLC 連線封裝，COM 全程同一 STA thread）
       ├─ PlcDevice(int stationNumber, Log)
       ├─ Open()            → new ActUtlType(64); ActLogicalStationNumber=站號; Open()（32/64 位自動選）
       ├─ TryReadBit(addr)  → ReadDeviceBlock2(addr, 1, out short) → 0/1 → bool；非 0 記失敗回 false
       └─ Close()/Dispose() → Close()
```

### 可測試接縫（為 TDD 設計）

`StockSensorLinkManager` 不直接 new `PlcDevice` / `SQLData`，而是依賴抽象，方便單元測試以假件替換、且不需真實 PLC / DB：

```csharp
public interface IBitReader      { bool TryReadBit(string address, out bool on); }
public interface IPortStateReader { oPortModel GetPort(string stationNo); }
public interface IStockClearer    { void ClearStock(string stationNo); }

// 正式組裝：
//   PlcDevice          : IBitReader        （ActUtlType COM 讀單 bit）
//   SqlPortStateReader : IPortStateReader  （內部 SQLData.Select_oPort）
//   SqlStockClearer    : IStockClearer     （內部 SQLData.Update_oPortEmpty）
```

- `ScanOnce()` 為 public，測試以腳本化的 `IBitReader` / `IPortStateReader` 餵值、用 spy `IStockClearer` 驗證清空次數與參數。
- 背景執行緒只是「重複呼叫 ScanOnce() + Sleep」，邏輯全在 ScanOnce() 內，故執行緒本身不需單測。

### 掃描規則（level + 去抖）

每個庫位有一個 `counter`（連續「OFF 且佔用」輪數）：

| 讀取結果 | 動作 |
|--|--|
| 讀取失敗（斷線） | `counter=0`、記 Warning、不清 |
| bit = ON（平板在） | `counter=0`、不清 |
| bit = OFF 且 `IsOccupied=false`（軟體已空） | `counter=0`、不清 |
| bit = OFF 且 `IsOccupied=true` | `counter++`；達 `ConfirmCount` → `ClearStock` + `counter=0` |

`IsOccupied(port)`：`port==null` → false；否則 `RackId 非空 OR WorkOrder 非空 OR HaveFlag ∈ {1,3}`。
此設計避開 AGV 送料落位瞬間（OFF→ON）誤清，並能於 ACC 重啟後（counter 從 0）自我補清殘留。

### 設定檔（`Config/FHtSetting.config`）

```xml
<configSections>
  ...
  <section name="SectionSensorClear" type="HikAGVWebAPI.SectionSensorClear, HikAGVWebAPI"/>
</configSections>

<SectionSensorClear>
  <SensorClearSettings Enable="true" StationNumber="1" PollIntervalMs="1000"
                       ConfirmCount="3" SensorMap="M10:B1" />
</SectionSensorClear>
```
新增 `SectionSensorClear` / `SensorClearSettings` 兩個設定類別，鏡射既有 `SectionFHt` / `FHtSettings`（位於 `Models/ConfigModel.cs`）。`ConfirmCount` = 連續確認輪數（≤0 自動回退為 1）。`SensorMap` 以逗號分隔多筆、冒號分隔「位址:庫位」，解析為 `List<SensorMapping>`。

### 涉及檔案

| 檔案 | 動作 |
|--|--|
| `App_Start/PlcDevice.cs` | 新增（IBitReader 實作，ActUtlType COM 封裝，32/64 位自動選） |
| `App_Start/StockSensorLinkManager.cs` | 新增（IBitReader / IPortStateReader / IStockClearer / SensorMapping / SqlPortStateReader / SqlStockClearer / StockSensorLinkBootstrap） |
| `Models/ConfigModel.cs` | 新增 `SectionSensorClear` / `SensorClearSettings`（含 `ConfirmCount`） |
| `Config/FHtSetting.config` | 加 configSections 項 + `<SectionSensorClear>` 區段 |
| `Global.asax.cs` | `Application_Start` 起、`Application_End` 停 |
| `HikAGVWebAPI.csproj` | 加 `Interop.ActUtlTypeLib` / `Interop.ActUtlType64Lib` 參考（預建 DLL，HintPath 至 `Dll\`） |
| `HikAGVWebAPITests/App_Start/StockSensorLinkManagerTests.cs` | 新增（IsOccupied / 去抖 / 解析單元測試） |

## 方案分析（六面向）

1. **方案概述**：沿用現場 MX Component，自寫最小「開連線 + 讀單點」，兩個類別＋一個設定區段，清空重用既有 `Update_oPortEmpty`。
2. **架構設計**：`PlcDevice`（連線/讀點）與 `StockSensorLinkManager`（設定/排程/偵測/清空）職責分離；以 `IBitReader` / `IStockClearer` 解耦 COM 與 DB，邏輯核心可單測。
3. **優點**：依賴最小（只多一個 `ActUtlTypeLib` COM 參考）；通訊路徑與現場一致、不增風險；清空邏輯重用既有、不重造；核心邏輯純函式化可 TDD。
4. **風險與缺點**：依賴佈署機器安裝 MX Component 並設好站號（環境前置）；ActUtlType STA 需自管專屬執行緒；COM 讀取需實機才能完整驗證。
5. **技術債評估**：低。設定檔逗號分隔即可擴充多庫位；偵測邏輯與通訊/DB 解耦，未來換通訊（如改 HslCommunication）只需替換 `IBitReader` 實作，`StockSensorLinkManager` 不動。
6. **建議適用情境**：現場已使用 MX Component、只需新增少量 M 點監控、希望最小依賴與最低通訊風險時。

## 被捨棄的方案

- **方案 A：ProjectReference 整包 `GlueNet.EquipmentTool.Core` 重用 `MxLinkDriver`**
  捨棄原因：該庫為通用化實作，含 word/bit 分群、區塊讀取、WPF docking（Dragablz）等大量用不到的內容，會把 WPF/UI 依賴帶進 Web API，安裝龐大，與「輕量」訴求衝突。本方案僅借用其 COM 呼叫精華（GetDevice2/Open）自寫精簡版。

- **方案 C：改用 HslCommunication 走 SLMP/MC over Ethernet**
  捨棄原因：雖部署最乾淨（單一 NuGet、免裝 MX Component），但等於新開一條與現場既有不同的通訊通道，需 PLC 開乙太網 MC 埠並取得 IP/Port，改變通訊路徑、增加不確定性。保留為未來若要去 MX Component 化的備案（屆時只需替換 `IBitReader` 實作）。
