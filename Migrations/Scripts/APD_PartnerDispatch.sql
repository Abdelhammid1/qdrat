-- APD-S1.1: جمهور الإرسال للشركاء — أعمدة الملكية وعلَم عقد معمل القياس وسجل الإرسال.
-- OwnerPartnerId على HomeworkSets وPerformanceIndicatorExams وMinistrySimExamAssignmentsToBatches/ToStudents (null = ملك المعهد)،
-- PartnerSubscriptions.CanUseMinistrySimExams (افتراضي 0)، جدول PartnerDispatchLogs، والفهارس والمفاتيح الأجنبية (NO ACTION).
-- منح مبدئي لـ PartnerDispatch.Send لمن يملك AdminLessonCompletions.ManageModelHomework.
-- لا Backfill للملكية: كل ما أُرسل قبل APD يبقى null. قابل لإعادة التشغيل. يُطبَّق بعد Backup وقبل نشر الكود.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [PerformanceIndicatorExams] ADD [OwnerPartnerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [PartnerSubscriptions] ADD [CanUseMinistrySimExams] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [MinistrySimExamAssignmentsToStudents] ADD [OwnerPartnerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [MinistrySimExamAssignmentsToBatches] ADD [OwnerPartnerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [HomeworkSets] ADD [OwnerPartnerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE TABLE [PartnerDispatchLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [DispatchGroupId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [SourceProfessionalModelId] int NULL,
        [SourceMinistrySimExamId] int NULL,
        [SourcePerformanceIndicatorExamId] int NULL,
        [PartnerId] int NOT NULL,
        [BatchId] int NOT NULL,
        [TargetEntityId] int NOT NULL,
        [StudentsCount] int NOT NULL,
        [ContractOverridden] bit NOT NULL,
        [OverrideReason] nvarchar(500) NULL,
        [SentByUserId] nvarchar(450) NOT NULL,
        [SentAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PartnerDispatchLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PartnerDispatchLogs_Partners_PartnerId] FOREIGN KEY ([PartnerId]) REFERENCES [Partners] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE INDEX [IX_PerformanceIndicatorExams_OwnerPartnerId] ON [PerformanceIndicatorExams] ([OwnerPartnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE INDEX [IX_MinistrySimExamAssignmentsToStudents_OwnerPartnerId] ON [MinistrySimExamAssignmentsToStudents] ([OwnerPartnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE INDEX [IX_MinistrySimExamAssignmentsToBatches_OwnerPartnerId_BatchId] ON [MinistrySimExamAssignmentsToBatches] ([OwnerPartnerId], [BatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE INDEX [IX_HomeworkSets_OwnerPartnerId_BatchId] ON [HomeworkSets] ([OwnerPartnerId], [BatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE INDEX [IX_PartnerDispatchLogs_DispatchGroupId] ON [PartnerDispatchLogs] ([DispatchGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE INDEX [IX_PartnerDispatchLogs_PartnerId_Kind] ON [PartnerDispatchLogs] ([PartnerId], [Kind]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    CREATE INDEX [IX_PartnerDispatchLogs_SentAtUtc] ON [PartnerDispatchLogs] ([SentAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [HomeworkSets] ADD CONSTRAINT [FK_HomeworkSets_Partners_OwnerPartnerId] FOREIGN KEY ([OwnerPartnerId]) REFERENCES [Partners] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [MinistrySimExamAssignmentsToBatches] ADD CONSTRAINT [FK_MinistrySimExamAssignmentsToBatches_Partners_OwnerPartnerId] FOREIGN KEY ([OwnerPartnerId]) REFERENCES [Partners] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [MinistrySimExamAssignmentsToStudents] ADD CONSTRAINT [FK_MinistrySimExamAssignmentsToStudents_Partners_OwnerPartnerId] FOREIGN KEY ([OwnerPartnerId]) REFERENCES [Partners] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    ALTER TABLE [PerformanceIndicatorExams] ADD CONSTRAINT [FK_PerformanceIndicatorExams_Partners_OwnerPartnerId] FOREIGN KEY ([OwnerPartnerId]) REFERENCES [Partners] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN

    INSERT INTO AdminProfileControllerPermissions (AdminProfileId, ControllerName, AccessLevel, HasCustomPermissions)
    SELECT DISTINCT cp.AdminProfileId, N'PartnerDispatch', 1, 1
    FROM AdminProfileControllerPermissions cp
    WHERE cp.ControllerName = N'AdminLessonCompletions'
      AND ((cp.HasCustomPermissions = 0 AND cp.AccessLevel >= 2)
           OR (cp.HasCustomPermissions = 1 AND EXISTS (
                SELECT 1 FROM AdminProfileCustomPermissions c
                WHERE c.AdminProfileControllerPermissionId = cp.Id
                  AND c.ActionName = N'ManageModelHomework'
                  AND c.IsAllowed = 1)))
      AND NOT EXISTS (
            SELECT 1 FROM AdminProfileControllerPermissions x
            WHERE x.AdminProfileId = cp.AdminProfileId AND x.ControllerName = N'PartnerDispatch');

    INSERT INTO AdminProfileCustomPermissions (AdminProfileControllerPermissionId, ActionName, IsAllowed)
    SELECT p.Id, N'Send', 1
    FROM AdminProfileControllerPermissions p
    WHERE p.ControllerName = N'PartnerDispatch'
      AND NOT EXISTS (
            SELECT 1 FROM AdminProfileCustomPermissions c
            WHERE c.AdminProfileControllerPermissionId = p.Id);

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105004750_APD_PartnerDispatch', N'8.0.17');
END;
GO

COMMIT;
GO

