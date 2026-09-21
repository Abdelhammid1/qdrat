# تقرير أمني عربي لمشروع قدرات QdratNew

تاريخ التقرير: 2026-06-04  
نوع التقرير: فحص أمني للإنتاج، تقرير فقط بدون تعديل الكود  
المسار: `D:\web\site2025\QdratNew`

## القرار التنفيذي

القرار الحالي: **لا أنصح بالإطلاق على الإنتاج بالحالة الحالية**.

السبب ليس وجود ملاحظات تجميلية، بل وجود ثغرات تؤثر مباشرة على:

- بيانات الطلاب.
- بيانات الشركاء.
- قاعدة بيانات الإنتاج.
- لوحات إدارية وتشغيلية حساسة.

أهم ما يمنع الإطلاق الآن:

1. إمكانية اختيار شريك `partnerId` بدون تحقق كاف، ثم استخدامه للوصول لبيانات هذا الشريك.
2. وجود بيانات اتصال قاعدة بيانات الإنتاج داخل `appsettings.Production.json`.
3. لوحة Hangfire مسجلة بدون صلاحيات واضحة في الكود.
4. وجود بعض صفحات الطلاب تعتمد على أرقام IDs من الطلب بدون ربطها بالمستخدم الحالي.
5. وجود endpoint عام لطباعة PDF يقبل اسم View من المستخدم.

## ملخص النتائج

| المستوى | العدد | المعنى |
|---|---:|---|
| Critical | 2 | يجب إصلاحها فورًا قبل الإنتاج |
| High | 4 | خطر عالٍ ويحتاج إصلاح سريع |
| Medium | 3 | تقوية أمان مهمة للإنتاج |

إجمالي النتائج: **9 نتائج أمنية**.

## الثغرات الحرجة

### 1. اختيار الشريك بدون تحقق كاف

الملفات المهمة:

- `Areas\Partner\Controllers\PartnerContextController.cs`
- `Areas\Partner\Controllers\PartnerBaseController.cs`
- `Areas\Partner\Controllers\StudentsController.cs`

المشكلة:

يوجد Action اسمه `Set` يأخذ `partnerId` ويحفظه في Session باسم `ActivePartnerId`. التحقق الموجود يتأكد فقط أن الشريك موجود في قاعدة البيانات، لكنه لا يتأكد أن المستخدم الحالي له حق الوصول لهذا الشريك.

الأثر:

لو عرف شخص رقم شريك صحيح، يمكنه ضبط الجلسة على هذا الشريك، وبعدها بعض صفحات Partner تعتمد على هذا الرقم لجلب الطلاب أو الدفعات أو الواجبات أو الاختبارات.

درجة الخطورة: **Critical**

الإصلاح المطلوب:

- إضافة `[Authorize(Policy = "PartnerOnly")]` على مستوى Partner area أو `PartnerBaseController`.
- في `PartnerContextController.Set` يجب التحقق من أن `partnerId` مرتبط بالمستخدم الحالي داخل جدول مثل `UserPartners`.
- إضافة `[ValidateAntiForgeryToken]` على POST.
- اختبار أن مستخدم شريك لا يستطيع اختيار شريك آخر.

### 2. بيانات قاعدة الإنتاج داخل ملف الإعدادات

الملف:

- `appsettings.Production.json`

المشكلة:

Connection string يحتوي Server و User ID و Password لقاعدة Azure SQL الإنتاجية.

الأثر:

أي شخص يصل إلى السورس أو نسخة build أو backup قد يحصل على بيانات دخول قاعدة الإنتاج. هذا خطر مباشر على كل بيانات النظام.

درجة الخطورة: **Critical**

الإصلاح المطلوب فورًا:

- تغيير كلمة مرور قاعدة البيانات فورًا.
- حذف كلمة المرور من `appsettings.Production.json`.
- استخدام Azure App Settings أو Key Vault أو Environment Variables.
- فحص Git history وأي artifacts قد تحتوي نفس السر.

## الثغرات عالية الخطورة

### 3. Hangfire Dashboard بدون حماية واضحة

الملف:

- `Program.cs`

المشكلة:

يوجد:

```csharp
app.UseHangfireDashboard();
```

بدون فلتر Authorization واضح.

الأثر:

قد تظهر لوحة jobs أو معلومات تشغيلية أو تحكم بالمهام المجدولة إذا كانت متاحة خارجيًا.

درجة الخطورة: **High**

الإصلاح:

- تقييد Hangfire dashboard بأدوار مثل `SuperAdmin`, `Owner`, `Developer`.
- أو تعطيلها في الإنتاج.
- اختبار أن المستخدم المجهول يحصل على 401 أو 403.

### 4. صفحات Study Sessions للطلاب لا تتحقق من ملكية البيانات

الملفات:

- `Areas\Students\Controllers\StudySessionsController.cs`
- `Areas\Students\Controllers\SessionRatingsController.cs`

المشكلة:

بعض Actions تقرأ أو تعدل جلسات باستخدام `id` أو `sessionId` مباشرة بدون التأكد أن الجلسة تخص الطالب الحالي. يوجد أيضًا `studentId = 3` بشكل مؤقت داخل الكود.

الأثر:

قد يستطيع مستخدم الوصول إلى تفاصيل أو تعديل بيانات جلسات ليست له إذا عرف ID صحيح.

درجة الخطورة: **High**

الإصلاح:

- إضافة `[Authorize(Roles = "Student")]`.
- استخراج الطالب الحالي من `UserManager` أو `StudentBaseController`.
- منع استخدام `StudentId` القادم من form أو query في عمليات تخص الطالب.
- كل query يجب أن تحتوي شرط ملكية مثل `StudentID == currentStudentId`.

