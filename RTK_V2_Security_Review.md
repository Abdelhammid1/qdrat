# RTK v2 — مراجعة الأمان والإنتاج (RTK-S12.2)

> التاريخ: 2026-10-07 · النطاق: ما أُضيف/عُدّل في RTK-S8 … S12 (الحذف الناعم، التعديل بعد النشر، وضع المراجعة، تقارير ولي الأمر، طابور الأدمن، الصلاحيات).
> الطريقة: قراءة الكود + بحث نصي (grep) + اختبارات آلية فعلية (وحدات، SQL Server حقيقي، تكامل HTTP، Playwright). لا أسرار في هذا الملف.

## الخلاصة

- **لا Critical ولا High** ضمن نطاق v2. لم يلزم إصلاح قبل الإغلاق.
- **Medium (2):** غياب رؤوس الأمان على مستوى المنصة (سابق وغير مرتبط بـ RTK ولم يُعدَّل)، وعدّادات Rate Limit محلية لكل نسخة.
- **Low (4)** و**ملاحظات (4)** أدناه.
- الدرجة: **جاهز للإنتاج بتحفظ** — الشروط: تطبيق سكربتات SQL بالترتيب (انظر تقرير الإغلاق)، وتأكيد رؤوس الأمان على الحافة (Azure/Cloudflare/IIS) لأنها غير ظاهرة في الكود.

## النتائج

| # | الخطورة | النتيجة | الدليل | المعالجة |
|---|---|---|---|---|
| F1 | Medium | لا رؤوس أمان (`X-Content-Type-Options`، `X-Frame-Options`/`frame-ancestors`، `Referrer-Policy`، CSP) في الكود: بحث `grep` في `*.cs`/`*.config` لا يعيد شيئًا؛ `Program.cs` فيه `UseHsts` (سطر 1280) و`ForwardedHeaders` (1235–1243) فقط. قد تُضبط على الحافة (لم يُتحقق). | `Program.cs` | **لم يُعدَّل** (سلوك عام للمنصة، خارج نطاق v2). يُوصى بطبقة رؤوس أو ضبطها على الحافة، مع CSP تدريجية بسبب `cdn.jsdelivr.net` وYouTube/Vimeo. |
| F2 | Medium | سياسات المعدّل (`rtk-code`، `rtk-ping`، `rtk-exam`، `rtk-parent-report`) `FixedWindow` في الذاكرة ⇒ العدّاد **لكل نسخة**؛ مع التوسّع الأفقي يصير الحد الفعلي = الحد × عدد النسخ. | `Program.cs:1132–1162` | موثَّق. حماية الرقم المرجعي لا تعتمد عليه وحده (`FailedCodeAttempts` في القاعدة). لو ظهر استغلال: Redis/حد على الحافة. |
| F3 | Low | إجراءات الأدمن الجديدة (`SendParentReport`/`ResendParentReport`/`SuppressParentReport`/`ToggleAutoSend`/`SetVideoReview`/`Delete`/`Restore`) بلا Rate Limit. | متحكّمات الأدمن | مقبول: صلاحية صريحة + Antiforgery + نطاق دفعة، وإعادة الإرسال محمية بفاصل دقيقة وRowVersion (مختبر بتزامن حقيقي). |
| F4 | Low | لم تُضف سياسة `rtk-review` (طُلبت «إن كان العرض منفصلًا»): عرض المراجعة هو نفسه `GetAxis`. | `RemedialTrackController.Axis` | قرار موثَّق؛ المسار يرث سياسات المحور القائمة والمراجعة لا تكتب شيئًا. |
| F5 | Low | `ToggleAutoSend` يستعمل صلاحية `RemedialTrackPublications:ManageReview` (لا مفتاح مخصّص). موظف بـ`ManageReview` فقط يستطيع تشغيل/إيقاف الإرسال التلقائي. | `RemedialTrackPublicationsController.ToggleAutoSend` | وفق نص الخطة (طابِق القائم). إن لزم فصلها: مفتاح `ManageAutoSend` في الأماكن الثلاثة. |
| F6 | Low | تسجيل نشاط الأدمن بعد الحفظ بـ`try/catch` يبتلع الفشل ويسجّله في اللوج؛ فقد يُنفَّذ إجراء بلا أثر تدقيقي إن تعطّل المسجِّل. | `RemedialTrackParentReportAdminService.LogAsync` | سلوك مقصود (لا يُبطل الإجراء). المخاطرة محدودة. |
| N1 | ملاحظة | **IDOR:** ولي الأمر لا يقبل معرّفات من العميل؛ غير المملوك/غير المُرسل/الموقوف/المحذوف ← 404 (لا فرق بين غير موجود وغير مملوك). | اختبارات HTTP: `ParentB_CannotReadOrAcknowledge_ParentAReport_Returns404`، `SuppressedAndPendingReports_AreHiddenFromParent` | مغطّى. |
| N2 | ملاحظة | **XSS:** لا `Html.Raw` في Views الأدمن/الطالب/ولي الأمر الخاصة بـRTK (grep فارغ). اسم طالب `<script>alert(1)</script>` يُعرض مُرمَّزًا في قائمة الأدمن وصفحة ولي الأمر. | اختبارا HTTP وPlaywright | مغطّى. |
| N3 | ملاحظة | **Antiforgery + صلاحيات:** كل POST جديد `[ValidateAntiForgeryToken]` + `[AdminPermission]`؛ بلا توكن ← 400، موظف بلا صلاحية ← 403 (7 نقاط نهاية جديدة). الحارس الآلي `RemedialTrackSecurityGuardTests` يمسح كل Action. | `RemedialTrackV2IntegrationTests` | مغطّى. |
| N4 | ملاحظة | **سلامة الدرجات:** وضع المراجعة لا يكتب `VideoProgress` ولا حالة المحور، ونبضاته مرفوضة من الخادم؛ المحور `Locked` لا يُراجَع؛ بعد الإغلاق/انتهاء المدة لا رابط ولا مشغّل. | `RemedialTrackVideoReviewTests`، `ReviewMode_...` (Playwright) | مغطّى. لم يُختبر تشغيل فيديو حقيقي (شبكة خارجية). |

