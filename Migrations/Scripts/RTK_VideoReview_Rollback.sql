-- RTK-S10.1 (تراجع): يحذف أعمدة وضع مراجعة الفيديوهات من RemedialTrackEnrollments.
-- ⚠ يُفقد بها حالة المراجعة المفتوحة حاليًا (تعود الفيديوهات مغلقة لكل الطلاب) — لا فقد لتقدّم أو درجات.
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود RTK-S10 من الإنتاج (الكود الجديد يقرأ الأعمدة).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007093522_RTK_VideoReview'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_RemedialTrackEnrollments_VideoReviewEnabled')
        EXEC(N'ALTER TABLE [RemedialTrackEnrollments] DROP CONSTRAINT [DF_RemedialTrackEnrollments_VideoReviewEnabled]');

    -- قيد افتراضي بلا اسم (لو أُنشئ بترحيل EF مباشرة): يُحذف بالاسم المولَّد
    DECLARE @dfName sysname;
    SELECT @dfName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[RemedialTrackEnrollments]') AND c.name = N'VideoReviewEnabled';
    IF @dfName IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackEnrollments] DROP CONSTRAINT [' + @dfName + N']');

    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewEnabled') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackEnrollments] DROP COLUMN [VideoReviewEnabled]');
    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewUntilUtc') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackEnrollments] DROP COLUMN [VideoReviewUntilUtc]');
    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewChangedAtUtc') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackEnrollments] DROP COLUMN [VideoReviewChangedAtUtc]');
    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewChangedByUserId') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackEnrollments] DROP COLUMN [VideoReviewChangedByUserId]');
    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'VideoReviewChangedByName') IS NOT NULL
        EXEC(N'ALTER TABLE [RemedialTrackEnrollments] DROP COLUMN [VideoReviewChangedByName]');

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261007093522_RTK_VideoReview';
END;
GO

COMMIT;
GO