### 5. AI Recommendation تقبل `studentId` من الطلب

الملف:

- `Areas\Students\Controllers\AIRecommendationController.cs`

المشكلة:

Action `Index(int studentId)` يستخدم الرقم القادم من الطلب لجلب أداء الطالب.

الأثر:

قد يتم عرض أداء وتوصيات طالب آخر بمجرد تغيير الرقم.

درجة الخطورة: **High**

الإصلاح:

- حذف `studentId` من الطلب للطالب العادي.
- جلب الطالب من المستخدم الحالي.
- إذا كان الاستخدام إداريًا، يتم نقله إلى مسار Admin محمي بصلاحيات.

### 6. PrintController يطبع أي View بناءً على اسم من المستخدم

الملف:

- `Controllers\PrintController.cs`

المشكلة:

يوجد endpoint عام:

```csharp
ViewAsPdf(string viewName, string area = "", string controller = "", string id = null)
```

يقبل اسم View و Area و Controller من الطلب.

الأثر:

قد يؤدي إلى تجاوز صلاحيات التقارير أو محاولة عرض Views داخلية بشكل غير مقصود.

درجة الخطورة: **High**

الإصلاح:

- حذف الطباعة العامة.
- إنشاء Actions محددة لكل تقرير.
- كل تقرير يجب أن يستخدم نفس صلاحيات الصفحة الأصلية.
- استخدام allowlist صارمة إذا بقيت الفكرة العامة.

## ملاحظات متوسطة الخطورة

### 7. Cookie الخاصة بالتخفي Impersonation ليست Secure

الملف:

- `Services\ImpersonationService.cs`

المشكلة:

الكود يحتوي:

```csharp
Secure = false
```

مع أنها Cookie حساسة تغير هوية المستخدم داخل الطلب.

درجة الخطورة: **Medium**

الإصلاح:

- جعلها `Secure = true`.
- أو استخدام `CookieSecurePolicy.Always`.
- يفضل إضافة مدة صلاحية واضحة وتسجيل Audit لكل عملية تخفي.

### 8. سياسة كلمات المرور والجلسات ضعيفة للإنتاج

الملف:

- `Program.cs`

المشكلة:

كلمات المرور تسمح بطول 6 وبدون أرقام أو حروف كبيرة أو رموز. الجلسة 6 ساعات.

درجة الخطورة: **Medium**

الإصلاح:

- تقوية كلمات المرور.
- تفعيل lockout عند محاولات فاشلة.
- فرض MFA على أدوار الإدارة.
- تقليل مدة الجلسة للأدوار الحساسة.

### 9. وجود حزم NuGet عليها ثغرات معروفة

تم تشغيل:

```powershell
dotnet list QdratNew.csproj package --vulnerable --include-transitive
```

وظهرت حزم transitive عليها advisories، منها:

- `Microsoft.Build 17.8.3` بدرجة High.
- `Npgsql 8.0.0` بدرجة High.
- `System.Data.SqlClient 4.4.0` بدرجة High/Moderate.
- `System.IO.Packaging 6.0.0` بدرجة High.

درجة الخطورة: **Medium** حاليًا، وقد تصبح High إذا ثبت أن مسارات رفع أو قراءة Excel/PDF/ملفات تستخدم هذه الحزم بشكل مباشر مع مدخلات المستخدم.

الإصلاح:

- تحديث الحزم المباشرة التي تسحب هذه dependencies.
- إعادة تشغيل نفس أمر الفحص.
- اختبار مسارات Excel/import/PDF بعد التحديث.

## خطة العمل المقترحة

### خلال نفس اليوم

1. تغيير كلمة مرور قاعدة بيانات الإنتاج.
2. إزالة ConnectionString الإنتاج من الملفات.
3. حجب مؤقت للمسارات الحساسة إن كان النشر قائمًا:
   - `/Partner/*`
   - `/hangfire`
   - `/Print/ViewAsPdf`
   - مسارات Study Sessions و AIRecommendation للطلاب

### خلال 24 ساعة

1. إصلاح Partner authorization.
2. ربط اختيار الشريك بالمستخدم الحالي.
3. حماية Hangfire.
4. حماية PrintController أو إلغاؤه.

### خلال 48-72 ساعة

1. إصلاح Student IDOR.
2. إضافة اختبارات تكامل:
   - Anonymous access.
   - Cross-partner access.
   - Cross-student access.
   - CSRF.
   - Admin-only dashboard.
3. تحديث حزم NuGet الضعيفة.
4. تشغيل فحص أمني آخر بعد الإصلاح.

## قرار الإنتاج النهائي

الحالة الحالية: **غير جاهز للإنتاج**.

يمكن إعادة تقييم القرار بعد تنفيذ هذه الشروط:

- لا توجد أسرار إنتاجية في الملفات.
- Partner area محمية مركزيًا.
- كل اختيار شريك مربوط بالمستخدم الحالي.
- كل Student action يستخدم الطالب الحالي لا ID من الطلب.
- Hangfire محمية أو معطلة.
- PrintController العام محذوف أو مقيد.
- فحص NuGet لا يظهر High vulnerabilities قابلة للوصول.

## الملفات المرتبطة

- التقرير الإنجليزي الكامل: `SecurityReports\codex-security-2026-06-04\report.md`
- نسخة HTML مختصرة: `SecurityReports\codex-security-2026-06-04\report.html`
- هذا التقرير العربي: `SecurityReports\codex-security-2026-06-04\report-ar.md`

