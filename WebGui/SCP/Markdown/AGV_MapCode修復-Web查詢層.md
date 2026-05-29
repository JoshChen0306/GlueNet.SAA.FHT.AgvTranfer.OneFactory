---
created: 2026-01-14
updated: 2026-01-14
tags: [AGV, MapCode, Bug Fix, ASP.NET Core, Entity Framework, Hikvision RCS]
related: 
  - ../Controllers/CommonController.cs
  - ../appsettings.json
  - ../../ACC/HikAGVWebAPI/HikAGVWebAPI/SQLData/SQLData.cs
---

# AGV MapCode 顯示問題修復紀錄

> ⚠️ **2026-05-29 更新註記**：客戶端海康 RCS 已將 **3F 的 MapCode 由 `DD` 改為 `CC`**。
> 目前實際生效設定為 `MapCodeMapping.FHT1-3F = "CC"`（appsettings.json）與 `MapCodeFloorMapping="AA:1F,CC:3F"`（HikAGV.config）。
> 本文以下內文中的 `DD` 為 2026-01-14 撰寫時的歷史值，僅保留作為當時除錯實錄，不再代表現行設定。

## 問題描述

AGV 車輛無法在 Web 地圖上正確顯示，原因為 Web 前端使用的區域代碼（`FHT1-1F`, `FHT1-2F`, `FHT1-3F`, `FHT1-4F`）與海康 RCS 系統的 MapCode（`AA`, `BB`, `DD`, `FF`）不匹配。

### MapCode 對應關係

| 海康 MapCode | 樓層 | Web 區域代碼 |
|-------------|------|-------------|
| AA | 1F | FHT1-1F |
| BB | 2F | FHT1-2F |
| DD | 3F | FHT1-3F |
| FF | 4F | FHT1-4F |

## 根本原因

### 問題 1：查詢層不匹配
用戶在 Web 介面選擇「1F」時，系統查詢 `WHERE MapCode = 'FHT1-1F'`，但資料庫中 AGV 的 MapCode 為 `AA`，導致查詢結果為空。

### 問題 2：MapCode 未即時更新
當 AGV 從 1F 移動到 3F 時，海康 RCS 會回報 MapCode 從 `AA` 變為 `DD`，但資料庫的 `Update_oShuttle` 方法並未更新 MapCode 欄位，導致資料庫中的位置資訊與實際不符。

## 解決方案

### 階段 1：Web 查詢層 MapCode 轉換

#### 修改檔案
- [appsettings.json](../appsettings.json)
- [CommonController.cs](../Controllers/CommonController.cs)

#### 實作內容

**1. 新增 MapCode 對應配置**

在 `appsettings.json` 新增：

```json
"MapCodeMapping": {
  "FHT1-1F": "AA",
  "FHT1-2F": "BB",
  "FHT1-3F": "DD",
  "FHT1-4F": "FF"
}
```

**2. 新增轉換方法**

在 `CommonController.cs` 新增 `GetMapCodeFromArea()` 方法：

```csharp
/// <summary>
/// 將 Web 區域代碼轉換為海康 MapCode
/// </summary>
private string GetMapCodeFromArea(string area)
{
    var mapping = _configuration.GetSection("MapCodeMapping").Get<Dictionary<string, string>>();
    if (mapping != null && mapping.ContainsKey(area))
    {
        return mapping[area];
    }
    // 如果找不到映射，返回原始 area（向後兼容）
    return area;
}
```

**3. 更新查詢邏輯**

修改 `GetAgv(string area)` 方法：

```csharp
private List<oShuttle> GetAgv(string area)
{
    // 將 Web 區域代碼轉換為海康 MapCode
    string mapCode = GetMapCodeFromArea(area);
    
    List<oShuttle> AgvPositions = _DBContext.oShuttle.Where(x => x.MapCode == mapCode).ToList();
    foreach (var item in AgvPositions)
    {
        // 使用原始 area 進行座標轉換
        item.PosX = ConvertX(item.PosX, area);
        item.PosY = ConvertY(item.PosY, area);
    }
    return AgvPositions;
}
```

### 階段 2：資料庫更新層 MapCode 同步

詳見：[../../ACC/HikAGVWebAPI/HikAGVWebAPI/Markdown/AGV_MapCode修復-資料庫更新.md](../../ACC/HikAGVWebAPI/HikAGVWebAPI/Markdown/AGV_MapCode修復-資料庫更新.md)

## 技術要點

### 設計原則

1. **職責分離**：MapCode 用於查詢，area 用於座標轉換
2. **配置驅動**：對應關係集中在 `appsettings.json` 維護
3. **向後兼容**：找不到映射時返回原值，不破壞舊邏輯

### 資料流程

```
用戶選擇 1F
  ↓
area = "FHT1-1F"
  ↓
GetMapCodeFromArea("FHT1-1F") → "AA"
  ↓
查詢: WHERE MapCode = 'AA'
  ↓
找到 AGV
  ↓
使用 area = "FHT1-1F" 查找座標轉換設定
  ↓
ConvertX/ConvertY 轉換座標
  ↓
正確顯示在地圖上
```

## 驗證步驟

### 1. 資料庫檢查

```sql
SELECT ShuttleId, MapCode, Status, PosX, PosY, Battery 
FROM oShuttle
ORDER BY ShuttleId;
```

確認 AGV 的 MapCode 為 `AA`, `BB`, `DD`, `FF`。

### 2. Web 測試

1. 啟動 Web 應用程式
2. 切換到不同樓層（1F, 3F）
3. 確認對應樓層的 AGV 正確顯示

### 3. 跨樓層移動測試

1. 派送跨樓層任務
2. 觀察 AGV 移動時 MapCode 是否正確更新
3. 確認 Web 畫面能即時反映 AGV 所在樓層

## 相關文件

- [AGV MapCode 修復 - 資料庫更新](../../ACC/HikAGVWebAPI/HikAGVWebAPI/Markdown/AGV_MapCode修復-資料庫更新.md)
- [完整討論紀錄](file:///C:/Users/user/.gemini/antigravity/brain/b8be6b4e-8b7d-4ce6-8f47-8f88fa62a51b/討論紀錄_AGV_MapCode顯示問題.md)

## 修改日期

- **2026-01-14**: 初次建立文檔，記錄 Web 查詢層的 MapCode 轉換實作
