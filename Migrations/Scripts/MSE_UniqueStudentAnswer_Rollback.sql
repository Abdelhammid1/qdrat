-- MSE (تراجع): يحذف الفهرس الفريد ويعيد الفهرس العادي على MinistrySimExamStudentAttemptId.
-- لا يعيد الصفوف المحذوفة: نسختها باقية في MinistrySimExamStudentAnswers_DuplicatesBackup ولا يحذفها هذا السكربت.
-- الكود الجديد يعمل بعد التراجع: يعتمد أحدث صف عند وجود تكرار.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105143557_MSE_UniqueStudentAnswer'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId_MinistrySimExamStageQuestionId' AND object_id = OBJECT_ID(N'MinistrySimExamStudentAnswers'))
        DROP INDEX [IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId_MinistrySimExamStageQuestionId] ON [MinistrySimExamStudentAnswers];

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId' AND object_id = OBJECT_ID(N'MinistrySimExamStudentAnswers'))
        CREATE INDEX [IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId] ON [MinistrySimExamStudentAnswers] ([MinistrySimExamStudentAttemptId]);

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261105143557_MSE_UniqueStudentAnswer';
END;
GO

COMMIT;
GO
