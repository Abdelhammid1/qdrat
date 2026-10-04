/*
  QRT — تراجع (Rollback) عن Migration: 20261004095208_QRT_QuestionReviewTasks
  يعادل Down() في الـ Migration: يحذف جدولي QuestionReviewTaskItems ثم QuestionReviewTasks ويزيل سطر السجل.

  ⚠️ تحذير: يحذف نهائيًا كل مهام المراجعة وعناصرها (الحجوزات وملاحظات الإرجاع).
     لا يمسّ جدول Questions ولا QuestionAuditLogs (سجل التدقيق يبقى كما هو).
  قبل التشغيل: خذ نسخة احتياطية كاملة من القاعدة، وأوقف نسخة التطبيق التي تحوي QRT
  (وإلا ستفشل صفحات المراجعة وفحص الحجز بخطأ «Invalid object name»).
  آمن لإعادة التشغيل (Idempotent). شغّله بـ sqlcmd -I (QUOTED_IDENTIFIER ON).
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[QuestionReviewTaskItems]', N'U') IS NOT NULL
    DROP TABLE [QuestionReviewTaskItems];
GO

IF OBJECT_ID(N'[QuestionReviewTasks]', N'U') IS NOT NULL
    DROP TABLE [QuestionReviewTasks];
GO

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261004095208_QRT_QuestionReviewTasks';
GO

COMMIT;
GO
