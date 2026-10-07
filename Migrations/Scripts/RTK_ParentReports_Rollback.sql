-- RTK-S11.2 (تراجع): يحذف جدول RemedialTrackParentReports وعمود RemedialTrackPublications.AutoSendParentReports.
-- ⚠ تُفقد بها لقطات تقارير أولياء الأمور وإقراراتهم بالاطلاع — لا فقد لتقدّم الطلاب أو درجاتهم.
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود RTK-S11 من الإنتاج (الكود الجديد يقرأ الجدول والعمود).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007100349_RTK_ParentReports'
)
BEGIN
    IF OBJECT_ID(N'[RemedialTrackParentReports]', N'U') IS NOT NULL
        DROP TABLE [RemedialTrackParentReports];

    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_RemedialTrackPublications_AutoSendParentReports')
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP CONSTRAINT [DF_RemedialTrackPublications_AutoSendParentReports]');

    -- قيد افتراضي مولَّد باسم EF (لو طُبّق الترحيل مباشرة بدل السكربت)
    DECLARE @dfName sysname;
    SELECT @dfName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[RemedialTrackPublications]') AND c.name = N'AutoSendParentReports';
    IF @dfName IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP CONSTRAINT [' + @dfName + N']');

    IF COL_LENGTH(N'[RemedialTrackPublications]', N'AutoSendParentReports') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP COLUMN [AutoSendParentReports]');

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261007100349_RTK_ParentReports';
END;
GO

COMMIT;
GO
