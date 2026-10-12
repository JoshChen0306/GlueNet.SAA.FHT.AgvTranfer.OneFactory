-- =============================================
-- oPort 一廠 AB 棟 1F 新貨架站點 - 部署腳本
-- 建立日期: 2026-10-12
-- 對照表: ToDo/20260726_一廠AB棟1F新路線與邏輯/區域代碼對照表.md（T1）
-- 說明:
--   1. 可重複執行（idempotent）：只新增不存在的 StationNo，已存在的站點不覆寫，
--      避免重跑時清掉現場 HaveFlag / RackId / WorkOrder。
--   2. 迅得 1 = 既有 B1，不在本腳本內。
--   3. Remark 座標為【暫定值】：現場尚未掃圖，先沿 1F 地圖下緣排開，避免貼圖重疊；
--      正式座標於 T4 掃圖後以 UPDATE 回填。
-- 區域說明：
--   D區 中光電 D（R1 來源）       E區 迅得 2/3/4（R1 目的、R2 來源）
--   F區 迅得 5/6/7（R4 目的）     O區 中光電 A（R4 來源）
--   P區 中光電 B（R3 目的）       Z區 中光電 C（R2 目的）
-- =============================================

DECLARE @NewPorts TABLE (
    Block     NVARCHAR(10),
    Port      INT,
    StationNo NVARCHAR(20),
    Remark    NVARCHAR(100)
);

INSERT INTO @NewPorts (Block, Port, StationNo, Remark)
VALUES
    ('D', 1, 'D1', '200000,196800,0'),  -- 中光電 D
    ('E', 1, 'E1', '201000,196800,0'),  -- 迅得 2
    ('E', 2, 'E2', '202000,196800,0'),  -- 迅得 3
    ('E', 3, 'E3', '203000,196800,0'),  -- 迅得 4
    ('Z', 1, 'Z1', '204000,196800,0'),  -- 中光電 C
    ('P', 1, 'P1', '205000,196800,0'),  -- 中光電 B
    ('O', 1, 'O1', '206000,196800,0'),  -- 中光電 A
    ('F', 1, 'F1', '207000,196800,0'),  -- 迅得 5
    ('F', 2, 'F2', '208000,196800,0'),  -- 迅得 6
    ('F', 3, 'F3', '209000,196800,0');  -- 迅得 7

INSERT INTO oPort (Area, Block, Port, StationNo, InterfaceName, PanelCallShuttle, MachineName, Priority, ProductionPartNo, UseFlag, HaveFlag, RackId, WorkOrder, PutTime, BgnToEnd, Remark)
SELECT 'FHT1-1F', n.Block, n.Port, n.StationNo, NULL, 'Y', n.StationNo, n.Port, NULL, 'Y', '0', '', '', '', '', n.Remark
FROM @NewPorts n
WHERE NOT EXISTS (SELECT 1 FROM oPort p WHERE p.StationNo = n.StationNo);

PRINT CONCAT('新增站點數：', @@ROWCOUNT, '（已存在的站點略過）');
GO

-- =============================================
-- 驗證結果
-- =============================================
SELECT Block AS '區域', COUNT(*) AS '站點數'
FROM oPort
WHERE Block IN ('D', 'E', 'F', 'O', 'P', 'Z')
GROUP BY Block
ORDER BY Block;
GO

-- 每個 StationNo 應只有 1 筆（oPort 無主鍵，重複會造成派車取錯站）
SELECT StationNo, COUNT(*) AS '筆數'
FROM oPort
WHERE Block IN ('D', 'E', 'F', 'O', 'P', 'Z')
GROUP BY StationNo
HAVING COUNT(*) > 1;
GO

SELECT Area, Block, StationNo, MachineName, PanelCallShuttle, UseFlag, HaveFlag, Remark
FROM oPort
WHERE Block IN ('D', 'E', 'F', 'O', 'P', 'Z')
ORDER BY Block, Port;
GO

PRINT 'AB 棟 1F oPort deployment completed.';
PRINT '預期站點數：D(1) + E(3) + F(3) + O(1) + P(1) + Z(1) = 10';
