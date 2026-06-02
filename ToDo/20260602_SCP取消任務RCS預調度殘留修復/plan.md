# 技術方案書：SCP 取消任務後 RCS 預調度殘留修復

## 選定方案概述

**對齊二廠（方案 A 連動取消）的 SCP 端移植**：將二廠已實機驗證正常的 SCP 四處改動搬移到一廠，使一廠 SCP 取消 MCS 任務時能透過 `ParentTaskDateTime` 連動取消其關聯的跨樓層預調度，補齊 ACC 端早已就緒的 RCS 取消鏈路。

## 架構設計

### 連動取消資料流（移植後）

```
[操作員] 在 SCP 取消任務
   │  前端送 taskDateTime（_DispatchPartial row 的 data-taskdatetime）
   ▼
[SCP] DispatchController.DeleteoNeed(taskDateTime)
   │  1. 查 oMission 取得 targetMission 與其 ParentTaskDateTime
   │  2. 組 cancelList：
   │       - 若被取消是預調度（有 Parent）→ 連動加入父 MCS
   │       - 若被取消是 MCS → 連動加入以它為 Parent 的預調度子任務
   │  3. 交易內對 cancelList 全部 oMission/oRequire 設 OkFlag = "C"
   ▼
[DB] oMission.OkFlag = 'C'（含預調度那筆）
   ▼
[ACC] Dispatch.cs 輪詢掃 OkFlag=='C' → DeleteMission
   │  對 CROSS_FLOOR_DISPATCH 發 hikAGV.CancelTask（fire-and-forget）
   ▼
[RCS] 預調度任務刪除 ✅
```

### 改動點對照（一廠現況 → 對齊二廠）

| # | 檔案 | 一廠現況 | 對齊後 |
|---|------|---------|--------|
| 1 | `Models/oMission.cs` | 無 `ParentTaskDateTime` | 補 `public string? ParentTaskDateTime { get; set; }` |
| 2 | `Controllers/DispatchController.cs` `DeleteoNeed` | 接 `beginStation`+`endStation`，無連動 | 接 `taskDateTime`，`ParentTaskDateTime` 雙向連動 + 交易 |
| 2 | `Controllers/DispatchController.cs` `UpdateDispatch` | 回傳不含 `TaskDateTime`/`TaskType` | 加回傳 `TaskDateTime` 與 `TaskType`（CROSS_FLOOR_DISPATCH→跨樓層預調度 / IDLE_RETURN→歸位 / 其他→一般搬運） |
| 3 | `Views/Dispatch/_DispatchPartial.cshtml` | `<tr>` 無 `data-taskdatetime`、無 TaskType 欄 | `<tr data-taskdatetime>` + 加 `@item.TaskType` 欄 |
| 4 | `Views/Dispatch/Index.cshtml` | 派送狀態表頭少一欄 | 表頭加「任務種類」 |
| 4 | `wwwroot/js/Dispatch.js` 取消按鈕 | 傳 `beginStation`/`endStation`、status `td:eq(4)` | 傳 `taskDateTime`、status `td:eq(5)` |

## 方案分析

1. **方案概述**：純移植二廠已驗證的 SCP 連動取消邏輯，不自創設計。
2. **架構設計**：SCP 只負責「在 DB 標記連動取消」，RCS 實際取消由 ACC 既有輪詢完成；職責不變、僅補齊 SCP 標記邏輯。
3. **優點**：與二廠完全對齊、行為已實機驗證；ACC 端零改動、風險面收斂在 SCP；解決客戶反映的預調度殘留。
4. **風險與缺點**：
   - 最大風險為**客戶 DB 是否已有 `ParentTaskDateTime` 欄位**（列為 P0 前置查證）。
   - 兩廠環境差異（DbContext 名稱、欄位 index）若照抄會出錯 → 以逐行 diff 把關。
   - 前端 status 欄位 index 由 `td:eq(4)`→`td:eq(5)`，須與 partial 欄位數同步，否則狀態判斷錯位（雖該變數目前被註解未使用，仍須對齊避免日後踩雷）。
5. **技術債評估**：對齊後兩廠 SCP 此區一致，降低日後跨廠維護成本；無新增技術債。
6. **建議適用情境**：來源廠（二廠）功能已驗證正常、目標廠（一廠）ACC 已就緒、僅 SCP 漏移植的補齊場景 —— 正是本案。

## 被捨棄的方案

- **方案 B：一廠自行改寫 `DeleteoNeed`，仍用起訖站但額外查預調度**
  捨棄原因：預調度起訖站是電梯等待點，無法用使用者取消的 MCS 起訖站關聯；自創邏輯偏離二廠、無法共用驗證成果、易再出錯。

- **方案 C：建 SCP.Tests + mock EF DbContext 後再改**
  捨棄原因：SCP 無現成測試專案，mock DbContext 成本高；本案為搬移已驗證程式碼，端對端手動驗證（DB/ACC log/RCS 三層）比單元測試更貼近真實故障場景。改以 diff + Build + 端對端為 Gate。
