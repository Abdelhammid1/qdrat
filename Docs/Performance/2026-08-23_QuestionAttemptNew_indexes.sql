/*
    إصلاح أداء عاجل — لوحة تحكم الواجبات الجديدة (StudentHomeworkDashboardNew/GetCurriculumData)
    تاريخ: 2026-08-23

    السبب الجذري (مؤكد من Application Insights + فحص الكود):
    جدول QuestionAttemptNew (أكبر جدول في القاعدة — يسجل كل محاولة سؤال في الواجبات
    والاختبارات ومؤشرات الأداء) ليس عليه أي فهرس يخدم:
      - الفلترة بـ StudentId + HomeworkSetId (استعلام واجبات طالب واحد)
      - الفلترة/التجميع بـ HomeworkSetId + StudentId (حساب متوسط الدفعة لكل واجب)
    الفهرس الوحيد الموجود مقصور على (StudentId, QuestionId, ExamId) وشرطه ExamId IS NOT NULL
    فقط — لا يفيد استعلامات الواجبات إطلاقًا. النتيجة: Full Table Scan لكل فتحة للوحة
    التحكم، وهو ما يطابق تمامًا ما ظهر في Application Insights:
      - GET StudentHomeworkDashboardNew/GetCurriculumData: 4.15 ثانية متوسط، 125 استدعاء
      - Excessive thread blocking by WaitHandle (25 ثانية إجمالي التأثير)
      - Excessive allocations due to SqlConnection (6%)

    هذا السكربت يطبّق الفهارس فورًا على قاعدة الإنتاج دون انتظار نشر نسخة جديدة من التطبيق.
    تم أيضًا إضافة نفس الفهارس في الكود (Data/ApplicationDbContext.cs عبر Fluent API)
    حتى يتطابق نموذج EF Core مع القاعدة ولا يظهر فرق (diff) عشوائي في أول
    "dotnet ef migrations add" قادم. شغّل هذا الأمر بعدها لتوليد Migration رسمية
    متطابقة (بدون تغييرات فعلية لأن الفهارس هتكون موجودة بالفعل):

        dotnet ef migrations add AddQuestionAttemptNewHomeworkIndexes

    ملاحظة: استخدام CREATE INDEX العادي (بدون ONLINE) قد يقفل الجدول لحظيًا أثناء
    الإنشاء. في SQL Server Standard/Basic (معظم خطط Azure SQL الاقتصادية) لا يتوفر
    خيار ONLINE = ON. يُفضّل تنفيذ هذا السكربت في وقت هدوء نسبي على المنصة
    (خارج أوقات الاختبارات الجماعية) تحسبًا لأي قفل قصير.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_QuestionAttemptNew_StudentId_HomeworkSetId'
      AND object_id = OBJECT_ID('dbo.QuestionAttemptNew')
)
BEGIN
    CREATE INDEX IX_QuestionAttemptNew_StudentId_HomeworkSetId
        ON dbo.QuestionAttemptNew (StudentId, HomeworkSetId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_QuestionAttemptNew_HomeworkSetId_StudentId'
      AND object_id = OBJECT_ID('dbo.QuestionAttemptNew')
)
BEGIN
    CREATE INDEX IX_QuestionAttemptNew_HomeworkSetId_StudentId
        ON dbo.QuestionAttemptNew (HomeworkSetId, StudentId)
        INCLUDE (IsCorrect);
END
GO

-- للتحقق بعد التنفيذ:
-- SELECT name, type_desc FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.QuestionAttemptNew');
