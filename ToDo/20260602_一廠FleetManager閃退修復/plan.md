# 技術方案/移植計畫 — 一廠 FleetManager 閃退修復

> 來源：cross-factory-porter 比對二廠（已修復）vs 一廠（待修），兩廠各檔逐支讀完。

## 角色對應

| 角色 | 二廠（來源，已修復） | 一廠（目標） | 狀態 |
|---|---|---|---|
| 派送核心 | svrPair/cPair.cs (983) | svrPair/cPair.cs (685) | 待修 |
| DB 輔助 | svrPair/SqlHelper.cs (UTF-8) | svrPair/SqlHelper.cs (**Big5/No-BOM**, 249) | 待轉碼+多載 |
| host 進入點 | cTest/Program.cs (含攔截) | cTest/Program.cs (**無攔截**, 22) | 待加攔截 |
| 測試基座/韌性/參數化 | svrPairTests/3 檔 | **一廠缺，需新建** | 新建 |
| 測試 csproj | svrPairTests.csproj | 同名存在 (107) | 加 Compile 項 |

host 同為 cTest（WinExe/WinForms）；LogType 同為 `SAA_PUBLIC.LogType {Normal,Warnning,Error}`（反射確認）。

## 一廠必須保留的差異（不得回填二廠邏輯）
1. cPair.cs:168-199 switch：一廠把 `case "L"` 放「系統配對」群（二廠在平板群）。
2. cPair.cs:110-113 主迴圈刻意註解停用 `GenerateoNeedDataByMCSAsBtoA`（"工廠1修正：B區透過 Release 手動回送"）。
3. 一廠**無** CheckAndHandleEmptyPlateRecovery / FindFallbackEmptySlot（空板回收）。
→ C4 抽 ProcessSingleoNeed 時，把一廠既有 switch 原樣搬入，只加 try/catch 外殼。

## 一廠 cPair.cs 需參數化的 14 個 SQL 點（行號為移植前）

| # | 行號 | 方法 | 參數 |
|---|---|---|---|
| 1 | 55 | SettingBlockUseFlag | @uf,@blk |
| 7 | 295 | GenerateoNeedDataByMCSAsCtoABD(R流程) | @sn |
| 8 | 336 | ProcessoNeedToRequire | @obj |
| 9 | 351-352 | ProcessoNeedToRequire **INSERT oRequire(閃退點)** | @td,@obj,@end,@rack,@wo |
| 10 | 373 | CheckoPortBgnToEndIsNullAndUseFlagAsY | @s1,@s2 |
| 15 | 433-434 | GetoPort_PartNoTheSame(非X) | Block片段保留 + WorkOrder like @pat（"%"+PartNo+"%"）|
| 17 | 476 | InsertoMission(check) | @bgn,@end |
| 18 | 479-480 | InsertoMission **INSERT oMission** | @td,@bgn,@end,@rack,@wo |
| 19 | 490 | UpdateoRequireAssignFlag | @af,@td,@obj,@sn(int) |
| 20 | 498-499 | InsertoNeed | @obj,@rack,@wo,@end,@td |
| 21 | 506 | UpdateoNeedAssignFlag | @af,@obj,@end |
| 22 | 513 | UpdateoPortBgnToEnd | @bte(**BgnToEnd ?? ""**),@s1,@s2 |
| 23 | 520 | DeleteoNeedByAssignFlag | @obj,@end,@af |
| 24 | 527 | DeleteoRequireByOkFlag | @td,@obj,@sn(int),@end,@ok |
| 25 | 534 | DeleteoRequireByAssignFlag | @obj,@end,@af |
| 27 | 568 | RecyclingoRequireByOkFlag(P→NULL) | @end |

- 靜態不動：92,165,276,448,546,583,613,637,649,653。
- Block IN-list 片段保留（程式碼常數，非使用者輸入）：207,222,382-383,392-403,413-414,426-427 + #15 的 Block 部分。
- ⚠️ #22 `UpdateoPortBgnToEnd` 必須保留 `?? ""`（原拼接 null→空字串；改成 NULL 會影響 Recycling 取消註冊語意）。
- ⚠️ #19/#24 SerialNo 為 int → `SP("@sn", SerialNo)`。

## Commit 順序
C1 轉碼(0邏輯,獨立) → C2 SqlHelper 多載+AddParameters(clone) → C3 cPair 參數化 → C4 單筆隔離(保留一廠switch) → C5 主迴圈保命 → C6 host 攔截 → C7 測試(防呆改 DB名判斷)。每步 build 驗證。

## 風險
- 🔴 C4 須保留一廠 switch（L 在系統群、無空板回收），勿用二廠 switch。
- 🔴 C3 #22 `?? ""`、#15 混合、#19/#24 int 三處最易錯。
- 🔴 C7 測試防呆反轉：白名單放行 `agvDB_1400004_1`，不可照抄二廠「127.0.0.1 拒跑」否則整套測試靜默 Inconclusive。
- 🟡 C2 AddParameters 的 Clone 是防「already contained」關鍵（重試建新 command）。
- 🟡 C7 韌性測試毒筆觸發點依賴一廠 oNeed/oRequire RackId 欄長差，須重驗。

## de-risk 已完成（2026-06-02）
- LogType.Error/Warnning 在一廠可編譯（反射確認）。
- SqlHelper.cs 確認 Big5/No-BOM。
- 一廠測試 DB agvDB_1400004_1 @127.0.0.1 本地端可安全建刪（使用者確認）。
