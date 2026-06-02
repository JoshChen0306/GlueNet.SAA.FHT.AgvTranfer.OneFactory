# 需求規格書 — 一廠 FleetManager 閃退修復（自二廠移植）

## 主題與背景

一廠 OneFactory 的 FleetManager（`Dispatch/svrPair/cPair.cs`，host = `cTest.exe`）與二廠同根因閃退漏洞（已讀碼確認 2026-06-02）：
- `MainProcess` while 迴圈無 try/catch（背景執行緒 `mThread`）。
- 含資料/使用者輸入的 SQL 為字串拼接（INSERT oRequire 等 14 點）。
- 全 solution 無 `AppDomain.UnhandledException`。
→ oNeed 毒資料（WorkOrder/RackId 含單引號）→ SqlException → 背景執行緒終止 → 整個進程閃退，且毒資料持續存在會反覆閃退。

本案將二廠已完成並驗證的「三層防護 + 全檔 SQL 參數化」修法移植到一廠，並**嚴格保留一廠既有派送差異**（不回填二廠邏輯）。

## 需求範圍

### 包含
1. SqlHelper.cs Big5→UTF-8 BOM 轉碼（0 邏輯、獨立 commit）+ 參數化多載。
2. cPair.cs 三層防護：單筆隔離（資料類例外標 X / 連線類重試）+ 主迴圈保命 + 05 補印。
3. cPair.cs 全檔含輸入的拼接 SQL 參數化（14 點；Block IN-list 結構片段保留）。
4. cTest/Program.cs 全域攔截（AppDomain.UnhandledException + Application.ThreadException + WriteCrashLog）。
5. 新建測試專案內容（IntegrationTestBase + 韌性 + 參數化測試）。

### 不包含
- 一廠既有派送邏輯（L 區歸系統配對群、主迴圈停用 BtoA、無空板回收）→ **一律保留，不得改成二廠版**。
- SCP / 平板前端驗證（另議）。
- 既有未 commit 變更（ACC/.../Dispatch.cs、FHtSetting.config）→ 不碰。

## 限制條件

### 測試環境
- 一廠測試 DB：`127.0.0.1 / agvDB_1400004_1`（使用者確認本地端、可安全建刪）。
- 測試 base 防呆：**改用 DB 名白名單**（放行 `agvDB_1400004_1`），不沿用二廠「127.0.0.1 拒跑」（一廠測試庫正是 127.0.0.1）。
- 隔離：測試資料 `TaskSource='UTEST'` + 哨兵站號 A91/B92/A93/B94，TestCleanup 一律清除。

### 編碼
- SqlHelper.cs = Big5/No-BOM，必須整檔轉 UTF-8 BOM 後再改。
- cPair.cs / Program.cs 編碼動手前確認；新建檔 UTF-8 BOM。

### 例外分類（同二廠）
- 資料類（語法/截斷/約束 SqlException 錯誤碼集合）→ 隔離標 oNeed.AssignFlag='X'。
- 連線/暫時性 → 保留重試，不標 X。

## 驗收標準

### 🧪 單筆隔離（Layer B）
- [ ] Given oNeed 含一筆會在 INSERT oRequire 觸發資料類例外的毒筆置於前，When 呼叫 GenerateoRequireByoNeed()，Then 不向外拋例外
- [ ] Given 同上，Then 毒筆 oNeed.AssignFlag 標 X、後續正常筆仍成功產生 oRequire
- [ ] Given 毒資料觸發例外，Then log 含 ObjStation/EndStation/WorkOrder/RackId 與例外訊息

### 🧪 SQL 參數化正確性（Layer A）
- [ ] Given WorkOrder = O'Brien，When 處理，Then 不丟例外、oRequire WorkOrder 原值完整
- [ ] Given 注入字串（非破壞性 payload），Then 原值存入、oRequire 表仍存在
- [ ] Given RackId 含單引號，Then 原值完整、不丟例外

### 🧪 Happy path 回歸（Layer C）
- [ ] Given 正常配對 oNeed，Then 正確產生 oRequire、oNeed 標 Y、oPort 註冊 BgnToEnd（與改前行為一致）

### 一般項
- [ ] MainProcess while 外層 try/catch，任一步驟例外不終止進程
- [ ] cTest/Program.cs 已掛全域攔截（手動驗證）
- [ ] SqlHelper.cs UTF-8 BOM + 參數化多載；轉碼 commit 獨立
- [ ] cPair.cs 14 個含輸入 SQL 全參數化（Block IN-list 保留）；一廠派送差異未被改動
- [ ] 既有 cPairTests 仍通過（無回歸）；Build 0 error
