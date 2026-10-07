-- RTK-S11.2: تقارير ولي الأمر (D23/D28)
--   1) عمود RemedialTrackPublications.AutoSendParentReports (الافتراضي 1 = الإرسال التلقائي مفعّل)
--   2) جدول RemedialTrackParentReports + فهرس فريد (EnrollmentId, AxisProgressId, Kind) وفهرسان للطابور وصفحة ولي الأمر
-- إضافات فقط (لا حذف أعمدة ولا جداول) وقابل لإعادة التشغيل. يسبقه: RTK_VideoReview.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007100349_RTK_ParentReports'
)
BEGIN
    IF COL_LENGTH(N'[RemedialTrackPublications]', N'AutoSendParentReports') IS NULL
        ALTER TABLE [RemedialTrackPublications] ADD [AutoSendParentReports] bit NOT NULL CONSTRAINT [DF_RemedialTrackPublications_AutoSendParentReports] DEFAULT CAST(1 AS bit);

    IF OBJECT_ID(N'[RemedialTrackParentReports]', N'U') IS NULL
    BEGIN
        CREATE TABLE [RemedialTrackParentReports] (
            [Id] int NOT NULL IDENTITY,
            [EnrollmentId] int NOT NULL,
            [AxisProgressId] int NULL,
            [Kind] int NOT NULL,
            [StudentId] int NOT NULL,
            [ParentId] int NULL,
            [SnapshotJson] nvarchar(max) NOT NULL,
            [Status] int NOT NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [SentAtUtc] datetime2 NULL,
            [AcknowledgedAtUtc] datetime2 NULL,
            [SentByUserId] nvarchar(450) NULL,
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_RemedialTrackParentReports] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_RemedialTrackParentReports_RemedialTrackEnrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [RemedialTrackEnrollments] ([Id]) ON DELETE CASCADE
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackParentReports_Parent_Status_CreatedAt' AND object_id = OBJECT_ID(N'[RemedialTrackParentReports]'))
        CREATE INDEX [IX_RemedialTrackParentReports_Parent_Status_CreatedAt] ON [RemedialTrackParentReports] ([ParentId], [Status], [CreatedAtUtc]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackParentReports_Status_CreatedAt' AND object_id = OBJECT_ID(N'[RemedialTrackParentReports]'))
        CREATE INDEX [IX_RemedialTrackParentReports_Status_CreatedAt] ON [RemedialTrackParentReports] ([Status], [CreatedAtUtc]);

    -- بلا فلتر: SQL Server يعامل NULL كقيمة واحدة فيُحمى «التقرير الختامي» (AxisProgressId NULL) من التكرار
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RemedialTrackParentReports_Enrollment_Axis_Kind' AND object_id = OBJECT_ID(N'[RemedialTrackParentReports]'))
        CREATE UNIQUE INDEX [UX_RemedialTrackParentReports_Enrollment_Axis_Kind] ON [RemedialTrackParentReports] ([EnrollmentId], [AxisProgressId], [Kind]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007100349_RTK_ParentReports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007100349_RTK_ParentReports', N'8.0.17');
END;
GO

COMMIT;
GO
