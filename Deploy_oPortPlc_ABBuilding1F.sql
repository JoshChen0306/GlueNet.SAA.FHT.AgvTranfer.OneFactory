-- =============================================
-- 站點 PLC 點位表 - oPortPlc TABLE（一廠 AB 棟 1F，T29）
-- 一站一列：sensor 位址（StockSensorLink 讀）＋三色燈綠/黃/紅位址（RackStatusOutput 寫）
-- 執行日期：2026-10-__
--
-- 原則：
--   1. 可重複執行（idempotent）：資料表不存在才建立；每站只在「尚無資料」時新增，
--      已存在的列一律不覆寫（現場改過的位址／啟用旗標不會被本腳本蓋回暫定值）。
--   2. 位址為 2026-10-20 我方暫定值，迅得正式位址到位後直接 UPDATE 本表，重啟 ACC 生效。
--   3. ⚠️ 啟用旗標預設 N（B1 sensor 除外，維持現行 M10）：
--      - 未接線的 sensor 讀到 OFF，有料的站會被誤判貨架移走而清空，並對中光電送 release
--      - 寫入未規劃的 M bit 可能干擾 PLC 既有程式
--      迅得確認位址與接線後，逐站改 SensorUseFlag / LampUseFlag = 'Y'。
--   4. 本表有任何資料列時，ACC 以本表為準（全部停用＝不監控），不再讀設定檔 SensorMap；
--      同一 sensor 位址被多站使用時，ACC 只採用第一站並記 WARN。
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[oPortPlc]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[oPortPlc](
        [StationNo] [nvarchar](20) NOT NULL,   -- 與 oPort.StationNo 同長度
        [SensorAddress] [nvarchar](20) NULL,
        [SensorUseFlag] [nvarchar](1) NOT NULL CONSTRAINT [DF_oPortPlc_SensorUseFlag] DEFAULT ('N'),
        [LampGreen] [nvarchar](20) NULL,
        [LampYellow] [nvarchar](20) NULL,
        [LampRed] [nvarchar](20) NULL,
        [LampUseFlag] [nvarchar](1) NOT NULL CONSTRAINT [DF_oPortPlc_LampUseFlag] DEFAULT ('N'),
        [Remark] [nvarchar](100) NULL,
        CONSTRAINT [PK_oPortPlc] PRIMARY KEY CLUSTERED ([StationNo] ASC),
        -- 只允許 Y/N：手動 UPDATE 填成 '1' 等值時直接報錯，避免被程式默默當成停用
        CONSTRAINT [CK_oPortPlc_SensorUseFlag] CHECK ([SensorUseFlag] IN ('Y', 'N')),
        CONSTRAINT [CK_oPortPlc_LampUseFlag] CHECK ([LampUseFlag] IN ('Y', 'N'))
    ) ON [PRIMARY]

    PRINT 'oPortPlc TABLE 建立成功'
END
ELSE
BEGIN
    PRINT 'oPortPlc TABLE 已存在，跳過建立'
END
GO

-- =============================================
-- 暫定點位（sensor：B1 沿用 M10，其餘 M11 起；三色燈：每站 3 bit 自 M200 起，綠/黃/紅）
-- =============================================
INSERT INTO [dbo].[oPortPlc] ([StationNo], [SensorAddress], [SensorUseFlag], [LampGreen], [LampYellow], [LampRed], [LampUseFlag], [Remark])
SELECT src.StationNo, src.SensorAddress, src.SensorUseFlag, src.LampGreen, src.LampYellow, src.LampRed, 'N', src.Remark
FROM (VALUES
    ('B1', 'M10', 'Y', 'M200', 'M201', 'M202', N'迅得1；sensor 為既有 M10（沿用設定檔 SensorMap）'),
    ('O1', 'M11', 'N', 'M203', 'M204', 'M205', N'中光電A；暫定位址待迅得確認'),
    ('P1', 'M12', 'N', 'M206', 'M207', 'M208', N'中光電B；暫定位址待迅得確認'),
    ('Z1', 'M13', 'N', 'M209', 'M210', 'M211', N'中光電C；暫定位址待迅得確認'),
    ('D1', 'M14', 'N', 'M212', 'M213', 'M214', N'中光電D；暫定位址待迅得確認'),
    ('E1', 'M16', 'N', 'M215', 'M216', 'M217', N'迅得2；暫定位址待迅得確認'),
    ('E2', 'M17', 'N', 'M218', 'M219', 'M220', N'迅得3；暫定位址待迅得確認'),
    ('E3', 'M18', 'N', 'M221', 'M222', 'M223', N'迅得4；暫定位址待迅得確認'),
    ('F1', 'M19', 'N', 'M224', 'M225', 'M226', N'迅得5；暫定位址待迅得確認'),
    ('F2', 'M20', 'N', 'M227', 'M228', 'M229', N'迅得6；暫定位址待迅得確認'),
    ('F3', 'M21', 'N', 'M230', 'M231', 'M232', N'迅得7；暫定位址待迅得確認')
) AS src (StationNo, SensorAddress, SensorUseFlag, LampGreen, LampYellow, LampRed, Remark)
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[oPortPlc] t WHERE t.StationNo = src.StationNo);

PRINT CONCAT('oPortPlc 新增 ', @@ROWCOUNT, ' 筆');
GO

-- =============================================
-- 驗證：應有 11 筆；啟用中的 sensor 只有 B1
-- =============================================
SELECT StationNo, SensorAddress, SensorUseFlag, LampGreen, LampYellow, LampRed, LampUseFlag
FROM [dbo].[oPortPlc]
ORDER BY StationNo;

-- 現場改位址範例（改完重啟 ACC）：
-- UPDATE oPortPlc SET SensorAddress = 'M30', SensorUseFlag = 'Y' WHERE StationNo = 'O1';
-- UPDATE oPortPlc SET LampGreen = 'M300', LampYellow = 'M301', LampRed = 'M302', LampUseFlag = 'Y' WHERE StationNo = 'O1';
