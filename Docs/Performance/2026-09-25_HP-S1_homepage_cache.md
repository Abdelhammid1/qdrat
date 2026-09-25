# HP-S1 — كاش الصفحة الرئيسية وإزالة DbContext من الـ Partials

التاريخ: 2026-09-25 · Epic HP · Sprint 1 (Stories HP-S1.1 → HP-S1.5) — مكتمل

## المشكلة
رندر `/` كان ينفّذ استعلامات متتالية على قاعدة البيانات لكل زائر (8 partials تحقن `ApplicationDbContext` مباشرة داخل الـ Views)، بدون كاش، وبعضها بدون `AsNoTracking`.

## الحل
محتوى الصفحة الرئيسية يُحمَّل مرة واحدة ويُخزَّن في `IMemoryCache`، والـ Views تقرأ من `Model` فقط.

| المكوّن | المسار | الدور |
|---|---|---|
| `HomePageCache` | `Services/Frontend/HomePage/HomePageCache.cs` | مفاتيح الكاش + `GetOrLoadAsync` بقفل `SemaphoreSlim` لكل مفتاح (منع stampede) |
| `IHomePageContentService` / `HomePageContentService` | `Services/Frontend/HomePage/` | تحميل المحتوى بالاستعلامات المتتالية (DbContext غير آمن للتوازي) وتقديم `GetHomeContentAsync` و`GetNavigationAsync` |
| `HomePageContentViewModel` / `FrontendNavViewModel` | `ViewModels/Public/HomePage/` | لقطة للقراءة فقط مشتركة بين الزوار |
| `HomePageCacheInvalidationInterceptor` | `Data/Interceptors/` | إبطال الكاش تلقائيًا عند `SaveChanges` على الجداول المعنية |

## مفاتيح الكاش ومدتها
- `HP:HomeContent` و`HP:FrontendCatalog` — 10 دقائق (شبكة أمان؛ الإبطال الفعلي فوري).
- `HP:ProfCertSection` و`HP:CourseCollectionsSection` — مفاتيح معرّفة ومربوطة بالـ Interceptor، ويُفعَّل استخدامها في HP-S2 (مدة 60 ثانية للعدّادات).

## الإبطال
الـ Interceptor (Singleton) مسجّل على `AddDbContext<ApplicationDbContext>` ويرثه `PooledDbContextFactory` لأنه يقرأ نفس `DbContextOptions`. يلتقط الكيانات Added/Modified/Deleted:
- `HeroSlide, SuccessPartner, StaticPage, CorporateRegistrationSetting, PlatformGoal, FrontendCourse, SubCourse` ← مفاتيح المحتوى والكتالوج.
- كيانات الشهادات المهنية ومجموعات الدورات ← مفتاح القسم الخاص بها.

يُبطل قبل الحفظ وبعد نجاحه (أو فشله) حتى لا يعيد طلب متزامن تعبئة الكاش بقيمة قديمة.

**حدّ معروف:** `ExecuteUpdate` / `ExecuteDelete` / SQL الخام لا تمر بالـ Interceptor؛ أي استخدام لها على هذه الجداول يجب أن يستدعي `HomePageCache.InvalidateAll` يدويًا.

## الملفات المعدّلة
- `Program.cs` — تسجيل الـ Interceptor والخدمة وربطه بـ `AddDbContext`.
- `Controllers/HomeController.cs` — `Index` async يمرّر الموديل.
- `Views/Home/Index.cshtml` — `@model` وتمرير `Model` للأقسام الثمانية.
- `Views/Shared/MainPageSections/`: `_HeroSection, _ProgramsAndCoursesSection, _AboutUsSection, _VisionMissionSection, _PartnersSliderSection, _PlatformGoalsSection, _SaudiVisionSection, _PartnersSection` — حذف `@inject ApplicationDbContext` وقراءة من `Model` بنفس أسماء المتغيرات (HTML الناتج لم يتغير).

## الحفاظ على السلوك
- نفس شروط الاستعلامات (`IsActive`، الـ slugs الأربعة، ترتيب `DisplayOrder`).
- الاستثناء الموثّق الوحيد: قائمة `SubCourses` في قسم البرامج صارت مرتّبة بـ `DisplayOrder`.
- لا `Contains` على قائمة (توافق SQL Server 2014)، كل القراءات `AsNoTracking`.

## القاعدة الجديدة
**ممنوع حقن `ApplicationDbContext` داخل أي `.cshtml`** — البيانات تأتي من Model أو ViewComponent أو خدمة.

## مخاطر متبقية / ملاحظات
- `IMemoryCache` لا يُبطل عبر أكثر من instance؛ عند التوسع يلزم Redis/`IDistributedCache`.
- الـ Layout (navbar) وأقسام الشهادات/المجموعات وبوابة الشريك ضمن HP-S2 ولم تُنفَّذ بعد.
- لم يُنفَّذ بعد فحص المتصفح ومطابقة HTML وعدّ الاستعلامات (HP-S3).
