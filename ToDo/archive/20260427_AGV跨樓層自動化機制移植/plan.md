# AGV 跨樓層自動化機制移植 — 技術方案書

## 選定方案概述

**方案 A：直接 Copy & Paste（依二廠檔案逐一複製、最小調整）**

從二廠專案直接複製 `CrossFloorManager.cs`、`CooldownTracker.cs` 與相關 SQL Deploy 檔、單元測試檔至一廠，僅調整 namespace、一廠 config 鍵與初始資料（限 1F↔3F 範圍），最小化改動以保留二廠已產線驗證的邏輯。

---

## 架構設計

### 元件關係圖

```
┌─────────────────────────────────────────────────────────────┐
│ ACC / HikAGVWebAPI / HikAGVWebAPI / App_Start /             │
│                                                              │
│  ┌─────────────────────┐                                    │
│  │ Dispatch (Singleton)│  ←─ 主循環 1秒/次                  │
│  │  Execute()          │                                    │
│  │  └─ Tick()          │                                    │
│  │      ├─ UpdateAGVStatus()                                │
│  │      ├─ AGVSchedulingTask()                              │
│  │      └─ CrossFloor.Tick()  ◄── 新增掛點                  │
│  │  + static CrossFloorManager CrossFloor                   │
│  └──────┬──────────────┘                                    │
│         │                                                    │
│         ▼                                                    │
│  ┌─────────────────────────────────┐                        │
│  │ CrossFloorManager (新增 477行)   │                        │
│  │  - Tick()                        │                        │
│  │  - SelectNextMission()           │                        │
│  │  - DecideNextCrossFloorAction()  │  純函數，可測試        │
│  │  - DispatchIdleReturn()          │                        │
│  │  - DispatchCrossFloor()          │                        │
│  │  - OnIdleReturnCompleted()       │                        │
│  │  - OnCrossFloorDispatchCompleted │                        │
│  │  - OnNewTaskArrived()            │                        │
│  └─────┬─────────┬─────────┬────────┘                        │
│        │         │         │                                 │
│        ▼         ▼         ▼                                 │
│  ┌──────────┐ ┌──────────────────┐ ┌──────────────────┐    │
│  │Cooldown  │ │ElevatorPath      │ │SQLData           │    │
│  │Tracker   │ │Calculator (沿用) │ │+ Select_oTaskType│    │
│  │(新增)    │ │ - BuildReturnPath│ │  Route           │    │
│  │ - IsIn   │ │ - GetFloor       │ │+ GetTaskType     │    │
│  │   Cooldown│ │ - IsCrossFloor  │ │+ Update_oShuttle │    │
│  │ - Record │ │                  │ │  UpdateTime      │    │
│  │   Compl. │ │                  │ │                  │    │
│  └──────────┘ └──────────────────┘ └─────────┬────────┘    │
│                                              │              │
└──────────────────────────────────────────────┼──────────────┘
                                               ▼
                              ┌────────────────────────────┐
                              │ DB: oShuttle / oMission /   │
                              │     oRequire / oTaskTypeRoute│
                              └────────────────────────────┘
                                               ▲
┌─────────────────────────────────────────────┴───────────────┐
│ ACC / HikAGVWebAPI / HikAGVWebAPI / API / CallBackAPI       │
│  AGVCallback(end)   → CrossFloor?.OnIdleReturnCompleted()   │
│  AGVCallback(end)   → CrossFloor?.OnCrossFloorDispatchCompleted() │
│  AGVCallback(cancel)→ CrossFloor?.OnTaskCancelled()         │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ WebGui / SCP / Controllers / TaskTypeRouteController (新增)  │
│  CRUD: oTaskTypeRoute                                        │
│  + View: TaskTypeRoute/Index.cshtml                          │
└─────────────────────────────────────────────────────────────┘
```

### 資料流向

