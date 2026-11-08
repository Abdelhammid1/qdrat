BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105143557_MSE_UniqueStudentAnswer'
)
BEGIN

    IF OBJECT_ID(N'MinistrySimExamStudentAnswers_DuplicatesBackup', N'U') IS NULL
        CREATE TABLE [MinistrySimExamStudentAnswers_DuplicatesBackup] (
            [Id] int NOT NULL PRIMARY KEY,
            [MinistrySimExamStudentAttemptId] int NOT NULL,
            [MinistrySimExamStageQuestionId] int NOT NULL,
            [SelectedOptionId] int NULL,
            [IsCorrect] bit NULL,
            [AnsweredAt] datetime2 NOT NULL,
            [IsFlaggedForReview] bit NOT NULL,
            [BackedUpAt] datetime2 NOT NULL DEFAULT (GETDATE())
        );

    INSERT INTO [MinistrySimExamStudentAnswers_DuplicatesBackup]
        ([Id], [MinistrySimExamStudentAttemptId], [MinistrySimExamStageQuestionId], [SelectedOptionId], [IsCorrect], [AnsweredAt], [IsFlaggedForReview])
    SELECT a.[Id], a.[MinistrySimExamStudentAttemptId], a.[MinistrySimExamStageQuestionId], a.[SelectedOptionId], a.[IsCorrect], a.[AnsweredAt], a.[IsFlaggedForReview]
    FROM [MinistrySimExamStudentAnswers] a
    WHERE EXISTS (
        SELECT 1 FROM [MinistrySimExamStudentAnswers] newer
        WHERE newer.[MinistrySimExamStudentAttemptId] = a.[MinistrySimExamStudentAttemptId]
          AND newer.[MinistrySimExamStageQuestionId] = a.[MinistrySimExamStageQuestionId]
          AND newer.[Id] > a.[Id]);

    DELETE a
    FROM [MinistrySimExamStudentAnswers] a
    WHERE EXISTS (
        SELECT 1 FROM [MinistrySimExamStudentAnswers] newer
        WHERE newer.[MinistrySimExamStudentAttemptId] = a.[MinistrySimExamStudentAttemptId]
          AND newer.[MinistrySimExamStageQuestionId] = a.[MinistrySimExamStageQuestionId]
          AND newer.[Id] > a.[Id]);

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105143557_MSE_UniqueStudentAnswer'
)
BEGIN
    DROP INDEX [IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId] ON [MinistrySimExamStudentAnswers];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105143557_MSE_UniqueStudentAnswer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId_MinistrySimExamStageQuestionId] ON [MinistrySimExamStudentAnswers] ([MinistrySimExamStudentAttemptId], [MinistrySimExamStageQuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105143557_MSE_UniqueStudentAnswer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105143557_MSE_UniqueStudentAnswer', N'8.0.17');
END;
GO

COMMIT;
GO

