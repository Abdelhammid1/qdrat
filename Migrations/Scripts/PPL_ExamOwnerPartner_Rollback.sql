-- PPL-S2 (تراجع): يحذف ملكية الاختبار Exams.OwnerPartnerId مع الفهرسين والمفتاح الأجنبي.
-- ⚠ بعد الحذف تصبح اختبارات الشركاء (إن وُجدت) بلا مالك فتظهر في Admin/PlacementExams كاختبارات المعهد.
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود PPL-S2 من الإنتاج (الكود الجديد يقرأ العمود).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102004719_PPL_ExamOwnerPartner'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Exams_Partners_OwnerPartnerId' AND parent_object_id = OBJECT_ID(N'[Exams]'))
        ALTER TABLE [Exams] DROP CONSTRAINT [FK_Exams_Partners_OwnerPartnerId];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Exams_OwnerPartnerId' AND object_id = OBJECT_ID(N'[Exams]'))
        DROP INDEX [IX_Exams_OwnerPartnerId] ON [Exams];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Exams_Type_OwnerPartnerId' AND object_id = OBJECT_ID(N'[Exams]'))
        DROP INDEX [IX_Exams_Type_OwnerPartnerId] ON [Exams];

    IF COL_LENGTH(N'[Exams]', N'OwnerPartnerId') IS NOT NULL
        EXEC(N'ALTER TABLE [Exams] DROP COLUMN [OwnerPartnerId]');

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261102004719_PPL_ExamOwnerPartner';
END;
GO

COMMIT;
GO
