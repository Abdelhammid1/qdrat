-- RTK-S10.1: وضع مراجعة الفيديوهات (D24) — أعمدة على RemedialTrackEnrollments:
--   VideoReviewEnabled / VideoReviewUntilUtc / VideoReviewChangedAtUtc / VideoReviewChangedByUserId / VideoReviewChangedByName
-- إضافات فقط (لا حذف أعمدة ولا فهارس) وقابل لإعادة التشغيل.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007093522_RTK_VideoReview'
)
BEGIN
    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewChangedAtUtc') IS NULL
        ALTER TABLE [RemedialTrackEnrollments] ADD [VideoReviewChangedAtUtc] datetime2 NULL;

    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewChangedByName') IS NULL
        ALTER TABLE [RemedialTrackEnrollments] ADD [VideoReviewChangedByName] nvarchar(200) NULL;

    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewChangedByUserId') IS NULL
        ALTER TABLE [RemedialTrackEnrollments] ADD [VideoReviewChangedByUserId] nvarchar(450) NULL;

    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewEnabled') IS NULL
        ALTER TABLE [RemedialTrackEnrollments] ADD [VideoReviewEnabled] bit NOT NULL CONSTRAINT [DF_RemedialTrackEnrollments_VideoReviewEnabled] DEFAULT CAST(0 AS bit);

    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewUntilUtc') IS NULL
        ALTER TABLE [RemedialTrackEnrollments] ADD [VideoReviewUntilUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007093522_RTK_VideoReview'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007093522_RTK_VideoReview', N'8.0.17');
END;
GO

COMMIT;
GO
