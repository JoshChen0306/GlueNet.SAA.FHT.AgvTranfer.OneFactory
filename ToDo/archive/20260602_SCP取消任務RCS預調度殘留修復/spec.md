# 需求規格書：SCP 取消任務後 RCS 預調度殘留修復

## 主題與背景

客戶反映：在一廠 SCP 畫面按下「取消任務」後，RCS 端的**跨樓層預調度任務不會被刪除**，導致預調度殘留。二廠相同功能正常。

### 根因（已交叉比對確認，高信心）

一廠在移植二廠「方案 A」跨樓層機制時，**SCP 這條線漏移植**：

- 二廠「方案 A」設計（一廠 `CrossFloorManager.cs:465` 註解明載）：預調度任務**只寫 `oMission`、不寫 `oRequire`**，`BeginStation`/`EndStation` 是電梯等待點，並寫入 `ParentTaskDateTime` 關聯觸發它的 MCS 任務。取消 MCS 時須**靠 `ParentTaskDateTime` 連動**把預調度的 `oMission.OkFlag` 設成 `C`，ACC 的 `DeleteMission` 才會掃到並對 RCS 發 `CancelTask`。
- 一廠 **ACC 端已完整移植**（`DeleteMission` 會對 `CROSS_FLOOR_DISPATCH` 發 RCS 取消、`CallBackAPI` 有連動父任務、`CrossFloorManager` 建立預調度時有寫 `ParentTaskDateTime`、`SQLData.Insert_oMission` 有 INSERT 該欄位）。
- **唯獨 SCP 端三處仍是舊版**：`DispatchController.DeleteoNeed` 用 `beginStation`+`endStation` 比對、無連動；`oMission.cs` EF entity 缺 `ParentTaskDateTime`；`Dispatch.js` 取消按鈕傳起訖站。

### 故障鏈

使用者取消的 MCS 任務起訖站是真實站點，預調度 `oMission` 起訖站是電梯等待點，兩者不同 → 一廠 `DeleteoNeed` 用起訖站比對**永遠命中不到預調度** → 預調度 `OkFlag` 不會變 `C` → ACC `DeleteMission` 掃不到 → 不對 RCS 發 `CancelTask` → **RCS 預調度殘留**。二廠用 `ParentTaskDateTime` 連動就抓得到，故正常。

## 需求範圍

### 包含
- 將二廠 SCP 端「方案 A 連動取消」四處對齊移植到一廠：
  1. `WebGui/SCP/Models/oMission.cs` 補 `ParentTaskDateTime` 屬性
  2. `WebGui/SCP/Controllers/DispatchController.cs` 的 `DeleteoNeed` 改用 `taskDateTime` + `ParentTaskDateTime` 連動取消；`UpdateDispatch` 加回傳 `TaskDateTime` 與 `TaskType`
  3. `WebGui/SCP/Views/Dispatch/_DispatchPartial.cshtml` row 加 `data-taskdatetime` 與 `TaskType` 顯示欄位
  4. `WebGui/SCP/Views/Dispatch/Index.cshtml` 派送狀態表頭加一欄（任務種類）；`WebGui/SCP/wwwroot/js/Dispatch.js` 取消按鈕改傳 `taskDateTime`、status 欄位 index 改 `td:eq(5)`
- 部署前置：確認客戶一廠正式 DB 的 `oMission`/`ubMission` 已具備 `ParentTaskDateTime` 欄位（`Deploy_AddParentTaskDateTime.sql` 已在 repo，冪等可重跑）

### 不包含
- ACC 端任何改動（已移植完成，本次不動）
- 二廠程式碼任何改動
- 一廠地圖偏移問題（屬同資料夾另一議題，本任務不處理）
- 預調度寫入 `oRequire` 機制（方案 A 刻意不寫，維持現狀）

## 限制條件

- 前端範圍決議：**完全對齊二廠（含 TaskType 顯示欄位）**，使日後跨廠比對一致、現場可辨識任務種類。
- 測試策略決議：**不建 SCP.Tests 單元測試**。SCP（WebGui）無現成測試專案，mock EF Core DbContext 成本高；本次為搬移二廠實機已驗證程式碼，改以「diff 邏輯比對 + Build + 端對端手動驗證」作為 Gate，更貼近真實故障場景。
- 移植鐵律：兩廠不一致處不可照抄，重點盯：
  - `DbContext` 名稱（一廠 `agvDB_1400004Context`）
  - JS 欄位 index（`td:eq(4)` → `td:eq(5)`，須與 partial 實際欄位數一致）
  - `oRequire` 連動取消用 `TaskDateTime` 比對即可，**不需**加 `ParentTaskDateTime` 欄位（與二廠一致）
- 編碼：修改任何 `.cs`/`.cshtml`/`.js` 前先確認原檔編碼，避免混合編碼亂碼。

### 資料流確認

| 操作 | 資料來源 | 讀寫方式 | 失敗時行為 |
|------|---------|---------|----------|
| 取消任務（取得目標任務關聯） | DB `oMission`（含 `ParentTaskDateTime`） | EF `_DBContext.oMission.FirstOrDefault(m => m.TaskDateTime == taskDateTime)` | 查無 → 僅取消自身，不連動 |
| 連動取消 MCS + 預調度 | DB `oMission` / `oRequire` | EF `ExecuteUpdate(OkFlag = "C")`，包在 `BeginTransaction` | catch 記錄 Log，交易不 commit |
| RCS 實際取消預調度 | ACC 輪詢 `oMission` `OkFlag=='C'` → `hikAGV.CancelTask` | （ACC 既有，本次不動） | 跨樓層 fire-and-forget，強制清理本地 |
| 前端取得任務清單 | DB `oRequire`（透過 `UpdateDispatch`） | EF 查詢，需多回傳 `TaskDateTime`、`TaskType` | 無資料則清單空 |

## 驗收標準

- [ ] 客戶一廠正式 DB `oMission`、`ubMission` 皆已具備 `ParentTaskDateTime` 欄位（跑 `Deploy_AddParentTaskDateTime.sql` 末段驗證 SELECT 確認）
- [ ] `oMission.cs` EF entity 具備 `ParentTaskDateTime` 屬性，SCP 查詢 `oMission` 不丟例外
- [ ] `DeleteoNeed` 接收 `taskDateTime`，能透過 `ParentTaskDateTime` 將被取消的 MCS 與其關聯預調度的 `oMission.OkFlag` 皆設為 `C`
- [ ] 取消預調度時，能反向連動取消其父 MCS 任務（雙向連動，與二廠一致）
- [ ] 連動取消包在交易中，失敗時不 commit 且記錄 Log（無空 catch）
- [ ] 前端派送狀態清單顯示「任務種類」欄位（一般搬運／跨樓層預調度／歸位），row 帶 `data-taskdatetime`，取消按鈕送出 `taskDateTime`
- [ ] SCP 專案建置 0 error
- [ ] 移植四處與二廠逐行 diff，差異僅限環境差異（DbContext 名稱、欄位 index），邏輯一致
- [ ] 端對端驗證：測試環境觸發跨樓層 MCS 任務產生預調度，於 SCP 取消該 MCS 後 —
  - [ ] DB：該 MCS 與關聯預調度 `oMission.OkFlag` 皆為 `C`
  - [ ] ACC log：出現對預調度 TaskCode 的 `Send Cancel Task API` 與 RCS 回應
  - [ ] RCS：預調度任務已消失（反向驗證客戶反映現象已解除）
