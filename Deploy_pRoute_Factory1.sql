-- =============================================
-- pRoute 工廠1路線定義 - 部署腳本
-- 更新日期: 2026-01-13
-- 說明: 工廠1專用的六條派送路線定義
-- =============================================

-- 1. 建立 pRoute 表（如不存在）
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
        [DispatchMode] NVARCHAR(20) DEFAULT 'DISPATCH'
    );
    PRINT 'Table pRoute created.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[pRoute]') AND name = 'DispatchMode')
    BEGIN
        ALTER TABLE [dbo].[pRoute] ADD [DispatchMode] NVARCHAR(20) DEFAULT 'DISPATCH';
    END
END
GO

-- 2. 清空現有二廠路線資料（可選，建議先備份）
-- DELETE FROM pRoute WHERE RouteId LIKE 'ROUTE_%';
-- DELETE FROM pUserRoute WHERE RouteId LIKE 'ROUTE_%';

-- =============================================
-- 工廠1路線定義（6條路線）
-- =============================================

-- 路線1: A區→K區上料（人工派送，跨樓層 1F→3F）
-- 終點選擇邏輯：K區優先，K滿則派M區
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_A_TO_K' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'A區→K區上料', SourceFloor = '1F', TargetFloor = '3F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'A', TargetAreas = 'K,M', 
               ControlFlag = 'Y', SortOrder = 1, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_A_TO_K', N'A區→K區上料', '1F', '3F', 'CROSS_FLOOR', 'A', 'K,M', 'Y', 1, 'DISPATCH');

-- 路線2: K區迴車→L區（Release，同樓層 3F）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_K_RELEASE' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'K區迴車→L區', SourceFloor = '3F', TargetFloor = '3F',
               RouteType = 'INTERNAL', SourceAreas = 'K', TargetAreas = 'L', 
               ControlFlag = 'Y', SortOrder = 2, DispatchMode = 'RELEASE'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_K_RELEASE', N'K區迴車→L區', '3F', '3F', 'INTERNAL', 'K', 'L', 'Y', 2, 'RELEASE');

-- 路線3: M區迴車→A區（Release，跨樓層 3F→1F）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_M_RELEASE' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'M區迴車→A區', SourceFloor = '3F', TargetFloor = '1F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'M', TargetAreas = 'A', 
               ControlFlag = 'Y', SortOrder = 3, DispatchMode = 'RELEASE'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_M_RELEASE', N'M區迴車→A區', '3F', '1F', 'CROSS_FLOOR', 'M', 'A', 'Y', 3, 'RELEASE');

-- 路線4: L區→B區下料（人工派送，跨樓層 3F→1F）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_L_TO_B' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'L區→B區下料', SourceFloor = '3F', TargetFloor = '1F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'L', TargetAreas = 'B', 
               ControlFlag = 'Y', SortOrder = 4, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_L_TO_B', N'L區→B區下料', '3F', '1F', 'CROSS_FLOOR', 'L', 'B', 'Y', 4, 'DISPATCH');

-- 路線5: B區迴車→A區（Release，同樓層 1F）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_B_RELEASE' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'B區迴車→A區', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'B', TargetAreas = 'A', 
               ControlFlag = 'Y', SortOrder = 5, DispatchMode = 'RELEASE'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_B_RELEASE', N'B區迴車→A區', '1F', '1F', 'INTERNAL', 'B', 'A', 'Y', 5, 'RELEASE');

-- 路線6: M區→K區自動派送（自動，同樓層 3F）
-- 觸發條件：M區 status=3 且 K區 status=0
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_M_TO_K_AUTO' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'M區→K區自動', SourceFloor = '3F', TargetFloor = '3F',
               RouteType = 'INTERNAL', SourceAreas = 'M', TargetAreas = 'K', 
               ControlFlag = 'Y', SortOrder = 6, DispatchMode = 'AUTO'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_M_TO_K_AUTO', N'M區→K區自動', '3F', '3F', 'INTERNAL', 'M', 'K', 'Y', 6, 'AUTO');

GO

-- =============================================
-- 驗證結果
-- =============================================
SELECT RouteId, RouteName, SourceFloor, TargetFloor, SourceAreas, TargetAreas, DispatchMode 
FROM pRoute 
WHERE RouteId LIKE 'ROUTE_F1_%'
ORDER BY SortOrder;
GO

PRINT 'Factory 1 pRoute deployment completed.';
