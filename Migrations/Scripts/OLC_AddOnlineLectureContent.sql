-- OLC-S1.2 — المحتوى التعليمي أونلاين (العقد الموحَّد v1.1): عمودان على Batches + 4 جداول جديدة + مفتاح الميزة.
-- إضافي فقط: لا يمس أي بيانات قائمة (IsOnlineLearning افتراضيًا 0 لكل الدفعات الحالية).
-- Idempotent: كل خطوة محمية بـ __EFMigrationsHistory، وإعادة التشغيل آمنة.
-- يُطبَّق يدويًا بعد Backup وقبل نشر كود OLC، ويفترض تطبيق APD_MinistrySimPartnerHidden قبله.
-- التراجع: OLC_AddOnlineLectureContent_Rollback.sql (بعد سحب كود OLC من الإنتاج).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    ALTER TABLE [Batches] ADD [IsOnlineLearning] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    ALTER TABLE [Batches] ADD [OnlineLearningEnabledAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE TABLE [LectureContents] (
        [Id] int NOT NULL IDENTITY,
        [LectureId] int NOT NULL,
        [ContentMode] int NOT NULL,
        [Status] int NOT NULL DEFAULT 1,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [VideoProvider] int NULL,
        [VideoUrl] nvarchar(500) NULL,
        [VideoExternalId] nvarchar(50) NULL,
        [DurationSeconds] int NULL,
        [ThumbnailUrl] nvarchar(500) NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedByUserId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_LectureContents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LectureContents_Lecture_LectureId] FOREIGN KEY ([LectureId]) REFERENCES [Lecture] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE TABLE [LectureContentItems] (
        [Id] int NOT NULL IDENTITY,
        [LectureContentId] int NOT NULL,
        [LessonId] int NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [Title] nvarchar(200) NULL,
        [StartSeconds] int NULL,
        [EndSeconds] int NULL,
        [SourceVideoKind] int NOT NULL DEFAULT 0,
        [SourceVideoId] int NULL,
        [VideoProvider] int NULL,
        [VideoUrl] nvarchar(500) NULL,
        [VideoExternalId] nvarchar(50) NULL,
        [DurationSeconds] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_LectureContentItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LectureContentItems_LectureContents_LectureContentId] FOREIGN KEY ([LectureContentId]) REFERENCES [LectureContents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LectureContentItems_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE TABLE [LectureContentProgress] (
        [Id] int NOT NULL IDENTITY,
        [LectureContentId] int NOT NULL,
        [StudentId] int NOT NULL,
        [WatchedSeconds] float NOT NULL,
        [MaxPositionSeconds] float NOT NULL,
        [DurationSeconds] int NULL,
        [LastPingState] nvarchar(10) NULL,
        [FirstPingAtUtc] datetime2 NULL,
        [LastPingAtUtc] datetime2 NULL,
        [EndedSeen] bit NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_LectureContentProgress] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LectureContentProgress_LectureContents_LectureContentId] FOREIGN KEY ([LectureContentId]) REFERENCES [LectureContents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LectureContentProgress_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([StudentID]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE TABLE [LectureContentItemProgress] (
        [Id] int NOT NULL IDENTITY,
        [LectureContentItemId] int NOT NULL,
        [StudentId] int NOT NULL,
        [WatchedSeconds] float NOT NULL,
        [MaxPositionSeconds] float NOT NULL,
        [DurationSeconds] int NULL,
        [LastPingState] nvarchar(10) NULL,
        [FirstPingAtUtc] datetime2 NULL,
        [LastPingAtUtc] datetime2 NULL,
        [EndedSeen] bit NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_LectureContentItemProgress] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LectureContentItemProgress_LectureContentItems_LectureContentItemId] FOREIGN KEY ([LectureContentItemId]) REFERENCES [LectureContentItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LectureContentItemProgress_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([StudentID]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LectureContentItemProgress_LectureContentItemId_StudentId] ON [LectureContentItemProgress] ([LectureContentItemId], [StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE INDEX [IX_LectureContentItemProgress_StudentId] ON [LectureContentItemProgress] ([StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LectureContentItems_LectureContentId_LessonId] ON [LectureContentItems] ([LectureContentId], [LessonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE INDEX [IX_LectureContentItems_LectureContentId_SortOrder] ON [LectureContentItems] ([LectureContentId], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE INDEX [IX_LectureContentItems_LessonId] ON [LectureContentItems] ([LessonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LectureContentProgress_LectureContentId_StudentId] ON [LectureContentProgress] ([LectureContentId], [StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE INDEX [IX_LectureContentProgress_StudentId] ON [LectureContentProgress] ([StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LectureContents_LectureId] ON [LectureContents] ([LectureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108165302_OLC_AddOnlineLectureContent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108165302_OLC_AddOnlineLectureContent', N'8.0.17');
END;
GO

COMMIT;
GO

-- OLC-S1.3 (R11): مفتاح الميزة — مغلق افتراضيًا حتى الإطلاق.
-- التفعيل:  UPDATE SystemSettings SET [Value] = N'true'  WHERE [Key] = N'OnlineLectureContent.Enabled';
-- التعطيل:  UPDATE SystemSettings SET [Value] = N'false' WHERE [Key] = N'OnlineLectureContent.Enabled';
-- يسري التغيير خلال 30 ثانية (كاش الميزة) دون إعادة تشغيل التطبيق.
IF NOT EXISTS (SELECT 1 FROM [SystemSettings] WHERE [Key] = N'OnlineLectureContent.Enabled')
BEGIN
    INSERT INTO [SystemSettings] ([Key], [Value], [Description], [CreatedAt])
    VALUES (N'OnlineLectureContent.Enabled', N'false', N'قسم «المحتوى التعليمي» أونلاين لطلاب الدفعات الأونلاين (true = تفعيل، أي قيمة أخرى = مغلق)', SYSUTCDATETIME());
END;
GO
