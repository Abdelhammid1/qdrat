BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE TABLE [QuestionReviewTasks] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [AdminNote] nvarchar(1000) NULL,
        [InstructorId] int NOT NULL,
        [CurriculumId] int NULL,
        [ParentTaskId] int NULL,
        [Status] int NOT NULL,
        [Priority] int NOT NULL,
        [DueAtUtc] datetime2 NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedByName] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [StartedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [ClosedAtUtc] datetime2 NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CancelReason] nvarchar(500) NULL,
        [LastReminderAtUtc] datetime2 NULL,
        [TotalItems] int NOT NULL,
        [PendingItems] int NOT NULL,
        [ApprovedItems] int NOT NULL,
        [ReturnedItems] int NOT NULL,
        [RemovedItems] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_QuestionReviewTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuestionReviewTasks_Curriculums_CurriculumId] FOREIGN KEY ([CurriculumId]) REFERENCES [Curriculums] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_QuestionReviewTasks_Instructors_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [Instructors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_QuestionReviewTasks_QuestionReviewTasks_ParentTaskId] FOREIGN KEY ([ParentTaskId]) REFERENCES [QuestionReviewTasks] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE TABLE [QuestionReviewTaskItems] (
        [Id] bigint NOT NULL IDENTITY,
        [TaskId] int NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [Status] int NOT NULL,
        [IsLockActive] bit NOT NULL,
        [ActionAtUtc] datetime2 NULL,
        [ActionByUserId] nvarchar(450) NULL,
        [ActionByName] nvarchar(200) NULL,
        [ReturnNote] nvarchar(500) NULL,
        [AdminResolutionNote] nvarchar(500) NULL,
        [ReferenceNumberSnapshot] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_QuestionReviewTaskItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuestionReviewTaskItems_QuestionReviewTasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [QuestionReviewTasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuestionReviewTaskItems_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuestionReviewTaskItems_TaskId_QuestionId] ON [QuestionReviewTaskItems] ([TaskId], [QuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE INDEX [IX_QuestionReviewTaskItems_TaskId_Status] ON [QuestionReviewTaskItems] ([TaskId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_QuestionReviewTaskItems_ActiveLock] ON [QuestionReviewTaskItems] ([QuestionId]) WHERE [IsLockActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuestionReviewTasks_Code] ON [QuestionReviewTasks] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE INDEX [IX_QuestionReviewTasks_CurriculumId] ON [QuestionReviewTasks] ([CurriculumId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE INDEX [IX_QuestionReviewTasks_InstructorId_Status] ON [QuestionReviewTasks] ([InstructorId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE INDEX [IX_QuestionReviewTasks_ParentTaskId] ON [QuestionReviewTasks] ([ParentTaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    CREATE INDEX [IX_QuestionReviewTasks_Status_DueAtUtc] ON [QuestionReviewTasks] ([Status], [DueAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004095208_QRT_QuestionReviewTasks', N'8.0.17');
END;
GO

COMMIT;
GO

