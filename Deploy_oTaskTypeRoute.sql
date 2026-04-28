-- =============================================
-- TaskType 路線對照表 - oTaskTypeRoute TABLE（一廠版本）
-- 統一管理搬運(Transport)與空車移動(EmptyMove)的 TaskType
-- 一廠樓層範圍：1F ↔ 3F（僅此兩層）
-- 執行日期：2026-04-__
-- =============================================

-- 建立 TABLE（若已存在則跳過）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[oTaskTypeRoute]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[oTaskTypeRoute](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [MoveType] [nvarchar](20) NOT NULL,
        [FromFloor] [nvarchar](10) NOT NULL,
        [ToFloor] [nvarchar](10) NOT NULL,
        [TaskType] [nvarchar](50) NOT NULL,
        [UseFlag] [nvarchar](1) NULL,
        [Remark] [nvarchar](100) NULL,
        CONSTRAINT [PK_oTaskTypeRoute] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY]

    PRINT 'oTaskTypeRoute TABLE 建立成功'
END
ELSE
BEGIN
    PRINT 'oTaskTypeRoute TABLE 已存在，跳過建立'
END
GO

-- =============================================
-- 一廠初始資料（僅 1F ↔ 3F）
-- MoveType：Transport=搬運 / EmptyMove=空車移動
-- TaskType：海康 RCS TaskType 代碼（F13Test / F31Test 為暫定值，待現場確認）
-- UseFlag：Y=啟用 / N=停用
-- =============================================

-- === Transport 跨樓層 ===
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '1F' AND [ToFloor] = '3F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '1F', '3F', 'F13Test', 'Y', '1F→3F 跨樓層搬運')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'Transport' AND [FromFloor] = '3F' AND [ToFloor] = '1F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('Transport', '3F', '1F', 'F31Test', 'Y', '3F→1F 跨樓層搬運')

-- === EmptyMove 空車移動（歸位 / 預調度用） ===
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '1F' AND [ToFloor] = '3F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '1F', '3F', 'F13Test', 'Y', '1F→3F 空車移動（暫用同 Transport TaskType）')
IF NOT EXISTS (SELECT 1 FROM [dbo].[oTaskTypeRoute] WHERE [MoveType] = 'EmptyMove' AND [FromFloor] = '3F' AND [ToFloor] = '1F')
    INSERT INTO [dbo].[oTaskTypeRoute] ([MoveType], [FromFloor], [ToFloor], [TaskType], [UseFlag], [Remark])
    VALUES ('EmptyMove', '3F', '1F', 'F31Test', 'Y', '3F→1F 空車移動（暫用同 Transport TaskType）')

PRINT '初始資料處理完成（已存在的跳過，新增的補上）'
GO

-- 驗證結果
SELECT * FROM [dbo].[oTaskTypeRoute] ORDER BY [MoveType], [FromFloor], [ToFloor]
GO
