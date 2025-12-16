# 4F 出貨區 (L) 到 3F 品檢區 (I) 派送流程

## 1. 需求說明
(Scope)
實作從 4F 出貨區 (L) 派送至 3F 品檢區 (I) 的功能。
- **起點**：4F 出貨區 (Block: L)
- **終點**：3F 品檢區 (Block: I)
- **工單**：需要輸入 (比照跨樓層或特殊製程邏輯)

## 2. 現有架構分析
(Analysis)
- **Dispatch.js**: 負責前端派送邏輯，目前未包含 L 區的處理。
- **cPair.cs**: 負責後端派送需求產生，目前未包含 L 區的已處理 case。
- **Mapping**: `L` -> 3F -> `I`

## 3. Proposed Changes

### Frontend
#### [MODIFY] Dispatch.js
路徑: `WebGui/SCP/wwwroot/js/Dispatch.js`

1.  **Station Select Logic**: 在 `UpdateDispatch` 函數中，增加 `area == "L"` 的處理。
    *   自動選擇終點區域為 "I"。
    *   篩選 "I" 區空架 (`HaveFlag = 0`)。
2.  **Filter Logic**: 修改 `filterEndStationOptions`，增加 `case "L":`，呼叫 `filterEndStationOptions("I", "0")`。

```javascript
// Dispatch.js logic snippet
case "L":
    autoSelectEndStation("I", "0", "N");  // 4F出貨區 -> 3F品檢區
    break;

// ...

case "L":
    filterEndStationOptions("I", "0");
    break;
```

### Backend
#### [MODIFY] cPair.cs
路徑: `Dispatch/svrPair/cPair.cs`

在 `GenerateoRequireByoNeed` 方法的 switch case 中加入 `L`。

```csharp
case "H":   // 2F 成型後 -> 4F 烘烤前入貨區
case "L":   // 4F 出貨區 -> 3F 品檢區  <-- NEW
    WriteLog("05.處理平板配對");
    ProcessoNeedToRequire(dr["ObjStation"].ToString().Substring(0, 1), dr);
    break;
```

## 4. Verification Plan
(Verification)
1.  重新編譯並執行。
2.  開啟 Web GUI，進入「儲架設定」，確認 I 區和 L 區都有站點資料 (已完成 I 區設定，需確認 L 區)。
3.  進入「任務派送」，選擇 4F。
4.  選擇起點區域 (L)。
5.  確認系統自動帶出 I 區的空架作為終點。
6.  提交派送任務。
7.  Check Database `oNeed` table for new entry.
