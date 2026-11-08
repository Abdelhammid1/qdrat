-- OLC-S1.2 (تراجع): يحذف جداول المحتوى التعليمي أونلاين الأربعة وعمودي Batches ومفتاح الميزة.
-- ⚠ يحذف نهائيًا كل محتوى المحاضرات الأونلاين وتقدّم الطلاب فيها — خذ Backup قبله.
-- لا يُطبَّق إلا بعد سحب كود OLC من الإنتاج (الكود الجديد يقرأ هذه الجداول والأعمدة).
-- Idempotent: آمن لإعادة التشغيل.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    -- الجداول بالترتيب العكسي للاعتماديات
    IF OBJECT_ID(N'[LectureContentItemProgress]', N'U') IS NOT NULL DROP TABLE [LectureContentItemProgress];
    IF OBJECT_ID(N'[LectureContentProgress]', N'U') IS NOT NULL DROP TABLE [LectureContentProgress];
    IF OBJECT_ID(N'[LectureContentItems]', N'U') IS NOT NULL DROP TABLE [LectureContentItems];
    IF OBJECT_ID(N'[LectureContents]', N'U') IS NOT NULL DROP TABLE [LectureContents];

    -- القيد الافتراضي على IsOnlineLearning باسم يولّده SQL Server — يُحذف أولًا بالبحث عن اسمه
    DECLARE @df sysname;
    SELECT @df = dc.[name]
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'[Batches]') AND c.[name] = N'IsOnlineLearning';
    IF @df IS NOT NULL EXEC(N'ALTER TABLE [Batches] DROP CONSTRAINT [' + @df + N'];');

    IF COL_LENGTH(N'Batches', N'IsOnlineLearning') IS NOT NULL
        ALTER TABLE [Batches] DROP COLUMN [IsOnlineLearning];

    IF COL_LENGTH(N'Batches', N'OnlineLearningEnabledAt') IS NOT NULL
        ALTER TABLE [Batches] DROP COLUMN [OnlineLearningEnabledAt];

    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent';
END;
GO

DELETE FROM [SystemSettings] WHERE [Key] = N'OnlineLectureContent.Enabled';
GO

COMMIT;
GO
