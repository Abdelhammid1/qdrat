-- PPM-S1 (تراجع): يحذف IsForPartners وSourceProfessionalModelId من HomeworkSets وExams مع الفهارس والمفاتيح الأجنبية.
-- ⚠ بعد الحذف يُفقد اختيار «داخلي» وتتبع مصدر ما أُرسل من نماذج. روابط ProfessionalModelPartners لا تتأثر.
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود PPM من الإنتاج (الكود الجديد يقرأ الأعمدة).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Exams_ProfessionalModels_SourceProfessionalModelId' AND parent_object_id = OBJECT_ID(N'[Exams]'))
        ALTER TABLE [Exams] DROP CONSTRAINT [FK_Exams_ProfessionalModels_SourceProfessionalModelId];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_HomeworkSets_ProfessionalModels_SourceProfessionalModelId' AND parent_object_id = OBJECT_ID(N'[HomeworkSets]'))
        ALTER TABLE [HomeworkSets] DROP CONSTRAINT [FK_HomeworkSets_ProfessionalModels_SourceProfessionalModelId];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Exams_SourceProfessionalModelId' AND object_id = OBJECT_ID(N'[Exams]'))
        DROP INDEX [IX_Exams_SourceProfessionalModelId] ON [Exams];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HomeworkSets_SourceProfessionalModelId_BatchId' AND object_id = OBJECT_ID(N'[HomeworkSets]'))
        DROP INDEX [IX_HomeworkSets_SourceProfessionalModelId_BatchId] ON [HomeworkSets];

    IF COL_LENGTH(N'[Exams]', N'SourceProfessionalModelId') IS NOT NULL
        EXEC(N'ALTER TABLE [Exams] DROP COLUMN [SourceProfessionalModelId]');

    IF COL_LENGTH(N'[HomeworkSets]', N'SourceProfessionalModelId') IS NOT NULL
        EXEC(N'ALTER TABLE [HomeworkSets] DROP COLUMN [SourceProfessionalModelId]');

    IF COL_LENGTH(N'[ProfessionalModels]', N'IsForPartners') IS NOT NULL
    BEGIN
        DECLARE @df sysname = (SELECT dc.name FROM sys.default_constraints dc
            JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[ProfessionalModels]') AND c.name = N'IsForPartners');
        IF @df IS NOT NULL
            EXEC(N'ALTER TABLE [ProfessionalModels] DROP CONSTRAINT [' + @df + N']');
        EXEC(N'ALTER TABLE [ProfessionalModels] DROP COLUMN [IsForPartners]');
    END;

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261103000022_PPM_PartnerModels';
END;
GO

COMMIT;
GO
