-- RTK: جدول الأيام — عمود RemedialTrackAxes.ReleaseDay (القيمة الافتراضية 1 = يوم النشر؛ سلوك الخطط القائمة لا يتغيّر).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075850_RTK_AxisReleaseDay'
)
BEGIN
    ALTER TABLE [RemedialTrackAxes] ADD [ReleaseDay] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075850_RTK_AxisReleaseDay'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005075850_RTK_AxisReleaseDay', N'8.0.17');
END;
GO

COMMIT;
GO
