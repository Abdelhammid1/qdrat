-- RTK-S13 (تراجع): يحذف جدولي الملاحق وعمود RemedialTrackExamAttempts.AddendumId ويعيد الفهرس الفريد القديم.
-- ⚠ تُفقد بها بيانات الملاحق كلها (تقدّم الطلاب فيها + محاولات اختبار الملحق وأسئلتها) — لا فقد لتقدّم المحاور ولا درجاتها.
-- محاولات الملحق تُحذف أولًا (ExamNumber = 3 / AddendumId غير فارغ) وإلا فشل إعادة الفهرس الفريد القديم (محاولات ملحق متعددة لنفس المحور).
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود RTK-S13 من الإنتاج (الكود الجديد يقرأ الجدولين والعمود).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007144918_RTK_Addendum'
)
BEGIN
    IF COL_LENGTH(N'[RemedialTrackExamAttempts]', N'AddendumId') IS NOT NULL
        EXEC(N'DELETE FROM [RemedialTrackExamAttempts] WHERE [AddendumId] IS NOT NULL');   -- أسئلتها تُحذف بالـ CASCADE

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RemedialTrackExamAttempts_RemedialTrackAddenda_AddendumId')
        ALTER TABLE [RemedialTrackExamAttempts] DROP CONSTRAINT [FK_RemedialTrackExamAttempts_RemedialTrackAddenda_AddendumId];

    IF OBJECT_ID(N'[RemedialTrackAddendumProgresses]', N'U') IS NOT NULL
        DROP TABLE [RemedialTrackAddendumProgresses];

    IF OBJECT_ID(N'[RemedialTrackAddenda]', N'U') IS NOT NULL
        DROP TABLE [RemedialTrackAddenda];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RemedialTrackExamAttempts_Addendum_OpenAttempt' AND object_id = OBJECT_ID(N'[RemedialTrackExamAttempts]'))
        DROP INDEX [UX_RemedialTrackExamAttempts_Addendum_OpenAttempt] ON [RemedialTrackExamAttempts];

    -- الفهرس المُصفّى يعتمد على العمود: يُحذف قبله ثم يُعاد بصيغته القديمة (غير مُصفّى)
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber' AND object_id = OBJECT_ID(N'[RemedialTrackExamAttempts]'))
        DROP INDEX [IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber] ON [RemedialTrackExamAttempts];

    IF COL_LENGTH(N'[RemedialTrackExamAttempts]', N'AddendumId') IS NOT NULL
        ALTER TABLE [RemedialTrackExamAttempts] DROP COLUMN [AddendumId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007144918_RTK_Addendum'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber' AND object_id = OBJECT_ID(N'[RemedialTrackExamAttempts]'))
        CREATE UNIQUE INDEX [IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber] ON [RemedialTrackExamAttempts] ([AxisProgressId], [ExamNumber]);

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261007144918_RTK_Addendum';
END;
GO

COMMIT;
GO
