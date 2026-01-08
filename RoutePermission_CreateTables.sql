-- =============================================
-- 路線權限管理系統 - 資料表建立腳本
-- 建立日期: 2026-01-08
-- =============================================

-- 1. 建立 pRoute 路線定義表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[pRoute]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[pRoute] (
        [RouteId]      NVARCHAR(30) NOT NULL PRIMARY KEY,
        [RouteName]    NVARCHAR(50) NOT NULL,
        [SourceFloor]  NVARCHAR(10) NULL,
        [TargetFloor]  NVARCHAR(10) NULL,
        [RouteType]    NVARCHAR(20) NULL,
        [SourceAreas]  NVARCHAR(100) NULL,
        [TargetAreas]  NVARCHAR(100) NULL,
        [ControlFlag]  NVARCHAR(1) DEFAULT 'Y',
        [SortOrder]    INT DEFAULT 0,
        [DispatchMode] NVARCHAR(20) DEFAULT 'DISPATCH'  -- DISPATCH=一般派送, RELEASE=Release專用
    );
    PRINT 'Table pRoute created successfully.';
END
ELSE
BEGIN
    -- 如果資料表已存在，檢查是否有 DispatchMode 欄位
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[pRoute]') AND name = 'DispatchMode')
    BEGIN
        ALTER TABLE [dbo].[pRoute] ADD [DispatchMode] NVARCHAR(20) DEFAULT 'DISPATCH';
        PRINT 'DispatchMode column added to pRoute.';
    END
    PRINT 'Table pRoute already exists.';
END
GO

-- 2. 建立 pUserRoute 使用者路線權限表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[pUserRoute]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[pUserRoute] (
        [UserId]  NVARCHAR(20) NOT NULL,
        [RouteId] NVARCHAR(30) NOT NULL,
        PRIMARY KEY ([UserId], [RouteId])
    );
    PRINT 'Table pUserRoute created successfully.';
END
ELSE
BEGIN
    PRINT 'Table pUserRoute already exists.';
END
GO

-- 3. 插入路線定義資料（客戶需求 7 條）
-- DispatchMode: DISPATCH=一般派送（顯示在派送頁面）, RELEASE=Release專用（只在Release功能）

