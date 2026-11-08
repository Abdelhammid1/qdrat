-- APD-S5.2 (تراجع): يحذف عمود HiddenByOwnerAt من تكليفات معمل القياس.
-- ⚠ ما أخفته المدارس من قوائمها يعود ظاهرًا لها، ولا أثر على الطلاب أو النتائج.
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود APD-S5 من الإنتاج (الكود الجديد يقرأ العمود).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214613_APD_MinistrySimPartnerHidden'
)
BEGIN
    IF COL_LENGTH(N'MinistrySimExamAssignmentsToStudents', N'HiddenByOwnerAt') IS NOT NULL
        ALTER TABLE [MinistrySimExamAssignmentsToStudents] DROP COLUMN [HiddenByOwnerAt];

    IF COL_LENGTH(N'MinistrySimExamAssignmentsToBatches', N'HiddenByOwnerAt') IS NOT NULL
        ALTER TABLE [MinistrySimExamAssignmentsToBatches] DROP COLUMN [HiddenByOwnerAt];

    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214613_APD_MinistrySimPartnerHidden';
END;
GO

COMMIT;
GO
