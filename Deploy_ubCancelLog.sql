-- ============================================================
-- 跨樓層自動化機制 — 補建 ubCancelLog 資料表
--
-- 用途：記錄跨樓層系統任務（CROSS_FLOOR_DISPATCH / IDLE_RETURN）
--       被取消時的歸檔紀錄（SQLData.Insert_ubCancelLog 使用）。
--
-- 背景：20260427 跨樓層移植時程式已引用此表，但現場 DB 從未建立。
--       2026-07-21 一廠測試因此表不存在，Insert_ubCancelLog 拋出
--       「無效的物件名稱 'ubCancelLog'」，導致 OnCrossFloorDispatchCompleted
--       未執行、_crossFloorDispatchPending 旗標卡死、後續跨樓層任務
--       全部無法派發至 RCS。
--
-- 欄位型別對齊 oMission（agvDB_1400004.sql）：
--   TaskDateTime nvarchar(20)、TaskSource nvarchar(20)、
--   BeginStation/EndStation nvarchar(20)、TaskCode nvarchar(50)、
--   ParentTaskDateTime varchar(50)（同 Deploy_AddParentTaskDateTime.sql）
--
-- 執行日期：2026-07-__
-- ============================================================

IF OBJECT_ID(N'dbo.ubCancelLog', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ubCancelLog](
        [Id]                 [bigint] IDENTITY(1,1) NOT NULL,
        [CancelTime]         [datetime]      NOT NULL,
        [TaskDateTime]       [nvarchar](20)  NOT NULL,
        [ParentTaskDateTime] [varchar](50)   NULL,
        [TaskSource]         [nvarchar](20)  NULL,
        [BeginStation]       [nvarchar](20)  NULL,
        [EndStation]         [nvarchar](20)  NULL,
        [TaskCode]           [nvarchar](50)  NULL,
        [RcsCancelResult]    [nvarchar](500) NULL,
        CONSTRAINT [PK_ubCancelLog] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];

    PRINT 'ubCancelLog 資料表已建立';
END
ELSE
BEGIN
    PRINT 'ubCancelLog 已存在，跳過';
END
GO

-- 查詢常以 TaskDateTime / ParentTaskDateTime 追任務關聯，建立非叢集索引
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ubCancelLog_TaskDateTime' AND object_id = OBJECT_ID(N'dbo.ubCancelLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ubCancelLog_TaskDateTime]
        ON [dbo].[ubCancelLog] ([TaskDateTime] ASC);
    PRINT 'IX_ubCancelLog_TaskDateTime 已建立';
END
ELSE
BEGIN
    PRINT 'IX_ubCancelLog_TaskDateTime 已存在，跳過';
END
GO

-- 驗證
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ubCancelLog'
ORDER BY ORDINAL_POSITION;
GO
