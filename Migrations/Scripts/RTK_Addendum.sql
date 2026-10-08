-- RTK-S13: «ملحق المحور» (D29–D34)
--   1) جدولا RemedialTrackAddenda + RemedialTrackAddendumProgresses (الفهرس الفريد (AddendumId, EnrollmentId) وفهرسان للمتابعة)
--   2) عمود RemedialTrackExamAttempts.AddendumId (اختبار الملحق؛ ExamNumber = 3) + مفتاح أجنبي (Restrict)
--   3) الفهرس الفريد IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber يصبح مُصفّى «AddendumId IS NULL» (يحافظ على قاعدة
--      «محاولة واحدة لكل اختبار 101/102» تمامًا، ويسمح بمحاولات الملحق غير المحدودة) + فهرس فريد مُصفّى يضمن محاولة ملحق جارية واحدة.
-- لا حذف أعمدة ولا جداول ولا بيانات؛ التعديل الوحيد على موجود = استبدال الفهرس (آمن: ما في الجدول من محاولات AddendumId فيها NULL فتحقق الفريد القديم).
-- قابل لإعادة التشغيل (Idempotent). يسبقه: RTK_ParentReports.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007144918_RTK_Addendum'
)
BEGIN
    -- (1) العمود الجديد أولًا (قبل أي فهرس يشير إليه)
    IF COL_LENGTH(N'[RemedialTrackExamAttempts]', N'AddendumId') IS NULL
        ALTER TABLE [RemedialTrackExamAttempts] ADD [AddendumId] int NULL;

    -- (2) الجداول
    IF OBJECT_ID(N'[RemedialTrackAddenda]', N'U') IS NULL
    BEGIN
        CREATE TABLE [RemedialTrackAddenda] (
            [Id] int NOT NULL IDENTITY,
            [PublicationId] int NOT NULL,
            [AxisId] int NOT NULL,
            [Title] nvarchar(200) NOT NULL,
            [Url] nvarchar(500) NOT NULL,
            [Provider] int NOT NULL,
            [ExternalId] nvarchar(50) NULL,
            [DurationSeconds] int NULL,
            [ExamModelId] int NULL,
            [ExamDurationMinutes] int NULL,
            [Reason] nvarchar(300) NOT NULL,
            [IsActive] bit NOT NULL,
            [CreatedByUserId] nvarchar(450) NOT NULL,
            [CreatedByName] nvarchar(200) NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_RemedialTrackAddenda] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_RemedialTrackAddenda_ProfessionalModels_ExamModelId] FOREIGN KEY ([ExamModelId]) REFERENCES [ProfessionalModels] ([Id]),
            CONSTRAINT [FK_RemedialTrackAddenda_RemedialTrackAxes_AxisId] FOREIGN KEY ([AxisId]) REFERENCES [RemedialTrackAxes] ([Id]),
            CONSTRAINT [FK_RemedialTrackAddenda_RemedialTrackPublications_PublicationId] FOREIGN KEY ([PublicationId]) REFERENCES [RemedialTrackPublications] ([Id])
        );
    END;

    IF OBJECT_ID(N'[RemedialTrackAddendumProgresses]', N'U') IS NULL
    BEGIN
        CREATE TABLE [RemedialTrackAddendumProgresses] (
            [Id] int NOT NULL IDENTITY,
            [AddendumId] int NOT NULL,
            [EnrollmentId] int NOT NULL,
            [WatchedSeconds] float NOT NULL,
            [DurationSeconds] int NULL,
            [EndedSeen] bit NOT NULL,
            [VideoCompleted] bit NOT NULL,
            [LastPingAtUtc] datetime2 NULL,
            [LastPingState] nvarchar(10) NULL,
            [VideoCompletedAtUtc] datetime2 NULL,
            [ExamPassed] bit NOT NULL,
            [BestScorePercent] float NULL,
            [AttemptsCount] int NOT NULL,
            [CompletedAtUtc] datetime2 NULL,
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_RemedialTrackAddendumProgresses] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_RemedialTrackAddendumProgresses_RemedialTrackAddenda_AddendumId] FOREIGN KEY ([AddendumId]) REFERENCES [RemedialTrackAddenda] ([Id]) ON DELETE CASCADE,
            CONSTRAINT [FK_RemedialTrackAddendumProgresses_RemedialTrackEnrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [RemedialTrackEnrollments] ([Id])
        );
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007144918_RTK_Addendum'
)
BEGIN
    -- (3) فهرس المحاولات القديم ← مُصفّى (يُنشأ بـ EXEC لأن العمود AddendumId أُضيف في دفعة سابقة)
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber' AND object_id = OBJECT_ID(N'[RemedialTrackExamAttempts]') AND has_filter = 0)
        DROP INDEX [IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber] ON [RemedialTrackExamAttempts];

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber' AND object_id = OBJECT_ID(N'[RemedialTrackExamAttempts]'))
        EXEC(N'CREATE UNIQUE INDEX [IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber] ON [RemedialTrackExamAttempts] ([AxisProgressId], [ExamNumber]) WHERE [AddendumId] IS NULL');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RemedialTrackExamAttempts_Addendum_OpenAttempt' AND object_id = OBJECT_ID(N'[RemedialTrackExamAttempts]'))
        EXEC(N'CREATE UNIQUE INDEX [UX_RemedialTrackExamAttempts_Addendum_OpenAttempt] ON [RemedialTrackExamAttempts] ([AddendumId], [AxisProgressId]) WHERE [AddendumId] IS NOT NULL AND [Status] = 0');

    -- فهارس الملاحق
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackAddenda_AxisId' AND object_id = OBJECT_ID(N'[RemedialTrackAddenda]'))
        CREATE INDEX [IX_RemedialTrackAddenda_AxisId] ON [RemedialTrackAddenda] ([AxisId]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackAddenda_ExamModelId' AND object_id = OBJECT_ID(N'[RemedialTrackAddenda]'))
        CREATE INDEX [IX_RemedialTrackAddenda_ExamModelId] ON [RemedialTrackAddenda] ([ExamModelId]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackAddenda_Publication_IsActive' AND object_id = OBJECT_ID(N'[RemedialTrackAddenda]'))
        CREATE INDEX [IX_RemedialTrackAddenda_Publication_IsActive] ON [RemedialTrackAddenda] ([PublicationId], [IsActive]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackAddendumProgresses_Enrollment_CompletedAt' AND object_id = OBJECT_ID(N'[RemedialTrackAddendumProgresses]'))
        CREATE INDEX [IX_RemedialTrackAddendumProgresses_Enrollment_CompletedAt] ON [RemedialTrackAddendumProgresses] ([EnrollmentId], [CompletedAtUtc]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RemedialTrackAddendumProgresses_Addendum_Enrollment' AND object_id = OBJECT_ID(N'[RemedialTrackAddendumProgresses]'))
        CREATE UNIQUE INDEX [UX_RemedialTrackAddendumProgresses_Addendum_Enrollment] ON [RemedialTrackAddendumProgresses] ([AddendumId], [EnrollmentId]);

    -- المفتاح الأجنبي لمحاولات الملحق (Restrict) — بـ EXEC لنفس سبب العمود
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RemedialTrackExamAttempts_RemedialTrackAddenda_AddendumId')
        EXEC(N'ALTER TABLE [RemedialTrackExamAttempts] ADD CONSTRAINT [FK_RemedialTrackExamAttempts_RemedialTrackAddenda_AddendumId] FOREIGN KEY ([AddendumId]) REFERENCES [RemedialTrackAddenda] ([Id])');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007144918_RTK_Addendum'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007144918_RTK_Addendum', N'8.0.17');
END;
GO

COMMIT;
GO
