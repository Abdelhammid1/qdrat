BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTracks] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [CurriculumId] int NOT NULL,
        [PassPercent] int NOT NULL,
        [MinWatchPercent] int NOT NULL,
        [Status] int NOT NULL,
        [IsStructureLocked] bit NOT NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedByName] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RemedialTracks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTracks_Curriculums_CurriculumId] FOREIGN KEY ([CurriculumId]) REFERENCES [Curriculums] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackAxes] (
        [Id] int NOT NULL IDENTITY,
        [TrackId] int NOT NULL,
        [SectionId] int NOT NULL,
        [TitleOverride] nvarchar(200) NULL,
        [Order] int NOT NULL,
        [Exam101ModelId] int NOT NULL,
        [Exam102ModelId] int NOT NULL,
        [ExamDurationMinutes] int NOT NULL,
        CONSTRAINT [PK_RemedialTrackAxes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackAxes_ProfessionalModels_Exam101ModelId] FOREIGN KEY ([Exam101ModelId]) REFERENCES [ProfessionalModels] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RemedialTrackAxes_ProfessionalModels_Exam102ModelId] FOREIGN KEY ([Exam102ModelId]) REFERENCES [ProfessionalModels] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RemedialTrackAxes_RemedialTracks_TrackId] FOREIGN KEY ([TrackId]) REFERENCES [RemedialTracks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RemedialTrackAxes_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackPublications] (
        [Id] int NOT NULL IDENTITY,
        [TrackId] int NOT NULL,
        [BatchId] int NOT NULL,
        [Scope] int NOT NULL,
        [Mode] int NOT NULL,
        [PublishAtUtc] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [AccessCode] nvarchar(6) NULL,
        [CodeVersion] int NOT NULL,
        [CodeGeneratedAtUtc] datetime2 NULL,
        [AdminNote] nvarchar(500) NULL,
        [TotalStudents] int NOT NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedByName] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CancelReason] nvarchar(300) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RemedialTrackPublications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackPublications_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RemedialTrackPublications_RemedialTracks_TrackId] FOREIGN KEY ([TrackId]) REFERENCES [RemedialTracks] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackVideos] (
        [Id] int NOT NULL IDENTITY,
        [AxisId] int NOT NULL,
        [Order] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [Provider] int NOT NULL,
        [ExternalId] nvarchar(50) NULL,
        [DurationSeconds] int NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_RemedialTrackVideos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackVideos_RemedialTrackAxes_AxisId] FOREIGN KEY ([AxisId]) REFERENCES [RemedialTrackAxes] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackEnrollments] (
        [Id] int NOT NULL IDENTITY,
        [PublicationId] int NOT NULL,
        [TrackId] int NOT NULL,
        [StudentId] int NOT NULL,
        [Status] int NOT NULL,
        [CurrentAxisId] int NULL,
        [VerifiedCodeVersion] int NULL,
        [FailedCodeAttempts] int NOT NULL,
        [CodeLockedUntilUtc] datetime2 NULL,
        [AdminReportNote] nvarchar(2000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [StartedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RemedialTrackEnrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackEnrollments_RemedialTrackPublications_PublicationId] FOREIGN KEY ([PublicationId]) REFERENCES [RemedialTrackPublications] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RemedialTrackEnrollments_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([StudentID]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackAxisProgresses] (
        [Id] int NOT NULL IDENTITY,
        [EnrollmentId] int NOT NULL,
        [AxisId] int NOT NULL,
        [Order] int NOT NULL,
        [Status] int NOT NULL,
        [Round] int NOT NULL,
        [Exam101Percent] float NULL,
        [Exam102Percent] float NULL,
        [OpenedAtUtc] datetime2 NULL,
        [PassedAtUtc] datetime2 NULL,
        [FailedAtUtc] datetime2 NULL,
        [AdminOpenedByUserId] nvarchar(450) NULL,
        [AdminOpenedByName] nvarchar(200) NULL,
        [AdminOpenReason] nvarchar(300) NULL,
        [AdminOpenedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RemedialTrackAxisProgresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackAxisProgresses_RemedialTrackAxes_AxisId] FOREIGN KEY ([AxisId]) REFERENCES [RemedialTrackAxes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RemedialTrackAxisProgresses_RemedialTrackEnrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [RemedialTrackEnrollments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackEvents] (
        [Id] int NOT NULL IDENTITY,
        [EnrollmentId] int NOT NULL,
        [AxisId] int NULL,
        [Type] int NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [ActorUserId] nvarchar(450) NULL,
        [ActorName] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RemedialTrackEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackEvents_RemedialTrackEnrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [RemedialTrackEnrollments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackExamAttempts] (
        [Id] int NOT NULL IDENTITY,
        [AxisProgressId] int NOT NULL,
        [ExamNumber] int NOT NULL,
        [ModelId] int NOT NULL,
        [Status] int NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [TotalQuestions] int NOT NULL,
        [CorrectCount] int NOT NULL,
        [ScorePercent] float NOT NULL,
        [IsPassed] bit NOT NULL,
        CONSTRAINT [PK_RemedialTrackExamAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackExamAttempts_RemedialTrackAxisProgresses_AxisProgressId] FOREIGN KEY ([AxisProgressId]) REFERENCES [RemedialTrackAxisProgresses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackVideoProgresses] (
        [Id] int NOT NULL IDENTITY,
        [AxisProgressId] int NOT NULL,
        [VideoId] int NOT NULL,
        [VideoOrder] int NOT NULL,
        [Round] int NOT NULL,
        [WatchedSeconds] float NOT NULL,
        [DurationSeconds] int NULL,
        [EndedSeen] bit NOT NULL,
        [IsCompleted] bit NOT NULL,
        [FirstPingAtUtc] datetime2 NULL,
        [LastPingAtUtc] datetime2 NULL,
        [LastPingState] nvarchar(10) NULL,
        [CompletedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RemedialTrackVideoProgresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackVideoProgresses_RemedialTrackAxisProgresses_AxisProgressId] FOREIGN KEY ([AxisProgressId]) REFERENCES [RemedialTrackAxisProgresses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RemedialTrackVideoProgresses_RemedialTrackVideos_VideoId] FOREIGN KEY ([VideoId]) REFERENCES [RemedialTrackVideos] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE TABLE [RemedialTrackExamAttemptQuestions] (
        [Id] int NOT NULL IDENTITY,
        [AttemptId] int NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [Order] int NOT NULL,
        [SelectedAnswer] nvarchar(1000) NULL,
        [IsCorrect] bit NULL,
        [AnsweredAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RemedialTrackExamAttemptQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackExamAttemptQuestions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RemedialTrackExamAttemptQuestions_RemedialTrackExamAttempts_AttemptId] FOREIGN KEY ([AttemptId]) REFERENCES [RemedialTrackExamAttempts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackAxes_Exam101ModelId] ON [RemedialTrackAxes] ([Exam101ModelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackAxes_Exam102ModelId] ON [RemedialTrackAxes] ([Exam102ModelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackAxes_SectionId] ON [RemedialTrackAxes] ([SectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackAxes_TrackId_Order] ON [RemedialTrackAxes] ([TrackId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackAxes_TrackId_SectionId] ON [RemedialTrackAxes] ([TrackId], [SectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackAxisProgresses_AxisId] ON [RemedialTrackAxisProgresses] ([AxisId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackAxisProgresses_EnrollmentId_AxisId] ON [RemedialTrackAxisProgresses] ([EnrollmentId], [AxisId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackAxisProgresses_EnrollmentId_Order] ON [RemedialTrackAxisProgresses] ([EnrollmentId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackEnrollments_PublicationId_StudentId] ON [RemedialTrackEnrollments] ([PublicationId], [StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackEnrollments_StudentId_Status] ON [RemedialTrackEnrollments] ([StudentId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackEnrollments_TrackId_StudentId] ON [RemedialTrackEnrollments] ([TrackId], [StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackEvents_EnrollmentId_CreatedAtUtc] ON [RemedialTrackEvents] ([EnrollmentId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackExamAttemptQuestions_AttemptId_Order] ON [RemedialTrackExamAttemptQuestions] ([AttemptId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackExamAttemptQuestions_AttemptId_QuestionId] ON [RemedialTrackExamAttemptQuestions] ([AttemptId], [QuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackExamAttemptQuestions_QuestionId] ON [RemedialTrackExamAttemptQuestions] ([QuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber] ON [RemedialTrackExamAttempts] ([AxisProgressId], [ExamNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackPublications_BatchId_Status] ON [RemedialTrackPublications] ([BatchId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackPublications_TrackId_Status] ON [RemedialTrackPublications] ([TrackId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_RemedialTrackPublications_ActiveCode] ON [RemedialTrackPublications] ([AccessCode]) WHERE [AccessCode] IS NOT NULL AND [Status] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTracks_Code] ON [RemedialTracks] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTracks_CurriculumId_Status] ON [RemedialTracks] ([CurriculumId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackVideoProgresses_AxisProgressId_VideoId_Round] ON [RemedialTrackVideoProgresses] ([AxisProgressId], [VideoId], [Round]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackVideoProgresses_VideoId] ON [RemedialTrackVideoProgresses] ([VideoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RemedialTrackVideos_AxisId_Order] ON [RemedialTrackVideos] ([AxisId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004121858_RTK_RemedialTracks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004121858_RTK_RemedialTracks', N'8.0.17');
END;
GO

COMMIT;
GO

