-- =============================================
-- pRoute 路線定義表 - 完整部署腳本
-- 更新日期: 2026-01-09
-- 說明: 建立 pRoute 表並插入/更新所有路線定義
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

-- 2. 清空現有資料（可選，建議先備份）
-- DELETE FROM pRoute;

-- 3. 插入/更新路線定義（10 條路線）
-- ROUTE_1F_3F: RELEASE
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_1F_3F' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'1F 電梯前暫存區(G) → 3F 插針室(J)', SourceFloor = '1F', TargetFloor = '3F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'G', TargetAreas = 'J', ControlFlag = 'Y', SortOrder = 1, DispatchMode = 'RELEASE'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_1F_3F', N'1F 電梯前暫存區(G) → 3F 插針室(J)', '1F', '3F', 'CROSS_FLOOR', 'G', 'J', 'Y', 1, 'RELEASE');

-- ROUTE_2F_MT_TO_OP: DISPATCH (SourceAreas = MT)
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_2F_MT_TO_OP' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'2F 雷雕區(M/T) → 加工區(O/P)', SourceFloor = '2F', TargetFloor = '2F',
               RouteType = 'INTERNAL', SourceAreas = 'MT', TargetAreas = 'O,P', ControlFlag = 'Y', SortOrder = 2, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_MT_TO_OP', N'2F 雷雕區(M/T) → 加工區(O/P)', '2F', '2F', 'INTERNAL', 'MT', 'O,P', 'Y', 2, 'DISPATCH');

-- ROUTE_2F_Q_TO_S: DISPATCH
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_2F_Q_TO_S' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'2F 出貨區(Q) → 清洗區(S)', SourceFloor = '2F', TargetFloor = '2F',
               RouteType = 'INTERNAL', SourceAreas = 'Q', TargetAreas = 'S', ControlFlag = 'Y', SortOrder = 3, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_Q_TO_S', N'2F 出貨區(Q) → 清洗區(S)', '2F', '2F', 'INTERNAL', 'Q', 'S', 'Y', 3, 'DISPATCH');

-- ROUTE_2F_R_TO_N: DISPATCH
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_2F_R_TO_N' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'2F 廢料區(R) → 廢料回收區(N)', SourceFloor = '2F', TargetFloor = '2F',
               RouteType = 'INTERNAL', SourceAreas = 'R', TargetAreas = 'N', ControlFlag = 'Y', SortOrder = 4, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_R_TO_N', N'2F 廢料區(R) → 廢料回收區(N)', '2F', '2F', 'INTERNAL', 'R', 'N', 'Y', 4, 'DISPATCH');

-- ROUTE_2F_VCUT: DISPATCH
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_2F_VCUT' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'2F 雷雕區 → Vcut', SourceFloor = '2F', TargetFloor = '2F',
               RouteType = 'INTERNAL', SourceAreas = 'M', TargetAreas = 'T', ControlFlag = 'Y', SortOrder = 5, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_VCUT', N'2F 雷雕區 → Vcut', '2F', '2F', 'INTERNAL', 'M', 'T', 'Y', 5, 'DISPATCH');

-- ROUTE_2F_4F: DISPATCH
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_2F_4F' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'2F 成型後(H) → 4F 烘烤(K)', SourceFloor = '2F', TargetFloor = '4F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'H', TargetAreas = 'K', ControlFlag = 'Y', SortOrder = 6, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_2F_4F', N'2F 成型後(H) → 4F 烘烤(K)', '2F', '4F', 'CROSS_FLOOR', 'H', 'K', 'Y', 6, 'DISPATCH');

-- ROUTE_3F_1F: DISPATCH
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_3F_1F' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'3F 插針室 → 1F 電梯前暫存區', SourceFloor = '3F', TargetFloor = '1F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'J', TargetAreas = 'G', ControlFlag = 'Y', SortOrder = 7, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_3F_1F', N'3F 插針室 → 1F 電梯前暫存區', '3F', '1F', 'CROSS_FLOOR', 'J', 'G', 'Y', 7, 'DISPATCH');

-- ROUTE_3F_4F: DISPATCH
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_3F_4F' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'3F 品檢區(I) → 4F 烘烤(L)', SourceFloor = '3F', TargetFloor = '4F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'I', TargetAreas = 'L', ControlFlag = 'Y', SortOrder = 8, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_3F_4F', N'3F 品檢區(I) → 4F 烘烤(L)', '3F', '4F', 'CROSS_FLOOR', 'I', 'L', 'Y', 8, 'DISPATCH');

-- ROUTE_4F_2F: RELEASE
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_4F_2F' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'4F 烘烤(K) → 2F 成型後(H)', SourceFloor = '4F', TargetFloor = '2F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'K', TargetAreas = 'H', ControlFlag = 'Y', SortOrder = 9, DispatchMode = 'RELEASE'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_4F_2F', N'4F 烘烤(K) → 2F 成型後(H)', '4F', '2F', 'CROSS_FLOOR', 'K', 'H', 'Y', 9, 'RELEASE');

-- ROUTE_4F_3F: DISPATCH
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_4F_3F' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'4F 烘烤(L) → 3F 品檢區(I)', SourceFloor = '4F', TargetFloor = '3F',
               RouteType = 'CROSS_FLOOR', SourceAreas = 'L', TargetAreas = 'I', ControlFlag = 'Y', SortOrder = 10, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_4F_3F', N'4F 烘烤(L) → 3F 品檢區(I)', '4F', '3F', 'CROSS_FLOOR', 'L', 'I', 'Y', 10, 'DISPATCH');

-- 刪除舊的 ROUTE_2F_FORMING（如存在）
IF EXISTS (SELECT 1 FROM pRoute WHERE RouteId = 'ROUTE_2F_FORMING')
BEGIN
    DELETE FROM pUserRoute WHERE RouteId = 'ROUTE_2F_FORMING';
    DELETE FROM pRoute WHERE RouteId = 'ROUTE_2F_FORMING';
END
GO

-- 驗證結果
SELECT RouteId, RouteName, SourceFloor, SourceAreas, TargetAreas, DispatchMode 
FROM pRoute 
ORDER BY SortOrder;
GO

PRINT 'pRoute deployment completed.';
