-- =============================================
-- pUserRoute 使用者路線權限表 - 完整部署腳本
-- 更新日期: 2026-01-09
-- 說明: 建立 pUserRoute 表並設定管理員權限
-- =============================================

-- 1. 建立 pUserRoute 表（如不存在）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[pUserRoute]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[pUserRoute] (
        [UserId]  NVARCHAR(20) NOT NULL,
        [RouteId] NVARCHAR(30) NOT NULL,
        PRIMARY KEY ([UserId], [RouteId])
    );
    PRINT 'Table pUserRoute created.';
END
GO

-- 2. 為管理員（UserId = '1'）設定所有路線權限
DECLARE @AdminUserId NVARCHAR(20) = '1';
DECLARE @Routes TABLE (RouteId NVARCHAR(30));

INSERT INTO @Routes VALUES 
    ('ROUTE_1F_3F'),
    ('ROUTE_2F_MT_TO_OP'),
    ('ROUTE_2F_Q_TO_S'),
    ('ROUTE_2F_R_TO_N'),
    ('ROUTE_2F_VCUT'),
    ('ROUTE_2F_4F'),
    ('ROUTE_3F_1F'),
    ('ROUTE_3F_4F'),
    ('ROUTE_4F_2F'),
    ('ROUTE_4F_3F');

-- 使用 MERGE 同步權限
MERGE INTO pUserRoute AS target
USING (SELECT @AdminUserId AS UserId, RouteId FROM @Routes) AS source 
ON target.UserId = source.UserId AND target.RouteId = source.RouteId
WHEN NOT MATCHED THEN
    INSERT (UserId, RouteId) VALUES (source.UserId, source.RouteId);

PRINT 'Admin (UserId=1) route permissions set.';
GO

-- 3. 驗證結果
SELECT ur.UserId, ur.RouteId, r.RouteName
FROM pUserRoute ur
JOIN pRoute r ON ur.RouteId = r.RouteId
WHERE ur.UserId = '1'
ORDER BY r.SortOrder;
GO
