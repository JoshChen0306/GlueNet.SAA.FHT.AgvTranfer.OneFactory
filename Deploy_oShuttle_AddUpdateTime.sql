-- ============================================================
-- 跨樓層自動化機制 — Step 1
-- oShuttle 新增 UpdateTime 欄位
--
-- 用途：記錄 AGV 狀態最後更新時間，供 CrossFloorManager 判斷連線狀態。
--       Update_oShuttle 每秒更新此欄位，超過 60 秒未更新視為離線。
--
-- 執行日期：2026-04-__
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'oShuttle' AND COLUMN_NAME = 'UpdateTime'
)
BEGIN
    ALTER TABLE oShuttle ADD UpdateTime datetime NULL;
    PRINT 'oShuttle.UpdateTime 已新增';
END
ELSE
BEGIN
    PRINT 'oShuttle.UpdateTime 已存在，跳過';
END
GO

-- 驗證
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'oShuttle' AND COLUMN_NAME = 'UpdateTime';
GO