**情境 1：自動回歸（IDLE_RETURN）**
```
Dispatch.Execute() (1秒/次)
  └→ CrossFloor.Tick()
      ├→ Select_oShuttle(CrossFloorShuttleId=3) [讀DB]
      ├→ 判斷 Status="I" 且不在 IdleReturnFloor=3F
      ├→ 啟動計時（>= 600秒？）
      └→ 超時 → DispatchIdleReturn()
          ├→ ElevatorPathCalculator.BuildReturnPath()
          ├→ Get_TaskType("EmptyMove", currentFloor, "3F") [讀oTaskTypeRoute]
          ├→ Insert_oMission(TaskSource="IDLE_RETURN") [寫DB]
          └→ Insert_oRequire() [寫DB]
              → 後續 Dispatch 正常派發 → RCS 執行 → 完成回呼
                  → CallBackAPI(end) → OnIdleReturnCompleted()
```

**情境 2：預派調度（CROSS_FLOOR_DISPATCH）**
```
Dispatch 評估跨樓層待派任務
  └→ CrossFloor.SelectNextMission()
      ├→ DecideNextCrossFloorAction(pending, currentFloor, calculator, cooldown)
      │   ├→ 同樓層 → SameFloor（直接派）
      │   ├→ 冷卻期命中 → CooldownHit（攔截重派）
      │   ├→ 跨樓層 → NeedDispatch（產生預調度）
      │   └→ 無任務 → None
      └→ NeedDispatch → DispatchCrossFloor()
          ├→ Get_TaskType("EmptyMove", currentFloor, taskBeginFloor)
          ├→ Insert_oMission(TaskSource="CROSS_FLOOR_DISPATCH",
          │                  ParentTaskDateTime=父任務.TaskDateTime)
          └→ Insert_oRequire()
              → RCS 執行 → 完成回呼
                  → CallBackAPI(end) → OnCrossFloorDispatchCompleted()
                      → CooldownTracker.RecordCompletion(parent)
                      → 重置狀態 → 下次 Tick 重新評估父任務
```

---

## 方案分析

### 1. 方案概述
從二廠專案直接複製核心類別與測試檔，調整 namespace 與一廠 config 鍵。

### 2. 架構設計
見上節「元件關係圖」與「資料流向」。

### 3. 優點

- **與二廠程式碼 1:1 對齊**，未來 bug 修復可直接同步兩邊
- 最忠實「完全照搬」原則，**行為差異可控**（只在 config 值與初始資料）
- 二廠已產線驗證 (commit `e0171b0`)，**邏輯風險低**
- 二廠 16+ 筆單元測試一併移植，**回歸保護完整**
- 一廠 `ElevatorPathCalculator` 已存在，可直接重用，省去搬移成本
- 一廠 `oMission.TaskSource` 已存在（無需 ALTER），降低 schema 變更風險

### 4. 風險與缺點

- **程式碼分叉**：未來兩廠各自演進需要手動 sync（中期維護成本）
- **namespace / using 調整**：複製過來要逐一改 using（風險低但繁瑣）
- **一廠 SQLData 介面差異**：若 method signature 與二廠不一致，需逐一比對適配（風險點）
- **WebGui 後台頁面 namespace / Razor 版本差異**：複製 PortBindingController 頁面結構時需注意

### 5. 技術債評估

- **中期債**：兩廠各有一份 CrossFloorManager，未來邏輯演進需雙邊同步，預估每次同步 1~2 工時
- **降債方向**：未來若有第三廠或邏輯成熟後，可重構成共用 NuGet（不在本次範圍）
- **可維護性**：高（檔案命名、結構與二廠完全一致，新人可對照學習）
- **測試覆蓋**：高（移植二廠既有 16+ 筆單元測試）

### 6. 建議適用情境

- ✅ 客戶要求快速上線
- ✅ 二廠邏輯已穩定（事實上已穩定）
- ✅ 一廠開發人力有限，避免過度設計
- ❌ 不適用：兩廠有大量演進需求且需要長期共用維護的情境（應選方案 B）

---

## 被捨棄的方案

### 方案 B：抽取共用類別庫（CrossFloor.Common NuGet 包）

**捨棄原因：**
- 需動到二廠（已穩定上線），改造風險高
- 抽象介面設計成本高（`ICrossFloorContext`、`ITaskRouteRepository`），耗時是方案 A 的 2~3 倍
- NuGet 發版 / 版本管理流程要建立，超出本次範圍
- 「完全照搬」原則下，不需要這層抽象

### 方案 C：分階段移植（Phase 1 自動回歸 → Phase 2 預派調度）

