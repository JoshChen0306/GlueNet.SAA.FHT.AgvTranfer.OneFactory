-- =============================================
-- oPort 工廠1庫位定義 - 部署腳本
-- 更新日期: 2026-01-13
-- 說明: 工廠1專用的庫位站點定義
-- =============================================

-- 清空現有資料（可選，建議先備份）
-- TRUNCATE TABLE oPort;

-- =============================================
-- 工廠1 庫位定義
-- 區域說明：
--   A區 (1F 供貨區) - 存放待派送物料
--   B區 (1F 下料區) - 成品下料暫存
--   K區 (3F 左側上料區) - 機台上料
--   L區 (3F 右側下料區) - 機台下料
--   M區 (3F 暫存區) - 暫存待派送或自動派送到K區
-- =============================================

-- 1. 清除現有工廠1相關站點（如果需要重新建立）
DELETE FROM oPort WHERE Block IN ('A', 'B', 'K', 'L', 'M');
GO

-- =============================================
-- A區 (1F 供貨區) - 5個站點
-- =============================================
INSERT INTO oPort (Area, Block, Port, StationNo, InterfaceName, PanelCallShuttle, MachineName, Priority, ProductionPartNo, UseFlag, HaveFlag, RackId, WorkOrder, PutTime, BgnToEnd, Remark)
VALUES
    ('FHT1-1F', 'A', 1, 'A1', NULL, 'Y', 'A1', 1, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'A', 2, 'A2', NULL, 'Y', 'A2', 2, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'A', 3, 'A3', NULL, 'Y', 'A3', 3, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'A', 4, 'A4', NULL, 'Y', 'A4', 4, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'A', 5, 'A5', NULL, 'Y', 'A5', 5, NULL, 'Y', '0', '', '', '', '', '200000,200000,0');
GO

-- =============================================
-- B區 (1F 下料區) - 5個站點
-- =============================================
INSERT INTO oPort (Area, Block, Port, StationNo, InterfaceName, PanelCallShuttle, MachineName, Priority, ProductionPartNo, UseFlag, HaveFlag, RackId, WorkOrder, PutTime, BgnToEnd, Remark)
VALUES
    ('FHT1-1F', 'B', 1, 'B1', NULL, 'Y', 'B1', 1, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'B', 2, 'B2', NULL, 'Y', 'B2', 2, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'B', 3, 'B3', NULL, 'Y', 'B3', 3, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'B', 4, 'B4', NULL, 'Y', 'B4', 4, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-1F', 'B', 5, 'B5', NULL, 'Y', 'B5', 5, NULL, 'Y', '0', '', '', '', '', '200000,200000,0');
GO

-- =============================================
-- K區 (3F 左側上料區) - 5個站點
-- =============================================
INSERT INTO oPort (Area, Block, Port, StationNo, InterfaceName, PanelCallShuttle, MachineName, Priority, ProductionPartNo, UseFlag, HaveFlag, RackId, WorkOrder, PutTime, BgnToEnd, Remark)
VALUES
    ('FHT1-3F', 'K', 1, 'K1', NULL, 'Y', 'K1', 1, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'K', 2, 'K2', NULL, 'Y', 'K2', 2, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'K', 3, 'K3', NULL, 'Y', 'K3', 3, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'K', 4, 'K4', NULL, 'Y', 'K4', 4, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'K', 5, 'K5', NULL, 'Y', 'K5', 5, NULL, 'Y', '0', '', '', '', '', '200000,200000,0');
GO

-- =============================================
-- L區 (3F 右側下料區) - 5個站點
-- =============================================
INSERT INTO oPort (Area, Block, Port, StationNo, InterfaceName, PanelCallShuttle, MachineName, Priority, ProductionPartNo, UseFlag, HaveFlag, RackId, WorkOrder, PutTime, BgnToEnd, Remark)
VALUES
    ('FHT1-3F', 'L', 1, 'L1', NULL, 'Y', 'L1', 1, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'L', 2, 'L2', NULL, 'Y', 'L2', 2, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'L', 3, 'L3', NULL, 'Y', 'L3', 3, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'L', 4, 'L4', NULL, 'Y', 'L4', 4, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'L', 5, 'L5', NULL, 'Y', 'L5', 5, NULL, 'Y', '0', '', '', '', '', '200000,200000,0');
GO

-- =============================================
-- M區 (3F 暫存區) - 5個站點
-- =============================================
INSERT INTO oPort (Area, Block, Port, StationNo, InterfaceName, PanelCallShuttle, MachineName, Priority, ProductionPartNo, UseFlag, HaveFlag, RackId, WorkOrder, PutTime, BgnToEnd, Remark)
VALUES
    ('FHT1-3F', 'M', 1, 'M1', NULL, 'Y', 'M1', 1, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'M', 2, 'M2', NULL, 'Y', 'M2', 2, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'M', 3, 'M3', NULL, 'Y', 'M3', 3, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'M', 4, 'M4', NULL, 'Y', 'M4', 4, NULL, 'Y', '0', '', '', '', '', '200000,200000,0'),
    ('FHT1-3F', 'M', 5, 'M5', NULL, 'Y', 'M5', 5, NULL, 'Y', '0', '', '', '', '', '200000,200000,0');
GO

-- =============================================
-- 驗證結果
-- =============================================
SELECT Block AS '區域', COUNT(*) AS '站點數' 
FROM oPort 
WHERE Block IN ('A', 'B', 'K', 'L', 'M')
GROUP BY Block
ORDER BY Block;
GO

SELECT Area, Block, StationNo, MachineName, PanelCallShuttle, UseFlag, HaveFlag, Remark
FROM oPort
WHERE Block IN ('A', 'B', 'K', 'L', 'M')
ORDER BY Block, Port;
GO

PRINT 'Factory 1 oPort deployment completed.';
PRINT '總計站點數：A區(5) + B區(5) + K區(5) + L區(5) + M區(5) = 25';
