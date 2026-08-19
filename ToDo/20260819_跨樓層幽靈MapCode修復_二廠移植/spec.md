# 需求規格書 — 一廠跨樓層樓層判斷幽靈資料修復（自二廠移植）

## 建立日期
2026-08-19

## 主題與背景

二廠 2026-08-19 客訴：AGV 在 2F，SCP 建立 3F→1F 任務不執行，RCS 只見 3→1 任務、
無把車叫到 3F 的預調度，任務卡在 `OkFlag=R` 達 19 分鐘直到人工取消。

根因：`Dispatch.UpdateAGVStatus()` 逐張 MapCode 輪詢 RCS，查到一筆就立刻 `Update_oShuttle`，
而該 SQL 的 `where` 只有 `ShuttleId` 不含 MapCode，同輪多張地圖回報同一台車時後者無條件覆蓋前者。
輪詢順序由 `MapCodeFloorMapping` 的 key 順序決定，導致殘留的幽靈回報（座標凍結）勝出，
`CrossFloorManager` 讀到錯誤樓層而跳過預調度，直接把起點在別層的任務送 RCS。

二廠已修復（commit `dcd7633` / `10a1062` / `2b95c4e` / `2dad4d3`）。

### 一廠有完全相同的缺陷（已逐檔 diff 驗證）

以二廠修改前版本 `0a08fc8` 對比一廠 HEAD，去除換行符差異後：

| 檔案 | 差異 |
|------|------|
| `App_Start/CrossFloorManager.cs` | **0 行**（含 `null == null` 誤判缺陷） |
| `App_Start/CrossFloorDecision.cs` | **0 行** |
| `App_Start/CooldownTracker.cs` | **0 行** |
| `SQLData/SQLData.cs` | **0 行**（含無條件覆寫 MapCode 的 SQL） |
| `API/CallBackAPI.cs` | **0 行** |
| `Models/QueryAGVStatusModel.cs` | **0 行** |
| `App_Start/Dispatch.cs` | 6 行（僅註解措辭與換行排版），`UpdateAGVStatus` 邏輯 0 差異 |
| `App_Start/ElevatorPathCalculator.cs` | 33 行（註解／空白／BOM，二廠多一未被呼叫的 `GetElevatorExitStation`）；`GetFloor` 與 `IsCrossFloor` 完全相同 |
| `Models/ConfigModel.cs` | 57 行（一廠多一整段 SensorClear PLC 設定）；電梯相關設定完全相同 |

一廠現場條件同樣成立：`MapCodeFloorMapping="AA:1F,CC:3F"`（2 張圖，碰撞前提成立），
`CrossFloorShuttleId="3"`、`IdleReturnTimeout=600`、`IdleReturnFloor=3F`。
二廠是 DD(3F) 恆勝 BB(2F)，一廠則是 **CC(3F) 恆勝 AA(1F)**。

---

## 需求範圍

### 包含
1. 新增 `App_Start/ShuttleMapCodeResolver.cs`（逐字移植）
2. 改 `App_Start/Dispatch.cs` 的 `UpdateAGVStatus()`：整輪收集 → 讀 oShuttle 現值 → 裁決 → 每台車只寫一次
3. 改 `App_Start/CrossFloorManager.cs` 兩處：抽出 `SelectNextMissionForFloor` 樓層閘門、修正 `null == null` 誤判
4. 新增 `ShuttleMapCodeResolverTests.cs`（12 案例）、`CrossFloorManagerFloorTrustTests.cs`（10 案例），皆逐字移植
5. 兩個 csproj 補 `<Compile Include>`（net48 classic 不會自動納入新檔）

### 不包含
- **oMission 卡住看門狗**（二廠第 ③ 項）— 使用者已決定不做，一廠同步不做
- **一廠 `FHtSetting.config` 的 `StationFloorMapping` 修正**（見下方已知矛盾）— 需先與現場確認再處理
- 一廠獨有的 `PlcDevice.cs` / `StockSensorLinkManager.cs` — 跑在 `Global.asax.cs` 啟動的獨立執行緒，
  只碰 `oPort` 不碰 `oShuttle`，與本次移植面無交集
- `ElevatorPathCalculator.cs` — 本次不動（該檔為 UTF-8 無 BOM 且含 63 行中文，日後要改須先整檔轉 BOM）

---

## 限制條件

