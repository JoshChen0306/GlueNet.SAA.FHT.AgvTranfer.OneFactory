-- ============================================================
-- 跨樓層自動化機制 — Step 2（環境保險）
-- oShuttle 新增 MapCode 欄位（若不存在）
--
-- 用途：記錄車輛當前所在 MapCode（AA/BB/DD/FF 等），由 RCS 即時回報。
--       供 CrossFloorManager 透過 MapCodeFloorMapping 反推樓層。
--
-- 備註：一廠 / 二廠既有環境此欄位早已存在（由更早期 commit 引入），
--       本檔主要作為新環境（staging / 客戶現場乾淨 DB）部署的保險閘。
--
-- 執行日期：2026-04-__
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'oShuttle' AND COLUMN_NAME = 'MapCode'
)
BEGIN
    ALTER TABLE oShuttle ADD MapCode nvarchar(20) NULL;
    PRINT 'oShuttle.MapCode 已新增';
END
ELSE
BEGIN
    PRINT 'oShuttle.MapCode 已存在，跳過（既有環境）';
END
GO

-- 驗證
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'oShuttle' AND COLUMN_NAME = 'MapCode';
GO
