-- PPM-S1.1: إتاحة النماذج الاحترافية للشركاء وتتبع مصدر الإرسال.
-- ProfessionalModels.IsForPartners + HomeworkSets/Exams.SourceProfessionalModelId + الفهارس والمفاتيح الأجنبية.
-- الـ Backfill يحافظ على ما يراه الشركاء اليوم. قابل لإعادة التشغيل. يُطبَّق بعد Backup وقبل نشر الكود.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    ALTER TABLE [ProfessionalModels] ADD [IsForPartners] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN

    UPDATE pm SET IsForPartners = 1
    FROM ProfessionalModels pm
    WHERE pm.IsGlobal = 1
       OR EXISTS (SELECT 1 FROM ProfessionalModelPartners p WHERE p.ProfessionalModelId = pm.Id)
       OR EXISTS (SELECT 1 FROM ProfessionalModelSubscriptionPeriods s WHERE s.ProfessionalModelId = pm.Id);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    ALTER TABLE [HomeworkSets] ADD [SourceProfessionalModelId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    ALTER TABLE [Exams] ADD [SourceProfessionalModelId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    CREATE INDEX [IX_HomeworkSets_SourceProfessionalModelId_BatchId] ON [HomeworkSets] ([SourceProfessionalModelId], [BatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    CREATE INDEX [IX_Exams_SourceProfessionalModelId] ON [Exams] ([SourceProfessionalModelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    ALTER TABLE [Exams] ADD CONSTRAINT [FK_Exams_ProfessionalModels_SourceProfessionalModelId] FOREIGN KEY ([SourceProfessionalModelId]) REFERENCES [ProfessionalModels] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    ALTER TABLE [HomeworkSets] ADD CONSTRAINT [FK_HomeworkSets_ProfessionalModels_SourceProfessionalModelId] FOREIGN KEY ([SourceProfessionalModelId]) REFERENCES [ProfessionalModels] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103000022_PPM_PartnerModels', N'8.0.17');
END;
GO

COMMIT;
GO

