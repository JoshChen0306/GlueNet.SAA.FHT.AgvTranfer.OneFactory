---
created: 2026-01-14
updated: 2026-01-14
tags: [AGV, MapCode, Bug Fix, Database, SQL, Hikvision RCS]
related: 
  - ../SQLData/SQLData.cs
  - ../App_Start/Dispatch.cs
  - ../../../../WebGui/SCP/Markdown/AGV_MapCode修復-Web查詢層.md
---

# AGV MapCode 修復 - 資料庫更新層

## 問題描述

當 AGV 在不同樓層間移動時，海康 RCS 會回報動態變化的 MapCode（例如從 1F 的 `AA` 變為 3F 的 `DD`），但資料庫的 `Update_oShuttle` 方法並未更新 MapCode 欄位，導致：

1. **資料庫 MapCode 過時**：AGV 實際在 3F，但資料庫仍顯示 1F 的 MapCode
2. **Web 畫面顯示失效**：即使 Web 查詢層已修復，仍因資料庫資料不正確而無法顯示 AGV

## MapCode 的真實本質

### ❌ 錯誤理解（修復前）
- MapCode 是靜態的，在資料庫初始化時設定
- AGV 的 MapCode 固定不變

### ✅ 正確理解
- **MapCode 是動態的**，代表 AGV **當前所在的樓層**
- AGV 移動時，海康 RCS 會即時更新 MapCode
- **必須同步更新到資料庫**，才能正確顯示 AGV 位置

## 解決方案

### 修改檔案
- [SQLData.cs](../SQLData/SQLData.cs)

### 實作內容

在 `Update_oShuttle` 方法的 UPDATE 語句中新增 `MapCode` 欄位：

```diff
  public void Update_oShuttle(AGVStatusData agvStatus)
  {
      string sSQL = $@"update oShuttle
                          set Battery = '{agvStatus?.battery}'
                             ,Status = '{agvStatus?.status}'
                             ,PosX = '{agvStatus?.posX.PadRight(6, '0')}'
                             ,PosY = '{agvStatus?.posY.PadRight(6, '0')}'
                             ,RobotDir = '{agvStatus?.robotDir}'
+                            ,MapCode = '{agvStatus?.mapCode}'
                        where ShuttleId = {agvStatus?.robotCode} ";
      mSql.WriteSqlByAutoOpen(sSQL);
  }
```

### 更新欄位清單

**修改前（5 個欄位）**：
- Battery
- Status
- PosX
- PosY
- RobotDir

**修改後（6 個欄位）**：
- Battery
- Status
- PosX
- PosY
- RobotDir
- ✅ **MapCode**（新增）

## 完整資料流程

### AGV 跨樓層移動場景

```
AGV 20106 從 1F 移動到 3F
  ↓
海康 RCS 回報狀態
  ↓
[Robot Code]: 20106
[Map Code]: DD  ← 從 AA 變成 DD
[Pos X]: 215000
[Pos Y]: 200000
  ↓
Dispatch.cs UpdateAGVStatus() 接收
  ↓
呼叫 Update_oShuttle(agvStatus)
  ↓
✅ 資料庫更新：
   UPDATE oShuttle SET
     MapCode = 'DD',      ← 現在會更新了！
     PosX = '215000',
     PosY = '200000',
     Battery, Status, RobotDir...
   WHERE ShuttleId = 20106
  ↓
Web 用戶切換到 3F
  ↓
CommonController.GetAgv("FHT1-3F")
GetMapCodeFromArea("FHT1-3F") → "DD"
  ↓
查詢: WHERE MapCode = 'DD'
  ↓
✅ 找到 AGV 20106（資料庫已更新為 DD）
  ↓
✅ 正確顯示在 3F 地圖上！
```

## 雙階段解決方案

### 為什麼需要兩個修正？

#### 階段 1：Web 查詢層（CommonController.cs）
**問題**：Web 區域代碼與海康 MapCode 不匹配  
**解決**：新增 MapCode 轉換方法  
**限制**：只有查詢時能找到 AGV，但 AGV 移動後資料庫 MapCode 不變

#### 階段 2：資料庫更新層（SQLData.cs）
**問題**：資料庫 MapCode 沒有隨 AGV 移動而更新  
**解決**：在 UPDATE 語句中新增 MapCode 欄位  
**效果**：資料庫即時反映 AGV 當前所在樓層

#### 兩者配合效果

| 情境 | 只有階段 1 | 只有階段 2 | 兩者配合 |
|------|-----------|-----------|---------|
| 初次查詢 | ✅ 能找到 | ❌ 找不到 | ✅ 能找到 |
| AGV 移動後 | ❌ 顯示錯誤樓層 | ❌ 找不到 | ✅ 正確顯示 |
| 跨樓層顯示 | ❌ 無法追蹤 | ❌ 無法追蹤 | ✅ 即時追蹤 |

## 驗證步驟

### 1. 資料庫即時監控

```sql
-- 持續監控 AGV MapCode 變化
SELECT ShuttleId, MapCode, PosX, PosY, Status, GETDATE() as QueryTime
FROM oShuttle
WHERE ShuttleId = 20106
ORDER BY ShuttleId;
```

### 2. 跨樓層移動測試

1. 派送 AGV 從 1F 到 3F 的任務
2. 觀察資料庫中 MapCode 是否從 `AA` 變為 `DD`
3. 確認 Web 畫面在 3F 能正確顯示該 AGV

### 3. LOG 檢查

查看 `Dispatch.cs` 的 LOG 輸出：

```log
Update AGV Data! [Robot Code] : 20106; [Map Code] : DD; [Pos X] : 215000; [Pos Y] : 200000
```

確認 MapCode 確實有動態變化。

## 相關文件

- [AGV MapCode 修復 - Web 查詢層](../../../../WebGui/SCP/Markdown/AGV_MapCode修復-Web查詢層.md)
- [完整討論紀錄](file:///C:/Users/user/.gemini/antigravity/brain/b8be6b4e-8b7d-4ce6-8f47-8f88fa62a51b/討論紀錄_AGV_MapCode顯示問題.md)

## 修改日期

- **2026-01-14**: 初次建立文檔，記錄資料庫更新層的 MapCode 同步實作
