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

---

# RTK-S13 — إضافة مراجعة «ملحق المحور» (D29–D34)

> التاريخ: 2026-10-07 · النطاق: كيانا `RemedialTrackAddendum`/`RemedialTrackAddendumProgress`، الخدمتان `RemedialTrackAddendumService` (أدمن) و`RemedialTrackAddendumStudentService` (طالب)، الـ Actions الأربعة الجديدة، تعديل حارس `OnExamSubmittedAsync`، تقارير الطالب/الأدمن/ولي الأمر.
> الطريقة: قراءة الكود + بحث نصي + اختبارات آلية فعلية (وحدات InMemory، SQL Server حقيقي على قاعدة الاختبار المعزولة، Playwright 375px).

## الخلاصة

- **لا Critical ولا High** مفتوح ضمن نطاق S13. (الخطر **R8** — محاولة اختبار رقمها غير 101 تُعامل كأنها 102 — أُغلق: حارس صريح + `switch` + اختبارات.)
- **Medium:** لا جديد (F1/F2 أعلاه تنطبق كما هي على سياستي `rtk-ping`/`rtk-exam` اللتين يرثهما الملحق).
- ملاحظات Low أدناه.

## النتائج

| # | الخطورة | النتيجة | الدليل | المعالجة |
|---|---|---|---|---|
| S13-R8 | كان High (مُغلق) | `OnExamSubmittedAsync` كان يعامل أي `ExamNumber` غير 101 كأنه 102 ← محاولة ملحق كانت ستجتاز/ترسب المحور وتفتح التالي. | `RemedialTrackProgressService.OnExamSubmittedAsync` | حارس فوري `ExamNumber == Addendum ← لا انتقال` + `switch` صريح لا يفترض 102 + `Exam102Percent` لا يُكتب إلا لـ102. اختبارات: `SubmittingAddendumAttempt_NeverChangesAnyAxisOrEnrollmentState` (4 حالات)، `ConcurrentDoubleSubmit_CountsOnce_AndAxisStateIsUntouched` (SQL حقيقي). |
| S13-1 | Low | الفهرس الفريد `IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber` صار **مُصفّى** (`AddendumId IS NULL`) ليسمح بمحاولات الملحق غير المحدودة؛ هذا استبدال فهرس موجود (لا إضافة فقط). | `RTK_Addendum.sql` | قاعدة «محاولة واحدة لكل اختبار 101/102» محفوظة بالضبط (اختبار SQL: `AxisExamIndex_StillEnforcesOneAttemptPerExam_AndIgnoresAddendumAttempts`)، وفهرس مُصفّى ثانٍ يضمن محاولة ملحق جارية واحدة. |
| S13-2 | Low | تراجع السكربت يحذف محاولات الملحق وأسئلتها (وإلا فشل إعادة الفهرس القديم). | `RTK_Addendum_Rollback.sql` | موثَّق في رأس السكربت؛ لا أثر على درجات المحاور. |
| S13-3 | Low | الإشعار برابط عام `/Students/RemedialTrack` (دالة الإشعار تقبل رابطًا واحدًا لكل الطلاب)، لا رابط مباشر للملحق. | `RemedialTrackAddendumService.NotifyAsync` | مقبول؛ البطاقة تظهر في صفحة الخطة. لا يُشعَر طالب محوره `Locked` (يظهر له الملحق عند فتح المحور، D31). |
| S13-4 | Low | محاولة ملحق جارية تُكمَل حتى لو أُوقف الملحق أثناءها (الإيقاف يخفيه عن البدء/المشاهدة/البطاقات/التقارير، لا يقتل محاولة بدأت). | `RemedialTrackExamService` | سلوك مقصود (لا خسارة إجابات)؛ وأمر محذوف/ملغى يمنع الجميع (D34). |
| N-S13-1 | ملاحظة | **IDOR:** كل مسار طالب يُحمَّل بـ`EnrollmentId` يخص `studentId` من الهوية؛ غير المستهدف/تسجيل غيره/أمر آخر/محور `Locked`/ملحق غير نشط/أمر محذوف أو ملغى ← 404 (أو `NotFound` في الخدمة). الرابط/المعرّف لا يُسلَّم إلا قبل اكتمال الفيديو ولمن استُهدف. | `Get_Owner_SeesLink_NonTargetedAndOtherPublicationGet404`، `Exam_OtherStudentOrOtherOrder_CannotStartOrReadAttempt`، `Ping_OtherStudentOrWrongEnrollment_NotFound...`، Playwright (IDOR على الصفحة ومحاولة الاختبار) | مغطّى. |
| N-S13-2 | ملاحظة | **Antiforgery + صلاحيات:** `CreateAddendum`/`SetAddendumActive` = `[HttpPost]` + `[ValidateAntiForgeryToken]` + `[AdminPermission(..."ManageAddendum")]`؛ `AddendumPing`/`StartAddendumExam` = POST + Antiforgery + `rtk-ping`/`rtk-exam`؛ المفتاح الجديد في الأماكن الثلاثة (ثابت السياسة + الكتالوج + `Program.cs`). النبضة بلا رمز ← 400 (Playwright). موظف بلا `ManageAddendum` مرفوض ومعه مسموح (معالج التفويض). | `RemedialTrackAddendumTests` (الأمان) | مغطّى. |
| N-S13-3 | ملاحظة | **نطاق الدفعات:** الإنشاء/الإيقاف/اللوحة/طلاب الملحق كلها تفرض `RemedialTrackBatchScope` داخل الخدمة؛ خارج النطاق ≡ غير موجود. معرّف تسجيل غريب أو ملغى ← رفض الطلب كله بلا أي إدراج. | `Create_ForeignOrCancelledEnrollmentId_RejectsWholeRequest`، `Create_DeletedOrCancelledPublication_AndOutOfScope_AreRejected`، `Students_Drilldown_IsScopedAndPaged` | مغطّى. |
| N-S13-4 | ملاحظة | **Overposting:** نموذج POST مخصّص (`CreateRemedialTrackAddendumInput`) بلا `IsActive`/`CreatedBy*`/`Provider`/`ExternalId`/أي حقل حالة؛ الرابط يُحلَّل عبر `RemedialTrackVideoUrlParser` (https فقط، لا `javascript:`) ويُبنى الـembed من `ExternalId` لا من النص المُدخل. | `CreateInput_HasNoOverpostableFields_AndValidatesReason`، `Create_BadUrl_Rejected` | مغطّى. |
| N-S13-5 | ملاحظة | **XSS:** لا `Html.Raw` في الـ Views الجديدة (grep فارغ)؛ نصوص العنوان/السبب/اسم الطالب تُرمَّز، وجدول الطلاب في JS يمرّ على `esc()`. | `Addendum.cshtml`، `Details.cshtml` | مغطّى. |
| N-S13-6 | ملاحظة | **سلامة المشاهدة (D32):** الرصيد بزمن الخادم وسقف 20ث للنبضة، العميل لا يضخّم الزمن، التحديث ذرّي بشرط `LastPingAtUtc` (8 نبضات متزامنة ← رصيد واحد على SQL حقيقي). | `Ping_ClientCannotInflateWatchedTime`، `ConcurrentPings_DoNotDoubleCreditServerTime` | مغطّى. |
| N-S13-7 | ملاحظة | **عدم تسريب الإجابة (D15):** صفحة حل الملحق بلا `CorrectAnswer`/`Explanation`/`VideoUrl`؛ ولا يظهر أي منها في HTML في Playwright. | `Solve_AddendumAttempt_NeverLeaksCorrectAnswerOrExplanation` + Playwright | مغطّى. |
| N-S13-8 | ملاحظة | **عدم المساس بالمحور (D29):** لقطة `AxisProgress`/`Enrollment`/الأحداث/تقارير ولي الأمر قبل/بعد أي عمل على الملحق متطابقة؛ والمحور التالي يُفتح قبل الملحق وبعده. | `Create_AllEnrollments_..._NeverTouchesAxes`، `PassingAxis_WithPendingAddendum_StillOpensNextAxis`، Playwright | مغطّى. |

