# AGV 跨樓層自動化機制移植 — 需求規格書

## 主題與背景

將二廠（GlueNet.SAA.FHT.AgvTranfer.SecondFactory）已驗證的 **CrossFloorManager 跨樓層管理機制** 完整移植到一廠（GlueNet.SAA.FHT.AgvTranfer.OneFactory），包含兩項核心功能：

1. **AGV 自動回歸（IDLE_RETURN）** — 跨樓層車輛閒置超時自動回到母樓層待命
2. **預派調度 / 全廠找車（CROSS_FLOOR_DISPATCH）** — 任務派車時若車輛不在任務起點樓層，先產生預調度任務把車派到任務起點，再派發原始任務

**移植來源版本：** 二廠 commit `e0171b0`（feat(cross-floor): AGV 自動歸位機制完整實作）

**移植原則：** 完全照搬，最小調整。差異僅在配置值與初始資料，不改邏輯。

---

## 需求範圍

### 包含

- 將二廠 `CrossFloorManager.cs`（核心 477 行）移植到一廠
- 將二廠 `CooldownTracker.cs` 移植到一廠
- 二廠對應單元測試移植（CrossFloorManagerCooldownTests / CrossFloorManagerSelectNextMissionTests）
- DB schema 變更：
  - `oShuttle.UpdateTime`（新增欄位）
  - `oShuttle.MapCode`（新增欄位）
  - `oMission.ParentTaskDateTime`（新增欄位）
  - `oTaskTypeRoute`（新建表，跨樓層 TaskType 動態路由管理）
- 設定檔擴充：`HikAGV.config` 新增 `<AGVSettings>` 屬性（CrossFloorShuttleId / IdleReturnTimeout / IdleReturnFloor / MapCodeFloorMapping）
- WebGui 後台：oTaskTypeRoute CRUD 管理頁
- Dispatch.cs 整合 CrossFloor.Tick()
- CallBackAPI.cs 回呼處理（end / cancel）

### 不包含

- 空平板自動派送（PLATE_RECOVERY）— 一廠有自己的邏輯，本次不動
- 多輛跨樓層車輛支援 — 本次只支援單台（CrossFloorShuttleId="3"），未來 Phase 2 再擴
- 2F、4F 跨樓層 — 一廠目前任務只 1F↔3F，其他樓層不在本次範圍
- 第二部電梯（客貨梯）— config 保留設定但實際只用一部電梯

---

## 限制條件

### 環境限制

- 一廠 `.NET Framework` 版本與二廠一致
- 一廠 SQL Server 資料庫已部署 `agvDB_1400004`，本次需執行 schema migration
- 一廠 `ElevatorPathCalculator` 已存在，可直接重用

### 配置初始值（一廠）

| 屬性 | 值 | 說明 |
|------|-----|------|
| `CrossFloorShuttleId` | `3` | 指定第 3 台 AGV 為跨樓層專用車 |
| `IdleReturnTimeout` | `600` | 閒置 600 秒（10 分鐘）後啟動回歸 |
| `IdleReturnFloor` | `3F` | 母樓層為 3F |
| `MapCodeFloorMapping` | `AA:1F,DD:3F`（待現場驗證實際對應） | 一廠範圍只 1F / 3F |

### 樓層範圍限制

- 跨樓層任務範圍：**1F ↔ 3F**（僅此兩層）
- `oTaskTypeRoute` 初始資料只塞 4 筆：
  - 1F→3F Transport
  - 3F→1F Transport
  - 1F→3F EmptyMove
  - 3F→1F EmptyMove

