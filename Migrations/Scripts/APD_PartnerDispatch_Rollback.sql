-- APD-S1.1 (تراجع): يحذف أعمدة الملكية وCanUseMinistrySimExams وجدول PartnerDispatchLogs وصلاحيات وحدة PartnerDispatch.
-- ⚠ بعد الحذف يُفقد سجل الإرسال، وكل ما أُرسل لشريك يعود ظاهرًا للأدمن كملك المعهد (OwnerPartnerId يُحذف).
-- لا يُطبَّق هذا السكربت إلا بعد سحب كود APD من الإنتاج (الكود الجديد يقرأ الأعمدة). قابل لإعادة التشغيل.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch'
)
BEGIN
    DELETE c FROM AdminProfileCustomPermissions c
    INNER JOIN AdminProfileControllerPermissions p ON p.Id = c.AdminProfileControllerPermissionId
    WHERE p.ControllerName = N'PartnerDispatch';

    DELETE FROM AdminProfileControllerPermissions WHERE ControllerName = N'PartnerDispatch';

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_HomeworkSets_Partners_OwnerPartnerId' AND parent_object_id = OBJECT_ID(N'[HomeworkSets]'))
        ALTER TABLE [HomeworkSets] DROP CONSTRAINT [FK_HomeworkSets_Partners_OwnerPartnerId];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MinistrySimExamAssignmentsToBatches_Partners_OwnerPartnerId' AND parent_object_id = OBJECT_ID(N'[MinistrySimExamAssignmentsToBatches]'))
        ALTER TABLE [MinistrySimExamAssignmentsToBatches] DROP CONSTRAINT [FK_MinistrySimExamAssignmentsToBatches_Partners_OwnerPartnerId];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MinistrySimExamAssignmentsToStudents_Partners_OwnerPartnerId' AND parent_object_id = OBJECT_ID(N'[MinistrySimExamAssignmentsToStudents]'))
        ALTER TABLE [MinistrySimExamAssignmentsToStudents] DROP CONSTRAINT [FK_MinistrySimExamAssignmentsToStudents_Partners_OwnerPartnerId];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PerformanceIndicatorExams_Partners_OwnerPartnerId' AND parent_object_id = OBJECT_ID(N'[PerformanceIndicatorExams]'))
        ALTER TABLE [PerformanceIndicatorExams] DROP CONSTRAINT [FK_PerformanceIndicatorExams_Partners_OwnerPartnerId];

    IF OBJECT_ID(N'[PartnerDispatchLogs]', N'U') IS NOT NULL
        DROP TABLE [PartnerDispatchLogs];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PerformanceIndicatorExams_OwnerPartnerId' AND object_id = OBJECT_ID(N'[PerformanceIndicatorExams]'))
        DROP INDEX [IX_PerformanceIndicatorExams_OwnerPartnerId] ON [PerformanceIndicatorExams];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MinistrySimExamAssignmentsToStudents_OwnerPartnerId' AND object_id = OBJECT_ID(N'[MinistrySimExamAssignmentsToStudents]'))
        DROP INDEX [IX_MinistrySimExamAssignmentsToStudents_OwnerPartnerId] ON [MinistrySimExamAssignmentsToStudents];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MinistrySimExamAssignmentsToBatches_OwnerPartnerId_BatchId' AND object_id = OBJECT_ID(N'[MinistrySimExamAssignmentsToBatches]'))
        DROP INDEX [IX_MinistrySimExamAssignmentsToBatches_OwnerPartnerId_BatchId] ON [MinistrySimExamAssignmentsToBatches];

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HomeworkSets_OwnerPartnerId_BatchId' AND object_id = OBJECT_ID(N'[HomeworkSets]'))
        DROP INDEX [IX_HomeworkSets_OwnerPartnerId_BatchId] ON [HomeworkSets];

    IF COL_LENGTH(N'[PerformanceIndicatorExams]', N'OwnerPartnerId') IS NOT NULL
        EXEC(N'ALTER TABLE [PerformanceIndicatorExams] DROP COLUMN [OwnerPartnerId]');

    IF COL_LENGTH(N'[MinistrySimExamAssignmentsToStudents]', N'OwnerPartnerId') IS NOT NULL
        EXEC(N'ALTER TABLE [MinistrySimExamAssignmentsToStudents] DROP COLUMN [OwnerPartnerId]');

    IF COL_LENGTH(N'[MinistrySimExamAssignmentsToBatches]', N'OwnerPartnerId') IS NOT NULL
        EXEC(N'ALTER TABLE [MinistrySimExamAssignmentsToBatches] DROP COLUMN [OwnerPartnerId]');

    IF COL_LENGTH(N'[HomeworkSets]', N'OwnerPartnerId') IS NOT NULL
        EXEC(N'ALTER TABLE [HomeworkSets] DROP COLUMN [OwnerPartnerId]');

    -- عمود bit له قيد افتراضي من الـ Migration يجب حذفه قبل العمود
    IF COL_LENGTH(N'[PartnerSubscriptions]', N'CanUseMinistrySimExams') IS NOT NULL
    BEGIN
        DECLARE @df sysname;
        SELECT @df = d.name
        FROM sys.default_constraints d
        INNER JOIN sys.columns c ON c.default_object_id = d.object_id
        WHERE d.parent_object_id = OBJECT_ID(N'[PartnerSubscriptions]') AND c.name = N'CanUseMinistrySimExams';

        IF @df IS NOT NULL
            EXEC(N'ALTER TABLE [PartnerSubscriptions] DROP CONSTRAINT [' + @df + N']');

        EXEC(N'ALTER TABLE [PartnerSubscriptions] DROP COLUMN [CanUseMinistrySimExams]');
    END;

    DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261105004750_APD_PartnerDispatch';
END;
GO

COMMIT;
GO