-- 1F → 3F 鑽針區: Release 專用（從 G 放空板到 J）
IF NOT EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_1F_3F')
BEGIN
    INSERT INTO pRoute (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_1F_3F', N'1F → 3F 鑽針區', '1F', '3F', 'CROSS_FLOOR', 'G', 'J', 'Y', 1, 'RELEASE');
END
ELSE
BEGIN
    UPDATE pRoute SET DispatchMode = 'RELEASE' WHERE RouteId = 'ROUTE_1F_3F';
END

-- 2F 成型區運輸: 一般派送
IF NOT EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_2F_FORMING')
BEGIN
    INSERT INTO pRoute (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_FORMING', N'2F 成型區運輸', '2F', '2F', 'INTERNAL', 'MT,Q,R', 'O,P,S,N', 'Y', 2, 'DISPATCH');
END
ELSE
BEGIN
    UPDATE pRoute SET SourceAreas = 'MT,Q,R', DispatchMode = 'DISPATCH' WHERE RouteId = 'ROUTE_2F_FORMING';
END

-- 2F 雷雕區 → Vcut: 一般派送（只能派送 V Cut 待加工物料）
IF NOT EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_2F_VCUT')
BEGIN
    INSERT INTO pRoute (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_VCUT', N'2F 雷雕區 → Vcut', '2F', '2F', 'INTERNAL', 'MT', 'T', 'Y', 3, 'DISPATCH');
END
ELSE
BEGIN
    UPDATE pRoute SET SourceAreas = 'MT', TargetAreas = 'T', DispatchMode = 'DISPATCH' WHERE RouteId = 'ROUTE_2F_VCUT';
END

-- 3F 鑽針區 → 1F: 一般派送（從 J 派送物料到 G）
IF NOT EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_3F_1F')
BEGIN
    INSERT INTO pRoute (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_3F_1F', N'3F 鑽針區 → 1F', '3F', '1F', 'CROSS_FLOOR', 'J', 'G', 'Y', 4, 'DISPATCH');
END
ELSE
BEGIN
    UPDATE pRoute SET DispatchMode = 'DISPATCH' WHERE RouteId = 'ROUTE_3F_1F';
END

-- 3F 品檢區 → 4F: 一般派送
IF NOT EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_3F_4F')
BEGIN
    INSERT INTO pRoute (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_3F_4F', N'3F 品檢區 → 4F', '3F', '4F', 'CROSS_FLOOR', 'I', 'L', 'Y', 5, 'DISPATCH');
END
ELSE
BEGIN
    UPDATE pRoute SET DispatchMode = 'DISPATCH' WHERE RouteId = 'ROUTE_3F_4F';
END

-- 2F 成型後 → 4F: 一般派送
IF NOT EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_2F_4F')
BEGIN
    INSERT INTO pRoute (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_4F', N'2F 成型後 → 4F', '2F', '4F', 'CROSS_FLOOR', 'H', 'K', 'Y', 6, 'DISPATCH');
END
ELSE
BEGIN
    UPDATE pRoute SET DispatchMode = 'DISPATCH' WHERE RouteId = 'ROUTE_2F_4F';
END

-- 4F → 3F 品檢區: 一般派送
IF NOT EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_4F_3F')
BEGIN
    INSERT INTO pRoute (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_4F_3F', N'4F → 3F 品檢區', '4F', '3F', 'CROSS_FLOOR', 'L', 'I', 'Y', 7, 'DISPATCH');
END
ELSE
BEGIN
    UPDATE pRoute SET DispatchMode = 'DISPATCH' WHERE RouteId = 'ROUTE_4F_3F';
END

PRINT 'Route definitions inserted/updated with DispatchMode.';
GO

-- 4. 為管理員（UserId = '1'）設定所有路線權限
IF NOT EXISTS (SELECT 1 FROM pUserRoute WHERE UserId = '1' AND RouteId = 'ROUTE_1F_3F')
    INSERT INTO pUserRoute (UserId, RouteId) VALUES ('1', 'ROUTE_1F_3F');

IF NOT EXISTS (SELECT 1 FROM pUserRoute WHERE UserId = '1' AND RouteId = 'ROUTE_2F_FORMING')
    INSERT INTO pUserRoute (UserId, RouteId) VALUES ('1', 'ROUTE_2F_FORMING');

IF NOT EXISTS (SELECT 1 FROM pUserRoute WHERE UserId = '1' AND RouteId = 'ROUTE_2F_VCUT')
    INSERT INTO pUserRoute (UserId, RouteId) VALUES ('1', 'ROUTE_2F_VCUT');

IF NOT EXISTS (SELECT 1 FROM pUserRoute WHERE UserId = '1' AND RouteId = 'ROUTE_3F_1F')
    INSERT INTO pUserRoute (UserId, RouteId) VALUES ('1', 'ROUTE_3F_1F');

IF NOT EXISTS (SELECT 1 FROM pUserRoute WHERE UserId = '1' AND RouteId = 'ROUTE_3F_4F')
    INSERT INTO pUserRoute (UserId, RouteId) VALUES ('1', 'ROUTE_3F_4F');

IF NOT EXISTS (SELECT 1 FROM pUserRoute WHERE UserId = '1' AND RouteId = 'ROUTE_2F_4F')
    INSERT INTO pUserRoute (UserId, RouteId) VALUES ('1', 'ROUTE_2F_4F');

IF NOT EXISTS (SELECT 1 FROM pUserRoute WHERE UserId = '1' AND RouteId = 'ROUTE_4F_3F')
    INSERT INTO pUserRoute (UserId, RouteId) VALUES ('1', 'ROUTE_4F_3F');

PRINT 'Admin user route permissions set.';
GO

-- 5. 新增路線權限管理選單項目到 pFunction（歸類在權限管理底下）
-- 注意：FunctionNo 503 已被「用戶資料維護」使用，改用 504
IF NOT EXISTS (SELECT 1 FROM pFunction WHERE FunctionNo = 504)
BEGIN
    INSERT INTO pFunction (ColumnNo, RowNo, FunctionNo, FunctionType, FunctionEnglishName, FunctionChineseName, ControlFlag, WebUrl, WebIcon)
    VALUES (5, 4, 504, N'Maintain', N'Route Permission', N'路線權限管理', N'Y', N'/Route', N'fa-solid fa-route');
    PRINT 'Route permission menu item added.';
END
ELSE
BEGIN
    PRINT 'Route permission menu item already exists.';
END
GO

-- 6. 更新管理者群組的 FunctionGroup，加入 504（路線權限管理）
-- 如果 FunctionGroup 不包含 504，則加入
UPDATE pGroup 
SET FunctionGroup = CASE 
    WHEN FunctionGroup IS NULL OR FunctionGroup = '' THEN '504'
    WHEN CHARINDEX('504', FunctionGroup) = 0 THEN FunctionGroup + ',504'
    ELSE FunctionGroup
END,
ModifiedTime = CONVERT(VARCHAR(14), GETDATE(), 112) + REPLACE(CONVERT(VARCHAR(8), GETDATE(), 108), ':', '')
WHERE GroupId = '1';

PRINT 'Admin group FunctionGroup updated to include route permission menu.';
GO
