# plan.md — 一廠 AB 棟 1F 新增搬運路線與邏輯 技術方案書

> 建立日期：2026-07-26
> 對應規格：`spec.md`

## 選定方案概述

**沿用既有鏈路擴充，不新建通道**：sensor 走 B1 現行鏈路（ACC StockSensorLink → oPort → SCP 輪詢）；自動派送把 `AutoDispatchService` 重構為 **appsettings 路線規則清單驅動**的通用引擎；PLC 三色燈輸出在 **ACC 既有 MX Component 框架補寫入能力**；空搬偵測僅寫 Log。

## 架構設計

```
┌─ 三菱 PLC（貨架 sensor / 三色燈）
│    ▲ 讀 GetDevice2（既有）      │ 寫 SetDevice2（新增）
│    │                            │
├─ ACC HikAGVWebAPI (net48, IIS)
│    ├─ StockSensorLinkManager（既有）→ 擴充 1F 新貨架點位 → 更新 oPort
│    └─ RackStatusOutputManager（新增，比照 StockSensorLink 架構）
│         輪詢 oPort 狀態 → 差異偵測 → SetDevice2 寫三色燈位址
│                                  │
├─ DB agvDB_1400004（oPort / oNeed / oRequire / oMission / pRoute）
│                                  │
└─ SCP (net6.0)
     ├─ AutoDispatchService → 重構為規則引擎（AutoDispatchRouteEngine）
     │    appsettings AutoDispatch:Routes[] 逐條執行：
     │    來源區 HaveFlag=3 → 排除 pending 起點 → SourceOrder 排序（FIFO）
     │    目的區 HaveFlag=0 → 排除 BgnToEnd / pending 終點 → 配對 → INSERT oNeed
     │    ＋ sensor 防呆（狀態不一致不派車）
     ├─ 任務完成 → 物料資訊轉寫（來源→目的）＋空搬偵測（目的 HaveFlag 非 ON → Warning Log）
     ├─ DispatchController.Release 新增 R1~R4 手動分支
     └─ 地圖/儲位頁：新區域顯示＋物料狀態顏色
```

### 路線規則設定格式（appsettings.json）

```json
"AutoDispatch": {
  "Enabled": true,
  "IntervalSeconds": 30,
  "Routes": [
    { "Name": "路線6 M→K", "Enabled": true, "SourceBlocks": ["M"], "TargetBlocks": ["K"], "SourceOrder": "PutTime" },
    { "Name": "R1 中光電D→迅得2/3/4", "Enabled": true, "SourceBlocks": ["<D>"], "TargetBlocks": ["<X2>","<X3>","<X4>"], "SourceOrder": "PutTime" },
    { "Name": "R2 迅得2/3/4→中光電C", "Enabled": true, "SourceBlocks": ["<X2>","<X3>","<X4>"], "TargetBlocks": ["<C>"], "SourceOrder": "PutTime" },
    { "Name": "R3 迅得1→中光電B", "Enabled": true, "SourceBlocks": ["<X1>"], "TargetBlocks": ["<B>"], "SourceOrder": "PutTime" },
    { "Name": "R4 中光電A→迅得5/6/7", "Enabled": true, "SourceBlocks": ["<A>"], "TargetBlocks": ["<X5>","<X6>","<X7>"], "SourceOrder": "PutTime" }
  ]
}
```

（`<>` 為佔位，實際 Block 代碼以 T1 區域代碼規劃結果為準）

## 方案分析（六面向）

1. **方案概述**：既有機制參數化擴充——規則驅動派送引擎 + ACC PLC 框架補寫入 + 空搬僅 Log。
2. **架構設計**：如上圖；SCP 不直接碰 PLC，硬體 I/O 全收斂在 ACC；SCP 只讀寫 DB。
3. **優點**：
   - 加路線只改 JSON + 重啟，不改程式（客戶後續擴充成本最低）
   - 共用邏輯（pending 排除、空位判斷）只存在一份，修一處全路線受惠
   - 引擎可依賴注入 mock 完整單元測試；PLC 寫入沿用 StockSensorLink 的介面抽象模式（IBitReader → 增 IBitWriter）
   - 空搬僅 Log，避免動用已停用的 aAlarm 鏈道，範圍最小
4. **風險與缺點**：
   - 重構既有 M→K 需回歸驗證（列入測試案例）
   - PLC 寫入是新能力，bit/word 裝置雷點需比照讀取經驗（GetDevice2 事件）處理
   - 假設對方 PLC 為三菱；若非，T17/T18 通訊層需換方案重估
   - 設定改動需重啟服務生效（非熱更新）
5. **技術債評估**：規則引擎收斂重複邏輯，屬還債；新玩法（插隊、時段限制）仍需擴充引擎策略後才有設定值可用；ACC 輸出服務比照既有架構模式，維護心智負擔低。
6. **建議適用情境**：路線數 ≥ 3 且邏輯同構（本案 R1~R4＋M→K 全部同構），規則驅動最划算。

## 被捨棄的方案

| 方案 | 捨棄原因 |
|------|---------|
| AutoDispatch 複製硬編碼 ×4 | 5 份重複邏輯，共用修改需改 5 處，維護成本高（2026-07-26 使用者選規則驅動） |
| 空搬異常重啟 aAlarm 機制 / 新增輕量異常表 | 使用者確認空搬只需寫 Log，不做畫面顯示，兩案皆過度設計 |
| PLC 寫入失敗即告警 | 狀態可重建（下輪重寫自癒），失敗即告警訊息量大；採 Warning Log + 自癒 |
| SCP 直接新建 PLC 通訊 | ACC 已有 MX Component 框架與部署經驗（DCOM/LocalSystem），重複造輪 |

## 測試骨架配置

| 測試專案 | 格式 | 骨架處理 |
|---------|------|---------|
| `WebGui/SCP.Tests`（net6.0, SDK 式） | 新檔自動納入 | T6/T8/T19 骨架已預建於 `SCP.Tests/Services/` |
| `ACC/HikAGVWebAPI/HikAGVWebAPITests`（net48, 舊式 csproj） | 加檔需登記 csproj | T18 骨架於該任務 Red 階段建立（避免規劃期動舊 csproj） |
