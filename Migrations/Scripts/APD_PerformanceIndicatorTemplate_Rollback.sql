-- APD-S4.1 (تراجع): يحذف عمود IsPartnerTemplate.
-- ⚠ القوالب الموجودة تبقى اختبارات مؤرشفة عادية بلا دفعات، ونسخ المدارس لا تتأثر.
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود APD-S4 من الإنتاج (الكود الجديد يقرأ العمود).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105131916_APD_PerformanceIndicatorTemplate'
)
BEGIN
    DECLARE @constraint sysname;
    SELECT @constraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'PerformanceIndicatorExams') AND c.name = N'IsPartnerTemplate';

    IF @constraint IS NOT NULL
        EXEC(N'ALTER TABLE [PerformanceIndicatorExams] DROP CONSTRAINT [' + @constraint + N']');

    IF COL_LENGTH(N'PerformanceIndicatorExams', N'IsPartnerTemplate') IS NOT NULL
        ALTER TABLE [PerformanceIndicatorExams] DROP COLUMN [IsPartnerTemplate];

    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105131916_APD_PerformanceIndicatorTemplate';
END;
GO

COMMIT;
GO
