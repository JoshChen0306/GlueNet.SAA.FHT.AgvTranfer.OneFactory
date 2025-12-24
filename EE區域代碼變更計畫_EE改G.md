# EE 區域代碼變更計畫（EE → G）

## 變更目的

將 **1F 電梯暫存區** 的區域代碼從 `EE` 改為 `G`，避免雙字母代碼造成的問題：

- 現有邏輯使用 `Substring(0, 1)` 取首字母判斷區域
- `EE` 會被誤判為 `E`（暫存區），可能導致邏輯混淆
- 單一字母代碼 `G` 可確保一致性

---

## 受影響範圍

| 類別 | 檔案/位置 | 修改內容 |
|------|-----------|----------|
| 資料庫 | `oPort` 表 | StationNo、Block 欄位 |
| 設定檔 | `appsettings.json` | Area、FloorArea 區段 |
| 前端 | `Dispatch.js` | validAreas、releaseAreas、autoSelectEndStation |
| 後端 | `DispatchController.cs` | Release 方法區域判斷 |
| 後端 | `cPair.cs` | switch case 註解 |

---

## 實作步驟

### 1. 資料庫變更

> [!CAUTION]
> 執行前請先備份資料庫！

```sql
-- 備份原始資料
SELECT * INTO oPort_Backup_EE FROM oPort WHERE Block = 'EE' OR StationNo LIKE 'EE%'

-- 更新站點編號（EE1 → G1, EE2 → G2, ...）
UPDATE oPort 
SET StationNo = 'G' + SUBSTRING(StationNo, 3, LEN(StationNo))
WHERE StationNo LIKE 'EE%'

-- 更新區域代碼
UPDATE oPort SET Block = 'G' WHERE Block = 'EE'

-- 驗證
SELECT * FROM oPort WHERE Block = 'G'
```

---

### 2. 設定檔修改

#### [MODIFY] appsettings.json

```diff
  "Area": {
-   "1F 電梯暫存區(Elevator staging area)": "EE",
+   "1F 電梯暫存區(Elevator staging area)": "G",
  },
  "FloorArea": {
    "1F": [
      "A",
      "C",
      "D",
      "F",
-     "EE"
+     "G"
    ],
```

---

### 3. 前端修改

#### [MODIFY] Dispatch.js

**3.1 修改 validAreas**
```diff
- var validAreas = ['M', 'T', 'O', 'P', 'S', 'N', 'J', 'E'];
+ var validAreas = ['M', 'T', 'O', 'P', 'S', 'N', 'J', 'G'];
```

**3.2 修改 releaseAreas**
```diff
- var releaseAreas = ['O', 'P', 'S', 'N', 'E'];
+ var releaseAreas = ['O', 'P', 'S', 'N', 'G'];
```

**3.3 修改 openStationLotModal 區域判斷**
```diff
- if (stationArea === 'E') {
+ if (stationArea === 'G') {
```

**3.4 修改 autoSelectEndStation 呼叫**
```diff
  case "J":
-     autoSelectEndStation("EE", "0", "N");
+     autoSelectEndStation("G", "0", "N");
      break;
- case "EE":
+ case "G":
      autoSelectEndStation("J", "0", "N");
      break;
```

---

### 4. 後端修改

#### [MODIFY] DispatchController.cs

**4.1 修改 Release 方法**
```diff
- if (stationArea == "E")
+ if (stationArea == "G")
```

---

### 5. 後端核心邏輯

#### [MODIFY] cPair.cs

更新註解（邏輯不需修改，因為 G.Substring(0,1) = "G"）：
```diff
+ case "G":   // ★★★ 新增：1F電梯暫存區 → J區（3F插針室）：Release 回送空板 ★★★
      WriteLog("05.處理系統配對");
```

---

## 驗證步驟

1. **資料庫驗證**
   ```sql
   SELECT COUNT(*) FROM oPort WHERE Block = 'G'  -- 應有站點
   SELECT COUNT(*) FROM oPort WHERE Block = 'EE' -- 應為 0
   ```

2. **前端驗證**
   - 3F 樓層選擇 → J 區起點 → 終點應自動選擇 G 區

3. **Release 驗證**
   - 點擊 G 區站點 → 標記空板 → Release → 應派車到 J 區

---

## 風險與注意事項

> [!WARNING]
> - 若有進行中的任務使用 EE 區，需等待任務完成後再執行變更
> - 變更後舊的 log 記錄中仍會顯示 EE，不影響功能
