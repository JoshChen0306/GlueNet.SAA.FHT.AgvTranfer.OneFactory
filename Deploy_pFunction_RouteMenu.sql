-- =============================================
-- pFunction 功能選單 - 路線權限管理選單項目
-- 更新日期: 2026-01-09
-- 說明: 新增路線權限管理選單項目 (FunctionNo = 504)
-- =============================================

-- 1. 新增路線權限管理選單項目
IF NOT EXISTS (SELECT 1 FROM pFunction WHERE FunctionNo = 504)
BEGIN
    INSERT INTO pFunction (ColumnNo, RowNo, FunctionNo, FunctionType, FunctionEnglishName, FunctionChineseName, ControlFlag, WebUrl, WebIcon)
    VALUES (5, 4, 504, N'Maintain', N'Route Permission', N'路線權限管理', N'Y', N'/Route', N'fa-solid fa-route');
    PRINT 'Route permission menu (FunctionNo=504) added.';
END
ELSE
BEGIN
    PRINT 'Route permission menu already exists.';
END
GO

-- 2. 更新管理者群組的 FunctionGroup，加入 504
UPDATE pGroup 
SET FunctionGroup = CASE 
    WHEN FunctionGroup IS NULL OR FunctionGroup = '' THEN '504'
    WHEN CHARINDEX('504', FunctionGroup) = 0 THEN FunctionGroup + ',504'
    ELSE FunctionGroup
END,
ModifiedTime = CONVERT(VARCHAR(14), GETDATE(), 112) + REPLACE(CONVERT(VARCHAR(8), GETDATE(), 108), ':', '')
WHERE GroupId = '1';

PRINT 'Admin group FunctionGroup updated.';
GO

-- 3. 驗證結果
SELECT FunctionNo, FunctionChineseName, WebUrl FROM pFunction WHERE FunctionNo = 504;
SELECT GroupId, GroupName, FunctionGroup FROM pGroup WHERE GroupId = '1';
GO
