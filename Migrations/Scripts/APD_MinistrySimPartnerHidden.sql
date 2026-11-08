-- APD-S5.2: عمود HiddenByOwnerAt على تكليفات معمل القياس (دفعة وطالب): إخفاء المدرسة الشريكة للاختبار من قائمتها فقط.
-- عمودان يقبلان NULL بلا قيمة افتراضية، فلا يمس الصفوف الحالية. يُطبَّق قبل نشر كود APD-S5 وبعد Backup.
-- يفترض تطبيق APD_PartnerDispatch و APD_PerformanceIndicatorTemplate و MSE_UniqueStudentAnswer قبله.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214613_APD_MinistrySimPartnerHidden'
)
BEGIN
    ALTER TABLE [MinistrySimExamAssignmentsToStudents] ADD [HiddenByOwnerAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214613_APD_MinistrySimPartnerHidden'
)
BEGIN
    ALTER TABLE [MinistrySimExamAssignmentsToBatches] ADD [HiddenByOwnerAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214613_APD_MinistrySimPartnerHidden'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105214613_APD_MinistrySimPartnerHidden', N'8.0.17');
END;
GO

COMMIT;
GO

