-- PPL-S2.1: ملكية الاختبار للشريك Exams.OwnerPartnerId (null = اختبار المعهد) + الفهارس والمفتاح الأجنبي.
-- إضافات فقط (عمود NULL بلا تعديل بيانات) وقابل لإعادة التشغيل.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102004719_PPL_ExamOwnerPartner'
)
BEGIN
    ALTER TABLE [Exams] ADD [OwnerPartnerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102004719_PPL_ExamOwnerPartner'
)
BEGIN
    CREATE INDEX [IX_Exams_OwnerPartnerId] ON [Exams] ([OwnerPartnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102004719_PPL_ExamOwnerPartner'
)
BEGIN
    CREATE INDEX [IX_Exams_Type_OwnerPartnerId] ON [Exams] ([Type], [OwnerPartnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102004719_PPL_ExamOwnerPartner'
)
BEGIN
    ALTER TABLE [Exams] ADD CONSTRAINT [FK_Exams_Partners_OwnerPartnerId] FOREIGN KEY ([OwnerPartnerId]) REFERENCES [Partners] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102004719_PPL_ExamOwnerPartner'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102004719_PPL_ExamOwnerPartner', N'8.0.17');
END;
GO

COMMIT;
GO

