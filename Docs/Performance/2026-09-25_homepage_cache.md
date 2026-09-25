# كاش الواجهة العامة (الصفحة الرئيسية + الـ navbar + الشهادات والمجموعات)

Epic HP · Sprints 1–3 · 2026-09-25

## المشكلة
رندر `/` كان يُنفّذ **19 استعلامًا متتاليًا** لكل زائر مجهول (8 partials تحقن `ApplicationDbContext` + navbar الـ Layout + قسما الشهادات المهنية ومجموعات الدورات)، وكل صفحة تستخدم `_FrontendLayout` (~30 صفحة عامة/Identity) كانت تنفّذ **3 استعلامات** للـ navbar، بلا كاش.

## الحل
| المكوّن | المسار | الدور |
|---|---|---|
| `HomePageCache` | `Services/Frontend/HomePage/HomePageCache.cs` | مفاتيح الكاش + `GetOrLoadAsync` بقفل `SemaphoreSlim` لكل مفتاح (منع stampede) |
| `HomePageContentService` | `Services/Frontend/HomePage/` | `GetHomeContentAsync` (محتوى الرئيسية) و`GetNavigationAsync` (navbar) |
| `HomePageCacheInvalidationInterceptor` | `Data/Interceptors/` | إبطال الكاش تلقائيًا عند `SaveChanges` على الجداول المعنية |
| `ProfessionalCertificateService` / `CourseCollectionService` | `Services/Frontend/...` | `GetHomeSectionAsync` مُخزَّنة بمدة 60 ثانية (فيها عدّادات تسجيل) |

## المفاتيح والمدد
| المفتاح | المحتوى | المدة |
|---|---|---|
| `HP:HomeContent` | Hero، الصفحات الأربع، الشركاء، الأهداف، إعداد Petrojet، البرامج | 10 دقائق |
| `HP:FrontendCatalog` | الدورات الفعّالة + SubCourses الفعّالة (مصدر البرامج والـ navbar) | 10 دقائق |
| `HP:ProfCertSection` | قسم الشهادات المهنية | 60 ثانية |
| `HP:CourseCollectionsSection` | قسم مجموعات الدورات | 60 ثانية |

المدة الطويلة شبكة أمان فقط؛ الإبطال الفعلي فوري عبر الـ Interceptor.

## الإبطال
الـ Interceptor (Singleton) مسجّل على `AddDbContext<ApplicationDbContext>` ويرثه `PooledDbContextFactory`. يلتقط الكيانات Added/Modified/Deleted ويُبطل المفاتيح المعنية **قبل** الحفظ و**بعده** (أو عند فشله) حتى لا يعيد طلب متزامن تعبئة الكاش بقيمة قديمة.

**حدّ معروف:** `ExecuteUpdate` / `ExecuteDelete` / SQL الخام لا تمر بالـ Interceptor — أي استخدام لها على هذه الجداول يجب أن يستدعي `HomePageCache.InvalidateAll` يدويًا.

## القاعدة الدائمة
**ممنوع حقن `ApplicationDbContext` داخل أي `.cshtml`.** البيانات تأتي من Model أو ViewComponent أو خدمة (ADR-HP-02).

## نتائج التحقق (HP-S3) — بيئة التطوير المحلية، زائر مجهول
عدّ الأوامر `Executed DbCommand` في سجل Serilog، مقارنة بنسخة ما قبل HP-S1 (commit `103ccd9`):

| الطلب | قبل | بعد |
|---|---|---|
| `/` — الأول بعد التشغيل (تعبئة الكاش) | 19 | 13 |
| `/` — الثاني والثالث | 19 | **0** |
| `/Identity/Account/Login` | 3 | **0** |

- **مطابقة HTML:** صفحة Login مطابقة حرفيًا (عدا hash روابط CSS وسطر فارغ ناتج عن حذف `@using/@inject`). الرئيسية: الفرق الوحيد ترتيب روابط «سجّل الآن» داخل SubCourses (الاستثناء الموثّق: `DisplayOrder`)، عدا hash الـ CSS وسطر فارغ.
- **الإبطال:** مغطى باختبارات `QdratNew.Tests/HomePageCacheInvalidationTests.cs` (3 اختبارات ناجحة): الاستدعاء الثاني يعيد نفس النسخة المخزنة؛ حفظ `HeroSlide` يُبطل `ContentKey` و`CatalogKey` ويظهر التعديل في الاستدعاء التالي؛ حفظ كيان غير معني لا يُبطل شيئًا.

## مخاطر متبقية
- `IMemoryCache` لا يُبطل عبر أكثر من instance؛ عند التوسع يلزم Redis/`IDistributedCache` أو رسائل إبطال.
- اختبار الحمل `hey -z 30s -c 50` على السيرفر الفعلي (HP-S3.3) يُنفَّذ بعد النشر مع مهندس السيرفر؛ إن بقي فشل بدون الـ workaround فمصدره تذكرة #2 (sync في Controllers/Services) لا هذه الصفحة.
