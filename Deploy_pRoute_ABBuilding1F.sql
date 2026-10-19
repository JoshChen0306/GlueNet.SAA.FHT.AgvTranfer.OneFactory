-- =============================================
-- pRoute / pUserRoute 一廠 AB 棟 1F 新路線 - 部署腳本
-- 建立日期: 2026-10-12
-- 前置: Deploy_pRoute_Factory1.sql（pRoute / pUserRoute 表結構與路線1~6）
--       Deploy_oPort_ABBuilding1F.sql（D/E/F/O/P/Z 站點）
-- 說明:
--   1. 可重複執行（idempotent）：pRoute 以 MERGE 同步，pUserRoute 只補不存在的權限。
--   2. R1、R4 由中光電貨架 Sensor ON 自動觸發（DispatchMode=AUTO，派送頁不顯示）；
--      R2、R3 由人員在派送頁派發（DispatchMode=DISPATCH）。2026-10-06 客戶流程圖。
--      2026-10-19：R2、R3 改由 SCP AutoDispatch 自動派送（appsettings AutoDispatch:Routes，引擎不讀 pRoute）；
--      此處 DISPATCH 路線保留作為人員派發的例外備援。
--   3. 權限先只給管理員 admin；現場操作帳號待客戶提供後再補。
-- 區域對照（區域代碼對照表.md）:
--   D=中光電D  E=迅得2/3/4  F=迅得5/6/7  O=中光電A  P=中光電B  Z=中光電C  B=迅得1(B1)
-- =============================================

-- R1: 中光電D → 迅得2/3/4（自動）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_D_TO_E_AUTO' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'R1 中光電D→迅得2/3/4', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'D', TargetAreas = 'E',
               ControlFlag = 'Y', SortOrder = 7, DispatchMode = 'AUTO'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_D_TO_E_AUTO', N'R1 中光電D→迅得2/3/4', '1F', '1F', 'INTERNAL', 'D', 'E', 'Y', 7, 'AUTO');

-- R2: 迅得2/3/4 → 中光電C（人員派發，起點由人員自選）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_E_TO_Z' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'R2 迅得2/3/4→中光電C', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'E', TargetAreas = 'Z',
               ControlFlag = 'Y', SortOrder = 8, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_E_TO_Z', N'R2 迅得2/3/4→中光電C', '1F', '1F', 'INTERNAL', 'E', 'Z', 'Y', 8, 'DISPATCH');

-- R3: 迅得1(B1) → 中光電B（人員派發）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_B_TO_P' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'R3 迅得1→中光電B', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'B', TargetAreas = 'P',
               ControlFlag = 'Y', SortOrder = 9, DispatchMode = 'DISPATCH'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_B_TO_P', N'R3 迅得1→中光電B', '1F', '1F', 'INTERNAL', 'B', 'P', 'Y', 9, 'DISPATCH');

-- R4: 中光電A → 迅得5/6/7（自動）
MERGE INTO pRoute AS target
USING (SELECT 'ROUTE_F1_O_TO_F_AUTO' AS RouteId) AS source ON target.RouteId = source.RouteId
WHEN MATCHED THEN
    UPDATE SET RouteName = N'R4 中光電A→迅得5/6/7', SourceFloor = '1F', TargetFloor = '1F',
               RouteType = 'INTERNAL', SourceAreas = 'O', TargetAreas = 'F',
               ControlFlag = 'Y', SortOrder = 10, DispatchMode = 'AUTO'
WHEN NOT MATCHED THEN
    INSERT (RouteId, RouteName, SourceFloor, TargetFloor, RouteType, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode)
    VALUES ('ROUTE_F1_O_TO_F_AUTO', N'R4 中光電A→迅得5/6/7', '1F', '1F', 'INTERNAL', 'O', 'F', 'Y', 10, 'AUTO');
GO

-- 管理員權限（只補不存在的）
MERGE INTO pUserRoute AS target
USING (
    SELECT 'admin' AS UserId, RouteId
    FROM (VALUES ('ROUTE_F1_D_TO_E_AUTO'), ('ROUTE_F1_E_TO_Z'), ('ROUTE_F1_B_TO_P'), ('ROUTE_F1_O_TO_F_AUTO')) AS r(RouteId)
) AS source
ON target.UserId = source.UserId AND target.RouteId = source.RouteId
WHEN NOT MATCHED THEN
    INSERT (UserId, RouteId) VALUES (source.UserId, source.RouteId);
GO

-- =============================================
-- 驗證結果
-- =============================================
SELECT RouteId, RouteName, SourceFloor, TargetFloor, SourceAreas, TargetAreas, ControlFlag, DispatchMode
FROM pRoute
WHERE RouteId IN ('ROUTE_F1_D_TO_E_AUTO', 'ROUTE_F1_E_TO_Z', 'ROUTE_F1_B_TO_P', 'ROUTE_F1_O_TO_F_AUTO')
ORDER BY SortOrder;
GO

SELECT ur.UserId, ur.RouteId
FROM pUserRoute ur
WHERE ur.RouteId IN ('ROUTE_F1_D_TO_E_AUTO', 'ROUTE_F1_E_TO_Z', 'ROUTE_F1_B_TO_P', 'ROUTE_F1_O_TO_F_AUTO')
ORDER BY ur.UserId, ur.RouteId;
GO

PRINT 'AB 棟 1F pRoute deployment completed.';
PRINT '預期：pRoute 4 筆（R1~R4）、admin 權限 4 筆';
