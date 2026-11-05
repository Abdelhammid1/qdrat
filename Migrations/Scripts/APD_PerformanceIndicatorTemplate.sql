-- APD-S4.1: قالب إرسال مؤشر الأداء للشركاء: عمود IsPartnerTemplate على PerformanceIndicatorExams (افتراضي 0).
-- يتطلب تطبيق APD_PartnerDispatch.sql أولًا. لا يغيّر أي صف موجود. قابل لإعادة التشغيل. يُطبَّق يدويًا قبل النشر بعد Backup.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105131916_APD_PerformanceIndicatorTemplate'
)
BEGIN
    IF COL_LENGTH(N'PerformanceIndicatorExams', N'IsPartnerTemplate') IS NULL
        ALTER TABLE [PerformanceIndicatorExams] ADD [IsPartnerTemplate] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105131916_APD_PerformanceIndicatorTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105131916_APD_PerformanceIndicatorTemplate', N'8.0.17');
END;
GO

COMMIT;
GO
