-- PPL-S7.2: فهارس مطلوبة لشاشات اختبار تحديد المستوى (الإرسال، التفاصيل، تقارير الدفعة، قائمة الشريك).
-- الـ Model وSnapshot يعتبران هذه الفهارس موجودة، لكن لا توجد Migration أنشأتها فعليًا، ولم توجد في قاعدة التطوير
-- ولا قاعدة الاختبار (فحص 2026-11-02). لذلك لا تنتج `dotnet ef migrations add` شيئًا لها، وهذا السكربت هو الطريق.
-- Idempotent وإضافات فقط. يُفضَّل تشغيله خارج أوقات الاختبارات الجماعية تحسبًا لقفل قصير أثناء الإنشاء.
-- الأسماء مطابقة لما يتوقعه EF حتى لا تتعارض مع أي Migration لاحقة.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExamStudentStatuses_ExamId' AND object_id = OBJECT_ID(N'[ExamStudentStatuses]'))
    CREATE INDEX [IX_ExamStudentStatuses_ExamId] ON [ExamStudentStatuses] ([ExamId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExamAssignmentsToBatches_ExamId' AND object_id = OBJECT_ID(N'[ExamAssignmentsToBatches]'))
    CREATE INDEX [IX_ExamAssignmentsToBatches_ExamId] ON [ExamAssignmentsToBatches] ([ExamId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExamAssignmentsToBatches_BatchId' AND object_id = OBJECT_ID(N'[ExamAssignmentsToBatches]'))
    CREATE INDEX [IX_ExamAssignmentsToBatches_BatchId] ON [ExamAssignmentsToBatches] ([BatchId]);
GO