- 改動全部侷限在 `ACC/HikAGVWebAPI`，不動 SCP / svrPair / DB schema
- **不得整段覆蓋 csproj 的 ItemGroup**：一廠多 `PlcDevice.cs`、`StockSensorLinkManager.cs`、
  `StockSensorLinkManagerTests.cs`，整段覆蓋會刪掉一廠既有檔案
- **不覆蓋一廠既有註解與排版**：`Dispatch.cs:169-171` 的註解措辭與 Split 斷行是一廠寫法，
  強制統一只會製造無意義 diff
- 確認次數 N = 3（沿用二廠決策，程式常數）；衝突僅記 WARN，不上報 FHt
- 編碼：新增 `.cs` 一律 UTF-8 BOM。已確認本次要改的 `Dispatch.cs`、`CrossFloorManager.cs`、
  兩個 csproj **皆已有 BOM**（`efbbbf`），不需整檔轉換

### 已知矛盾（不在本次修復範圍，但須寫入交付說明）

一廠 `FHtSetting.config:31` 的 `StationFloorMapping="A:1F,B:1F,C:1F,K:3F,L:3F,M:3F"`
與 `ConfigModel.cs:292` 的 DefaultValue（含 `H:2F,I:3F,J:3F,K:4F,...`）**互相矛盾**，
其中 `K` 在現場設定是 3F、在 DefaultValue 是 4F。

單元測試以 `new ElevatorSettings()` 建構，走的是 **DefaultValue**，
因此測試通過只證明「決策邏輯正確」，**不證明一廠現場站點對照正確**。
交付時必須明確標註此但書，避免有人拿測試綠燈當現場驗證。

---

## 驗收標準

### AC1~AC8：`ShuttleMapCodeResolver`（與二廠同）
- [ ] **AC1** 本輪只有 1 筆回報 → 直接採用且 `HasConflict = false`
- [ ] **AC2** 同輪多筆、一筆座標有變動 → 取變動者且 `HasConflict = true`
- [ ] **AC3** 同輪多筆皆凍結或皆變動 → 依序黏著 待確認值 → 認可值 → 上一輪勝出值
- [ ] **AC4** MapCode 變更需連續 3 輪確認才寫入
- [ ] **AC5** 確認中途換目標 → pending 重設為 1，認可值不變
- [ ] **AC6** DB 現值與內部認可值不同（callback 剛寫過）→ 以 DB 為準並清空計數
- [ ] **AC7** 二廠 2026-08-19 事故序列回放 → 派發當下認可 BB 而非 DD
- [ ] **AC8** 衝突描述含各筆 mapCode／座標／判定結果／勝出原因

### AC9~AC11：`CrossFloorManager` 樓層閘門（與二廠同）
- [ ] **AC9** `currentFloor` 為 null／空 → 不得回傳 `SameFloor`
- [ ] **AC10** 樓層不可信 → `SelectNextMissionForFloor` 回 null 且不寫入 `CROSS_FLOOR_DISPATCH`
- [ ] **AC11** 樓層不可信但有系統任務（`CROSS_FLOOR_DISPATCH` / `IDLE_RETURN`）→ 仍回傳該系統任務

### AC12~AC14：一廠專屬
- [ ] **AC12** 一廠既有全部測試 0 失敗（移植前先取基準數）
- [ ] **AC13** `Update_oShuttle` 全 ACC 仍僅一處呼叫，且位於 `Resolve()` 之後（無旁路）
- [ ] **AC14** csproj 未刪除一廠既有的 `PlcDevice.cs` / `StockSensorLinkManager.cs` /
      `StockSensorLinkManagerTests.cs` 三個 `<Compile Include>`

### 交付說明須含（人工驗證清單，由現場填寫）
- [ ] 現場 `HikAGV.config` 的 `MapCodeFloorMapping` 實際值確認（repo 內為指向 localhost 的開發設定）
- [ ] 跑一輪 1F↔3F 跨樓層任務，確認 log 出現 `[MapCode] ... MapCode 變更生效：AA → CC` 且延遲 ≤ 3 秒
- [ ] 觀察 `[MapCode] ... 同輪被 N 張地圖回報` WARN 出現頻率（一廠 2 張圖，理論上罕見）
- [ ] 閒置 600 秒歸位 3F 流程照跑一次，確認 `Tick()` 未受影響
- [ ] oMission / ubMission 站點前綴普查：`select distinct left(BeginStation,1), left(EndStation,1)`，
      確認沒有 A/B/C/K/L/M 以外的前綴在跑跨樓層（若有屬設定缺漏，需補 `FHtSetting.config`）