## مراجعة الإنتاج على ما تغيّر

| البند | النتيجة |
|---|---|
| N+1 / حلقات | إنشاء الملحق: استعلام واحد للتسجيلات + `AddRange` + `SaveChanges` واحد (52 تسجيلًا ← صف إدراج لكل طالب دفعة واحدة). لا استعلام داخل حلقة في أي مسار جديد. تقرير الطالب/ولي الأمر +1 استعلام ثابت (الملاحق) فيصير 5 استعلامات ثابتة بغض النظر عن عدد المحاور. |
| التوافق مع SQL Server | لا `Contains`/`IN` على قوائم في الخدمات الجديدة (تقاطع المعرّفات في الذاكرة)؛ لا `OPENJSON`؛ نُفِّذت كل استعلامات القراءة فعليًا على `UseCompatibilityLevel(120)`. |
| الفهارس | `UX_RemedialTrackAddendumProgresses_Addendum_Enrollment` (فريد)، `IX_..._Enrollment_CompletedAt`، `IX_RemedialTrackAddenda_Publication_IsActive`، `UX_RemedialTrackExamAttempts_Addendum_OpenAttempt` (مُصفّى) — مختبرة على SQL حقيقي. |
| الترحيلات | `RTK_Addendum.sql` مُشغَّل **مرتين** بلا خطأ، ثم `RTK_Addendum_Rollback.sql` مرتين، ثم إعادة التطبيق، على قاعدة الاختبار المعزولة (بيانات المحاور سليمة). |
| Pagination / AsNoTracking | لوحة الملاحق 10/صفحة، طلاب الملحق 20/صفحة، كل القراءات `AsNoTracking`. |
| التوسّع | نفس تحفظ F2 (عدّادات الـ Rate Limit محلية لكل نسخة). |

## ما لم يُتحقق منه

- تشغيل فيديو حقيقي (YouTube/Vimeo) على iPhone/Android في صفحة الملحق — يدوي إلزامي ضمن بوابة النشر (§7 بند 7).
- بروفة السكربتات بالترتيب الكامل `RTK_TermsAccepted ← … ← RTK_Addendum` على **نسخة من الإنتاج** (Staging) والنسخة الاحتياطية الفعلية للإنتاج: قرار تشغيلي خارج هذه الجلسة.
