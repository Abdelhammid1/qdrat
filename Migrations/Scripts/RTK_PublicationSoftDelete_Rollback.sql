-- RTK-S9.1 (تراجع): يعيد الفهرس المُصفّى القديم ويحذف أعمدة الحذف الناعم.
-- ⚠ شرط إلزامي: لا صفوف محذوفة. إن وُجد أمر محذوف (IsDeleted = 1) يتوقف السكربت بخطأ — استرجِع الأوامر المحذوفة من الواجهة أولًا
--   (وإلا سيظهر محذوف سابقًا للطلاب من جديد، وقد يتعارض رقمه المرجعي مع أمر نشط).
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود RTK-S9 من الإنتاج (الكود الجديد يقرأ الأعمدة).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007092203_RTK_PublicationSoftDelete'
)
BEGIN
    IF COL_LENGTH(N'[RemedialTrackPublications]', N'IsDeleted') IS NOT NULL
    BEGIN
        DECLARE @deleted int;
        EXEC sp_executesql N'SELECT @n = COUNT(*) FROM [RemedialTrackPublications] WHERE [IsDeleted] = 1', N'@n int OUTPUT', @n = @deleted OUTPUT;
        IF @deleted > 0
            THROW 51000, N'يوجد أوامر نشر محذوفة ناعمًا. استرجعها أولًا ثم أعد تشغيل التراجع.', 1;
    END;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RemedialTrackPublications_ActiveCode' AND object_id = OBJECT_ID(N'[RemedialTrackPublications]'))
        EXEC(N'DROP INDEX [UX_RemedialTrackPublications_ActiveCode] ON [RemedialTrackPublications]');

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RemedialTrackPublications_IsDeleted_CreatedAt' AND object_id = OBJECT_ID(N'[RemedialTrackPublications]'))
        EXEC(N'DROP INDEX [IX_RemedialTrackPublications_IsDeleted_CreatedAt] ON [RemedialTrackPublications]');

    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_RemedialTrackPublications_IsDeleted')
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP CONSTRAINT [DF_RemedialTrackPublications_IsDeleted]');

    IF COL_LENGTH(N'[RemedialTrackPublications]', N'IsDeleted') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP COLUMN [IsDeleted]');
    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeleteReason') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP COLUMN [DeleteReason]');
    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeletedAtUtc') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP COLUMN [DeletedAtUtc]');
    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeletedByName') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP COLUMN [DeletedByName]');
    IF COL_LENGTH(N'[RemedialTrackPublications]', N'DeletedByUserId') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackPublications] DROP COLUMN [DeletedByUserId]');

    EXEC(N'CREATE UNIQUE INDEX [UX_RemedialTrackPublications_ActiveCode] ON [RemedialTrackPublications] ([AccessCode]) WHERE [AccessCode] IS NOT NULL AND [Status] = 1');

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261007092203_RTK_PublicationSoftDelete';
END;
GO

COMMIT;
GO
