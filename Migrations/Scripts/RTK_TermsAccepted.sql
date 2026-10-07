-- RTK: إقرار شروط الخطة — عمود RemedialTrackEnrollments.TermsAcceptedAtUtc (nullable؛ إضافات فقط وقابل لإعادة التشغيل).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005095620_RTK_TermsAccepted'
)
BEGIN
    IF COL_LENGTH(N'[RemedialTrackEnrollments]', N'TermsAcceptedAtUtc') IS NULL
        ALTER TABLE [RemedialTrackEnrollments] ADD [TermsAcceptedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005095620_RTK_TermsAccepted'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005095620_RTK_TermsAccepted', N'8.0.17');
END;
GO

COMMIT;
GO
