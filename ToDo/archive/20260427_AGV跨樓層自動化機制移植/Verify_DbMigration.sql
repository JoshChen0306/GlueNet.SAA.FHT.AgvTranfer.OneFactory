-- ============================================================
-- 跨樓層自動化機制 — DB Migration 驗收
-- 對應 5 支 Deploy SQL 部署完成後執行
-- 純讀，可重複執行
-- ============================================================

PRINT '======== Section 1：欄位 / 表 存在性矩陣 ========';

SELECT Item, Result FROM (
    SELECT 1 AS Ord, 'oShuttle.UpdateTime' AS Item,
           CASE WHEN EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='oShuttle' AND COLUMN_NAME='UpdateTime') THEN 'PASS' ELSE 'FAIL' END AS Result
    UNION ALL
    SELECT 2, 'oShuttle.MapCode',
           CASE WHEN EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='oShuttle' AND COLUMN_NAME='MapCode') THEN 'PASS' ELSE 'FAIL' END
    UNION ALL
    SELECT 3, 'oMission.ParentTaskDateTime',
           CASE WHEN EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='oMission' AND COLUMN_NAME='ParentTaskDateTime') THEN 'PASS' ELSE 'FAIL' END
    UNION ALL
    SELECT 4, 'ubMission.ParentTaskDateTime',
           CASE WHEN EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ubMission' AND COLUMN_NAME='ParentTaskDateTime') THEN 'PASS' ELSE 'FAIL' END
    UNION ALL
    SELECT 5, 'oTaskTypeRoute table',
           CASE WHEN EXISTS(SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID(N'[dbo].[oTaskTypeRoute]') AND type='U') THEN 'PASS' ELSE 'FAIL' END
    UNION ALL
    SELECT 6, 'pFunction.FunctionNo=506',
           CASE WHEN EXISTS(SELECT 1 FROM pFunction WHERE FunctionNo=506) THEN 'PASS' ELSE 'FAIL' END
    UNION ALL
    SELECT 7, 'pGroup(GroupId=1).FunctionGroup contains 506',
           CASE WHEN EXISTS(SELECT 1 FROM pGroup WHERE GroupId='1' AND CHARINDEX('506', FunctionGroup) > 0) THEN 'PASS' ELSE 'FAIL' END
) t ORDER BY Ord;

PRINT '';
PRINT '======== Section 2：新欄位實際型別 ========';

SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE,
       CHARACTER_MAXIMUM_LENGTH AS MaxLen, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE (TABLE_NAME='oShuttle' AND COLUMN_NAME IN ('UpdateTime', 'MapCode'))
   OR (TABLE_NAME IN ('oMission', 'ubMission') AND COLUMN_NAME='ParentTaskDateTime')
ORDER BY TABLE_NAME, COLUMN_NAME;

PRINT '';
PRINT '======== Section 3：oTaskTypeRoute 4 筆初始資料 ========';

SELECT MoveType, FromFloor, ToFloor, TaskType, UseFlag, Remark
FROM oTaskTypeRoute
ORDER BY MoveType, FromFloor, ToFloor;

PRINT '';
PRINT '======== Section 4：pFunction(506) 與 pGroup(1) ========';

SELECT FunctionNo, FunctionEnglishName, FunctionChineseName, WebUrl
FROM pFunction WHERE FunctionNo=506;

SELECT GroupId, FunctionGroup
FROM pGroup WHERE GroupId='1';
