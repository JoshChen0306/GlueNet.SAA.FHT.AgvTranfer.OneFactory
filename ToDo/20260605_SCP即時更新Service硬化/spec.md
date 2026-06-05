# 需求規格書 — SCP 即時更新 Service 硬化（SqlDependency + SignalR）

## 主題與背景

2026-06-05 客戶反映 SCP 新增任務後 UI 不即時更新、需手動 F5；經查 AGV/地圖/任務全部凍結，
代表整條「SqlDependency(Service Broker) → SignalR 推播」鏈路在客戶端失效。

當次已完成防護性修補（commit `a66a14c`）：
- `Hub.js` 加 `withAutomaticReconnect`（斷線自動重連）。
- `Service.cs` 的 `SqlDependency.Start` / `RegisterDependency` 加 try/catch + Serilog（broker 未啟用不再靜默失效）。

但 code-reviewer 審查 `WebGui/SCP/Services/Service.cs` 後判定 **FAIL**，發現 3 項 Critical + 2 項 Warning
**既有風險**（非當次改出，當次也未觸及）。本任務專責收乾淨這支 Service。

> 根因（客戶端 `is_broker_enabled`）屬環境設定，不在本任務範圍；本任務只處理程式碼健壯性。

## 需求範圍

### 包含（`WebGui/SCP/Services/Service.cs`）
1. `OnChange` 處理器加 `e.Type == SqlNotificationType.Change` 守衛，擋 `Invalid`/`Subscribe` 造成的無限重註冊迴圈。
2. 移除無界成長且從未被讀取的 `_dependency` List 洩漏點（或改為執行緒安全 + 重註冊前清理舊訂閱）。
3. 每個 `async void OnChange` 處理器 body 包 try/catch + Log（避免 `SendAsync` 例外打掛行程）。
4. `StopAsync` 的 `SqlDependency.Stop` 補對稱 try/catch + Log。
5. （次要）`_connectionString` null/empty 檢查，避免誤導為「broker 未啟用」。

### 不包含
- 客戶端 SQL Server Service Broker 啟用 / 帳號 `SUBSCRIBE QUERY NOTIFICATIONS` 權限（環境設定，另行處理）。
- SignalR / 前端 js 行為變更（`Hub.js` 已於 a66a14c 處理）。
- 6 條註冊 SQL 的欄位 / 商業邏輯變更。

## 限制條件

- `Service.cs` 編碼 = UTF-8 BOM（已確認），動手前再次確認，禁止局部混編。
- 純健壯性修補，不得改變既有 6 條通知查詢的語意與推播事件名稱（前端 `connection.on` 依賴）。
- 「移除 `_dependency` List」前須確認 SqlDependency 存活由 ADO.NET 內部靜態表維護，不依賴此 List。

## 驗收標準

### 無限重註冊迴圈防護
- [ ] 6 個 `OnChange` 處理器在重註冊 / 推播前皆判斷 `e.Type == SqlNotificationType.Change`
- [ ] 非 `Change`（`Invalid` / `Subscribe`）時記 Warning log 並**不**重註冊，無緊密迴圈

### 記憶體 / 執行緒安全
- [ ] `_dependency` 不再無界成長（移除，或重註冊前移除舊項並 `OnChange -=` 解除訂閱）
- [ ] 若保留集合，對其存取為執行緒安全（`ConcurrentBag` 或 `lock`）

### 例外不打掛行程
- [ ] 每個 `async void OnChange` body 以 try/catch 包覆，例外至少記 Warning log，不向外拋
- [ ] `StopAsync` 的 `Stop()` 以 try/catch 包覆並記 log

### 回歸
- [ ] SCP 專案 build 0 error
- [ ] 既有 6 個即時推播事件名稱不變（`SendTracChange` / `SendAgvChange` / `SendAgvStatusChange` / `SendTotalTaskChange` / `SendDispatchChange` / `SendMissionChange`）
- [ ] 無新增空 catch（每個 catch 至少 Warning log）
