# خطوات تنفيذ Migration — فهارس QuestionAttemptNew (إصلاح بطء GetCurriculumData)

> نفّذ هذا الملف داخل مشروع QdratNew عن طريق Claude Code (اكستنشن كلود كود في VS Code).
> الهدف: توليد Migration فعلية للتغييرات اللي اتعملت بالفعل في الكود، وتطبيقها أولًا على
> بيئة التطوير، وبعدين على الإنتاج بأمان.

## الخلفية

تم بالفعل تعديل ملفين في الكود (لسه ما اتعملهمش Migration ولا Update-Database):

1. `Data/ApplicationDbContext.cs` — إضافة فهرسين جداد على `QuestionAttemptNew` داخل
   `modelBuilder.Entity<QuestionAttemptNew>(entity => { ... })`:
   - `IX_QuestionAttemptNew_StudentId_HomeworkSetId`
   - `IX_QuestionAttemptNew_HomeworkSetId_StudentId`
2. `Areas/Students/Controllers/StudentHomeworkDashboardNewController.cs` — تغليف استعلام
   متوسط الدفعة (`batchHomeworkStats`) بـ `IMemoryCache.GetOrCreateAsync` لمدة 5 دقائق.
   هذا التعديل الثاني لا يحتاج Migration (مفيش تغيير في الـ Schema)، بس يحتاج Build فقط.

السبب الجذري الكامل موثّق في: `Docs/Performance/2026-08-23_QuestionAttemptNew_indexes.sql`
(ملف السكربت ده **متشغلوش** — احتفظ بيه للمرجعية بس، هنمشي بطريقة Code-First / Migration
بدل السكربت اليدوي، عشان النموذج (Model) وقاعدة البيانات يفضلوا متطابقين رسميًا).

## الخطوة 1 — توليد الـ Migration في بيئة التطوير

من جذر مشروع `QdratNew` (فين ملف `QdratNew.csproj`)، شغّل:

```bash
dotnet ef migrations add AddQuestionAttemptNewHomeworkIndexes
```

أو لو بتستخدم Package Manager Console داخل Visual Studio:

```powershell
Add-Migration AddQuestionAttemptNewHomeworkIndexes
```

## الخطوة 2 — راجع ملف الـ Migration الناتج قبل أي تنفيذ

هيتولّد ملف جديد تحت `Migrations/` باسم يبدأ بتاريخ + `_AddQuestionAttemptNewHomeworkIndexes.cs`.

**افتحه وتأكد إنه يحتوي بالظبط على استدعاءين `migrationBuilder.CreateIndex(...)`**
لجدول `QuestionAttemptNew` بس — زي الشكل ده تقريبًا:

```csharp
migrationBuilder.CreateIndex(
    name: "IX_QuestionAttemptNew_StudentId_HomeworkSetId",
    table: "QuestionAttemptNew",
    columns: new[] { "StudentId", "HomeworkSetId" });

migrationBuilder.CreateIndex(
    name: "IX_QuestionAttemptNew_HomeworkSetId_StudentId",
    table: "QuestionAttemptNew",
    columns: new[] { "HomeworkSetId", "StudentId" });
```

⚠️ **لو لقيت في نفس الملف أي عمليات تانية غير متوقعة** (`CreateTable`، `DropColumn`،
`AlterColumn`، `DropIndex` لفهارس تانية، إلخ) — **متكملش**. ده معناه إن فيه تغييرات
سابقة في الموديل (`ApplicationDbContext.cs` أو أي Entity) اتعملت من قبل ولسه ملهاش
Migration مطبّقة، وهتتطبّق كلها مع بعض بالغلط. وقّف واسألني أو راجعها الأول.

## الخطوة 3 — طبّق على بيئة التطوير أولًا

```bash
dotnet ef database update
```

أو:

```powershell
Update-Database
```

بعد كده افتح لوحة تحكم الواجبات لطالب تجريبي في بيئة التطوير وتأكد إن الشاشة شغّالة
عادي ومفيش أي استثناء (Exception).

## الخطوة 4 — تحقّق من الفهارس محليًا (اختياري لكن مستحسن)

على قاعدة بيانات التطوير، شغّل:

```sql
SELECT name, type_desc
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.QuestionAttemptNew');
```

تأكد إن الفهرسين الجداد ظاهرين.

## الخطوة 5 — ولّد سكربت SQL آمن للإنتاج (بدل Update-Database مباشرة على الإنتاج)

بدل ما تشغّل `Update-Database` بـ Connection String بتاع الإنتاج مباشرة، الأنسب لقاعدة
إنتاج إنك تولّد سكربت وتراجعه بعينك الأول:

```bash
dotnet ef migrations script <اسم آخر Migration قبل AddQuestionAttemptNewHomeworkIndexes> AddQuestionAttemptNewHomeworkIndexes --idempotent --output migrate_qa_indexes.sql
```

أو في Package Manager Console:

```powershell
Script-Migration -From <اسم آخر Migration قبل ده> -To AddQuestionAttemptNewHomeworkIndexes -Idempotent
```

افتح السكربت الناتج وتأكد إنه يحتوي على `CREATE INDEX` للفهرسين بس (ممكن يكون ملفوف
جوه شرط `IF NOT EXISTS` أو تحقق من `__EFMigrationsHistory` تلقائيًا — ده طبيعي).

## الخطوة 6 — طبّق على الإنتاج

- شغّل السكربت الناتج من الخطوة 5 مباشرة على قاعدة الإنتاج (عن طريق SSMS أو Azure Data
  Studio أو أي أداة بتستخدمها للاتصال بالآيجر).
- **يُفضَّل تنفيذه في وقت هدوء نسبي على المنصة** (مش وقت اختبار جماعي)، لأن إنشاء
  فهرس على جدول كبير زي `QuestionAttemptNew` ممكن ياخد وقت ويعمل قفل لحظي حسب حجم
  الجدول وخطة SQL Server المستخدمة.
- بعد التنفيذ، تأكد إن جدول `__EFMigrationsHistory` على الإنتاج فيه صف جديد باسم
  Migration ده — ده بيضمن إن أي `dotnet ef database update` قادم على الإنتاج مش
  هيحاول يعيد تنفيذها تاني.

## بعد التنفيذ — راقب الأداء

ارجع لـ Azure Application Insights → Performance بعد ساعة أو ساعتين من ساعات استخدام
حقيقية، وشوف قيمة `GET StudentHomeworkDashboardNew/GetCurriculumData` — المتوقع إنها
تنزل بشكل ملحوظ من ~4.15 ثانية لأقل بكتير (أجزاء من الثانية للطلبات اللي بتضرب الكاش،
واستعلام سريع مفهرس للطلبات اللي بتحسب من جديد).
