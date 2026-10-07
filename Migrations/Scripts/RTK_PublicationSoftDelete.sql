-- RTK-S9.1: حذف ناعم لأمر النشر — أعمدة IsDeleted/DeletedAtUtc/DeletedByUserId/DeletedByName/DeleteReason
-- + فهرس القائمة IX_RemedialTrackPublications_IsDeleted_CreatedAt + إعادة تعريف الفهرس المُصفّى UX_RemedialTrackPublications_ActiveCode
--   ليستثني المحذوف (الرقم المرجعي لأمر محذوف يتحرر). إضافات فقط (لا حذف أعمدة) وقابل لإعادة التشغيل.
-- الفهرس المُصفّى يتطلب QUOTED_IDENTIFIER ON (افتراضي SSMS/Azure Data Studio؛ sqlcmd بدون -I يجعله OFF).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007092203_RTK_PublicationSoftDelete'
)
BEGIN
    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeleteReason') IS NULL
        ALTER TABLE [RemedialTrackPublications] ADD [DeleteReason] nvarchar(300) NULL;

    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeletedAtUtc') IS NULL
        ALTER TABLE [RemedialTrackPublications] ADD [DeletedAtUtc] datetime2 NULL;

    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeletedByName') IS NULL
        ALTER TABLE [RemedialTrackPublications] ADD [DeletedByName] nvarchar(200) NULL;

    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeletedByUserId') IS NULL
        ALTER TABLE [RemedialTrackPublications] ADD [DeletedByUserId] nvarchar(450) NULL;

    IF COL_LENGTH(N'[RemedialTrackPublications]', N'IsDeleted') IS NULL
        ALTER TABLE [RemedialTrackPublications] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_RemedialTrackPublications_IsDeleted] DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007092203_RTK_PublicationSoftDelete'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackPublications_IsDeleted_CreatedAt' AND object_id = OBJECT_ID(N'[RemedialTrackPublications]'))
        EXEC(N'CREATE INDEX [IX_RemedialTrackPublications_IsDeleted_CreatedAt] ON [RemedialTrackPublications] ([IsDeleted], [CreatedAtUtc])');

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RemedialTrackPublications_ActiveCode' AND object_id = OBJECT_ID(N'[RemedialTrackPublications]'))
        EXEC(N'DROP INDEX [UX_RemedialTrackPublications_ActiveCode] ON [RemedialTrackPublications]');

    EXEC(N'CREATE UNIQUE INDEX [UX_RemedialTrackPublications_ActiveCode] ON [RemedialTrackPublications] ([AccessCode]) WHERE [AccessCode] IS NOT NULL AND [Status] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007092203_RTK_PublicationSoftDelete'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007092203_RTK_PublicationSoftDelete', N'8.0.17');
END;
GO

COMMIT;
GO