**捨棄原因：**
- 與二廠 CrossFloorManager 的單一類別架構不一致 — 拆兩個 Manager 反而需要重新設計狀態同步邏輯
- 情境 7（預調度執行中又來新任務）會跨兩個 Manager，邏輯耦合難拆
- 拆分後二廠的 16 筆單元測試無法直接套用，要重寫
- 整體工作量增加 30%
- 違背「完全照搬」原則
- 上線後 Phase 2 再合併等於第三次重構，反而更貴

---

## 部署順序（Migration Strategy）

1. **DB Migration（先）**
   - `Deploy_oShuttle_AddUpdateTime.sql`
   - `Deploy_oShuttle_AddMapCode.sql`（一廠專屬）
   - `Deploy_AddParentTaskDateTime.sql`
   - `Deploy_oTaskTypeRoute.sql`（含 1F<>3F 4 筆初始資料）
   - `Deploy_pFunction_TaskTypeRouteMenu.sql`

2. **程式部署**
   - 先停 Dispatch / WebGui
   - 部署新版 ACC / WebGui
   - HikAGV.config 寫入新增的 `<AGVSettings>` 屬性

3. **驗證**
   - 啟動 Dispatch，觀察 Log 是否有 CrossFloor 初始化訊息
   - 手動測試：將跨樓層車（ID=3）開到 1F 閒置，計時 600 秒應觸發 IDLE_RETURN
   - 手動測試：建立 1F 起點任務但車在 3F，應觸發 CROSS_FLOOR_DISPATCH

---

## 與一廠既有功能的相容性

| 既有功能 | 相容性評估 | 備註 |
|---------|-----------|------|
| Port 動態樓層（FloorSettings）| 不衝突 | CrossFloor 用獨立 MapCodeFloorMapping |
| 路線權限（pUserRoute / pRoute）| 不衝突 | CrossFloor 任務以系統身份產生，不經權限檢查 |
| ElevatorPathCalculator | 直接重用 | 一廠已有，不需重做 |
| 空平板派送（一廠版）| 不衝突 | 一廠保留自己的邏輯，本次不動 |
| oMission.TaskSource | 直接重用 | 既有欄位，新增值 IDLE_RETURN / CROSS_FLOOR_DISPATCH |

---

## Commit 策略

依「結構性重構 0 邏輯變更」原則，本次以「新增功能」為主，不混雜重構。建議 commit 順序：

| # | Commit Message | 變動範圍 |
|---|---------------|---------|
| 1 | `feat(db): 新增 oShuttle MapCode/UpdateTime 與 oMission ParentTaskDateTime 欄位` | Deploy_*.sql ×3 |
| 2 | `feat(db): 新建 oTaskTypeRoute 表與 1F<>3F 初始路由資料` | Deploy_oTaskTypeRoute.sql + 後台選單 |
| 3 | `feat(config): HikAGV.config 新增跨樓層 AGVSettings 屬性` | ConfigAGVModel.cs + HikAGV.config |
| 4 | `feat(model): oShuttleModel 新增 MapCode / UpdateTime 屬性` | oShuttleModel.cs |
| 5 | `test(cooldown): 移植 CooldownTracker 單元測試（紅燈）` | CrossFloorManagerCooldownTests.cs |
| 6 | `feat(cooldown): 移植 CooldownTracker 實作（綠燈）` | CooldownTracker.cs |
| 7 | `test(cross-floor): 移植 CrossFloorManager 決策函數測試（紅燈）` | CrossFloorManagerSelectNextMissionTests.cs |
| 8 | `feat(cross-floor): 移植 CrossFloorManager 核心實作（綠燈）` | CrossFloorManager.cs |
| 9 | `feat(repo): 新增 oTaskTypeRoute Repository 與 GetTaskType 查詢` | SQLData.cs |
| 10 | `feat(dispatch): Dispatch 主循環掛接 CrossFloor.Tick()` | Dispatch.cs |
| 11 | `feat(callback): CallBackAPI 補 CrossFloor 完成回呼` | CallBackAPI.cs |
| 12 | `feat(webgui): 新增 oTaskTypeRoute 後台 CRUD 管理頁` | TaskTypeRouteController + View |
| 13 | `docs: 新增一廠 AGV 跨樓層自動化機制功能說明書` | Doc/*.md |