### 資料流規格表

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|----------|-----------|
| 讀車輛狀態 / 位置 | `oShuttle` 表 | `SQLData.Select_oShuttle()` | 跳過本次 Tick |
| 讀車輛 MapCode → Floor | `oShuttle.MapCode` + `MapCodeFloorMapping` config | 記憶體查表 | 視為「未知樓層」，跳過本次 Tick |
| 讀 TaskType 路由 | `oTaskTypeRoute` 表 | `SQLData.Select_oTaskTypeRoute(MoveType, FromFloor, ToFloor)` | 寫 Log Warning，跳過本次派發 |
| 寫 IDLE_RETURN 任務 | `oMission` + `oRequire` | `SQLData.Insert_oMission()` / `Insert_oRequire()` | 寫 Log Warning，下次 Tick 重試 |
| 寫 CROSS_FLOOR_DISPATCH 任務 | `oMission` + `oRequire` | `SQLData.Insert_oMission()` / `Insert_oRequire()` | 寫 Log Warning，下次 Tick 重試 |
| 更新 UpdateTime | `oShuttle.UpdateTime` | `SQLData.Update_oShuttleUpdateTime()` | 寫 Log，不影響流程 |
| 任務完成回呼 | CallBackAPI.AGVCallback() | 觸發 `Dispatch.CrossFloor?.OnXxxCompleted()` | 寫 Log Warning |

---

## 驗收標準

### 🧪 CooldownTracker（TDD）

- [ ] Given 未呼叫 RecordCompletion，When 呼叫 IsInCooldown("P1")，Then 回傳 false
- [ ] Given RecordCompletion("P1") 後立即查同 parent，When 呼叫 IsInCooldown("P1")，Then 回傳 true
- [ ] Given RecordCompletion("P1") 後查不同 parent，When 呼叫 IsInCooldown("P2")，Then 回傳 false
- [ ] Given 冷卻期超過設定秒數，When 呼叫 IsInCooldown，Then 回傳 false（過期自動失效）

### 🧪 CrossFloorManager（TDD）

- [ ] Given IDLE 車輛位於母樓層 3F，When Tick()，Then 不啟動回歸計時
- [ ] Given IDLE 車輛位於 1F 且計時未啟動，When Tick()，Then 啟動閒置計時
- [ ] Given IDLE 車輛位於 1F 且計時超過 IdleReturnTimeout，When Tick()，Then 產生 TaskSource='IDLE_RETURN' 的 oMission
- [ ] Given 計時中車輛收到新任務，When OnNewTaskArrived()，Then 重置計時
- [ ] Given 跨樓層待派任務且車輛在任務起點樓層，When SelectNextMission()，Then 直接回傳父任務
- [ ] Given 跨樓層待派任務且車輛不在任務起點樓層，When SelectNextMission()，Then 產生 TaskSource='CROSS_FLOOR_DISPATCH' 的預調度 oMission
- [ ] Given 冷卻期內同 parent 任務再次評估，When DecideNextCrossFloorAction()，Then 回傳 CooldownHit 並帶父任務
- [ ] Given 預調度執行中又來新任務，When OnNewTaskArrived()，Then 等預調度完成後重新評估

### 🧪 oTaskTypeRoute Repository（TDD）

- [ ] Given DB 有 1F→3F Transport 路由設定，When GetTaskType("Transport","1F","3F")，Then 回傳對應 TaskType
- [ ] Given DB 找不到對應路由，When GetTaskType()，Then 回傳 null 並寫 Log Warning
- [ ] Given UseFlag='N' 的路由，When GetTaskType()，Then 視為不存在，回傳 null

### 一般子任務（部署 / 整合）

- [ ] DB Migration SQL 在一廠資料庫執行成功，欄位 / 表結構符合預期
- [ ] HikAGV.config `<AGVSettings>` 4 個新屬性可正確讀取為 ConfigAGVModel
- [ ] Dispatch.Execute() 主循環每秒呼叫 CrossFloor.Tick()，無例外
- [ ] CallBackAPI.AGVCallback(end) 觸發 OnIdleReturnCompleted 與 OnCrossFloorDispatchCompleted
- [ ] WebGui 後台選單可看到「跨樓層 TaskType 路由管理」頁面，CRUD 功能正常
- [ ] 端對端：跨樓層車閒置 600 秒後自動回 3F；任務起點不在當前樓層時自動產生預調度任務
- [ ] 一廠 PortBindingMenu / 路線管理等其他既有功能不受影響（回歸測試）

### 部署檢查

- [ ] 全部 Deploy_*.sql 在 staging 環境執行通過
- [ ] HikAGV.config 與 FHtSetting.config 完成一廠版本配置
- [ ] WebGui 後台選單部署完成