## مراجعة الإنتاج (aspnet-production-review) على ما تغيّر

| البند | النتيجة |
|---|---|
| N+1 / حلقات | لا استعلام ولا `SaveChanges` داخل حلقة في خدمات v2. عدد استعلامات `RemedialTrackReportBuilder.BuildAsync` **ثابت ≤ 6** ولا يزيد بعدد المحاور (اختبار على SQL Server: 2 محاور = 6 محاور). |
| `AsNoTracking` | كل القراءات (الطابور، صفحة ولي الأمر، السياق) بـ`AsNoTracking`. التتبّع فقط حيث يُعدَّل الصف (إرسال/إعادة/إيقاف). |
| Pagination | الطابور 20/صفحة، قائمة ولي الأمر 15/صفحة، قوائم الفلاتر ≤ 100، آخر التعديلات ≤ 20. |
| التوافق مع SQL Server | `Contains` على قائمة دفعات الموظف الصغيرة (نمط `GetIndexAsync` القائم) ينفَّذ كـ`IN` بثوابت تحت `UseCompatibilityLevel(120)`؛ ثبت بتنفيذ فعلي على SQL Server. لا `OPENJSON`. |
| الفهارس | `UX_RemedialTrackParentReports_Enrollment_Axis_Kind` (فريد، يعمل مع `NULL` كقيمة واحدة — مختبر)، `IX_..._Status_CreatedAt` لطابور الأدمن، `IX_..._Parent_Status_CreatedAt` لصفحة ولي الأمر، و`UX_RemedialTrackPublications_ActiveCode` مُصفّى بـ`IsDeleted = 0` (إعادة استعمال الرقم المحذوف مختبرة). |
| التزامن | إنشاء التقرير: 8 تسليمات متزامنة ← تقرير واحد وإشعار واحد. إعادة الإرسال/الإرسال اليدوي: 6 طلبات متزامنة ← واحد ينجح فقط (`RowVersion`). الإقرار: 6 متزامنة بلا استثناء ووقت واحد. |
| الترحيلات | إضافات فقط، سكربتات Idempotent مُشغَّلة **مرتين** على قاعدة الاختبار بلا خطأ، ومع كل سكربت تراجع. لا أسرار في السكربتات (بحث نصي). |
| الأوقات | كيانات v2 بـUTC؛ `Notification.SentAt` صريح بـUTC (R6، مختبر). العرض بتوقيت السعودية عبر `ITimeZoneService`. |
| Overposting | نماذج POST صغيرة (`Id` فقط، `PublicationId+Enabled`)؛ لا ربط مباشر لكيان. |

## ما لم يُتحقق منه

- رؤوس الأمان على الحافة (F1).
- سلوك ملء الشاشة على iPhone Safari وAndroid Chrome الحقيقيين مع YouTube/Vimeo (يدوي إلزامي؛ المختبر Chromium بحجب `requestFullscreen`).
- عدم تكرار العدّادات عبر نسخ متعددة فعلية (F2).
- اختبار الحمل (`RTK_LOAD=1`) لم يُشغَّل.
