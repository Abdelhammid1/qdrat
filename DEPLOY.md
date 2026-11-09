# خطوات النشر على qdrat-server — إلزامية بعد أي publish جديد

## ⚠️ تحذير حرج: مجلد uploads
`wwwroot/uploads` على السيرفر هو **symlink** بيشاور على
`/opt/qdrat/persistent-uploads` (مش مجلد حقيقي).

قبل نسخ أي build جديد فوق `/opt/qdrat/app/wwwroot`:

```bash
# تأكد إن الـ symlink لسه موجود بعد النسخ
ls -la /opt/qdrat/app/wwwroot/uploads
# المفروض يطلع: uploads -> /opt/qdrat/persistent-uploads

# لو اتمسح (اتحول لمجلد عادي فاضي بالغلط)، رجّعه فورًا:
rm -rf /opt/qdrat/app/wwwroot/uploads
ln -s /opt/qdrat/persistent-uploads /opt/qdrat/app/wwwroot/uploads
chown -h root:root /opt/qdrat/app/wwwroot/uploads
```

**السبب:** ديبلوي بتاريخ 2026-09-22 مسح مجلد uploads الحقيقي كامل
(188M صور courses/slides/subcourses/questions) لأن الملفات كانت
عايشة جوه wwwroot العادي. اتعمل استرجاع من backup + تحويلها لـ
symlink خارج مسار النشر. لازم الفحص ده يتعمل بعد كل نشر جديد
لحد ما يتعمل سكريبت ديبلوي أوتوماتيك يتحقق من الخطوة دي لوحده.

## ⚠️ تحديث 2026-09-22: مجلد images/profiles اكتُشف بنفس المشكلة

بعد deploy تعديلات ThreadPool، اتلاحظ إن صور البروفايل الشخصية
(`wwwroot/images/profiles/`) كانت بتتمسح بنفس طريقة `uploads` —
بس محدش وثّقها وقتها لأنها مش لاقية اهتمام زي صور الكورسات.

**تم:**
- استرجاع 24 صورة بروفايل من `/opt/qdrat/app-old-20260921_212400/`
- تحويل `wwwroot/images/profiles` لـ symlink على `/opt/qdrat/persistent-images/profiles`
- تعميم `ensure-uploads-symlink.sh` ليشتغل بمصفوفة `PAIRS` بدل مسار واحد ثابت،
  عشان أي فولدر جديد مشابه يتضاف بسطر واحد فقط

**الدرس:** أي فولدر فيه ملفات يرفعها المستخدمون (صور، مستندات) تحت
`wwwroot/` لازم يتفحص ويتحول لـ symlink، مش بس `uploads/`. الفحص
الشامل لكل `wwwroot/` لسه لم يتم — ينصح بمراجعة دورية.

**تحديث:** تم إجراء الفحص الشامل لكل wwwroot/ بمقارنة عدد الملفات مع نسخة
backup قبل الـ deploy — لم يظهر أي فولدر آخر متأثر غير uploads/ و
images/profiles/. الفروقات في js/css/lib هي مكتبات ثابتة (jQuery,
validation) وليست ملفات مستخدمين، ولا تحتاج حماية.


## 📋 نشر إصلاح تكرار الإجابات والأسئلة في معمل القياس (MSE)

**1) قبل نشر الكود:**
```sql
BACKUP DATABASE [QdratNewDB] TO DISK = N'/var/opt/mssql/backup/QdratNewDB_before_MSE_UniqueAnswer.bak' WITH INIT, COMPRESSION;
```
شغّل `Migrations/Scripts/MSE_Diagnose_DuplicateAnswers.sql` واحفظ ناتجه: هو سجل الطلاب المتأثرين، وبعد الخطوة التالية يختفي التكرار. ثم شغّل `Migrations/Scripts/MSE_UniqueStudentAnswer.sql` (Idempotent).

- يبقي أحدث صف لكل سؤال، وينسخ الأقدم إلى `MinistrySimExamStudentAnswers_DuplicatesBackup`، ثم ينشئ الفهرس الفريد.
- **لا يعيد حساب الدرجات:** `StagePercentScore` و`TotalScorePercent` المحفوظة تبقى كما هي، وقد تكون مضخّمة لمن ظهر في الفحص. إعادة الحساب قرار منفصل مؤجل.

**2) التحقق:**
```sql
SELECT COUNT(*) FROM sys.indexes WHERE name = N'IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId_MinistrySimExamStageQuestionId' AND is_unique = 1; -- 1
SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = N'20261105143557_MSE_UniqueStudentAnswer'; -- 1
```

**3) بعد نشر الكود:**
- الطالب: اضغط «التالي» مرتين بسرعة ثم غيّر الإجابة، وتأكد أن صفحة الحل تعمل وأن آخر إجابة هي الظاهرة.
- الأدمن: الاختبار **المنشور** لا يتغير. المسودة التي فيها سؤال مكرر أو مشابه تُمنع من النشر وتظهر أسبابها في Details، وتُحل بالاستبدال أو إعادة التوليد.
- قد تظهر رسالة نقص جديدة عند بناء قسم: «بعد استبعاد ... سؤال مكرر أو مشابه». معناها أن البنك لا يكفي بلا تكرار.

**4) مفتاح الضبط:** `MinistrySimExam.QuestionSimilarityPercent` في `SystemSettings` (الافتراضي 70؛ 0 يوقف فحص التشابه ويبقي منع السؤال نفسه).

**5) التراجع:** `Migrations/Scripts/MSE_UniqueStudentAnswer_Rollback.sql` يعيد الفهرس القديم ولا يعيد الصفوف المحذوفة (نسختها في الجدول الاحتياطي). الكود الجديد يعمل بعد التراجع.

## 📋 نشر Epic RL (سجل معنا + لوحة طلبات الالتحاق) — RL-S6

### 1) قبل النشر (Backup إلزامي)
```sql
BACKUP DATABASE [QdratNewDB] TO DISK = N'/var/opt/mssql/backup/QdratNewDB_before_RL.bak' WITH INIT, COMPRESSION;
```
لا تُطبَّق الـ Migration آليًا. راجع ثم شغّل يدويًا `.PROMPT/RL_Migration.sql` على قاعدة الإنتاج.

### 2) سكريبت `RL_Migration.sql` — نتيجة المراجعة
- إضافات فقط (أعمدة + جدول `FrontendLeadCourses` + 4 فهارس)، بلا حذف بيانات. الوحيد الذي يُسقَط فهرس `IX_Courses_ProjectId` ويُستبدل بفهرس مركّب أوسع.
- Idempotent فعليًا: كل خطوة داخل `IF NOT EXISTS (… __EFMigrationsHistory …)` وفي Transaction واحدة. جُرِّب تشغيله ثانيةً على قاعدة الاختبار (التي فيها الـ Migration مطبّقة) فانتهى بلا أخطاء وبلا تغيير.
- ترحيل البيانات: `Status = IsContacted ? 2 : 1` للصفوف التي `Status = 0`. الطلبات القديمة تبقى ظاهرة بشارة «طلب قديم».
- متوافق مع SQL Server 2014: لا `OPENJSON` ولا `Contains` على قوائم داخل استعلامات EF في خدمتي RL.

### 3) بعد النشر
- تأكد من `symlink` مجلد `uploads` و`images/profiles` (القسمان أعلاه).
- ⚠ **قرار معلّق: `UseForwardedHeaders`.** Program.cs لا يستدعيه. خلف Nginx/Reverse Proxy يرى `RemoteIpAddress` عنوان الـ Proxy، فيتشارك كل الزوار حد `public-forms` (5 طلبات/10 دقائق) ويُحجَب التسجيل بعد 5 طلبات إجمالًا. فعّله (مع `KnownProxies`) قبل فتح `/register` للعامة، ثم أعد فحص الحد.
- بعد أي تعديل مباشر في SQL على `Projects/Courses` أعد تشغيل التطبيق أو انتظر 10 دقائق (كاش الكتالوج)؛ التعديل من لوحة الأدمن يُبطل الكاش تلقائيًا.

### 4) Checklist اختبار يدوي (لم يُجرَّب في المتصفح آليًا)
- [ ] `/register` على 375px: RTL سليم، لا تمرير أفقي، الكروت والـ Tiles مقروءة، الشريط اللاصق لا يغطي زر الإرسال.
- [ ] `/register` على الديسكتوب.
- [ ] `/register` بدون JavaScript: الاختيار والإرسال يعملان، ويصل لـ `/register/success`.
- [ ] حالة الكتالوج الفارغ (إخفاء كل البرامج): تظهر رسالة فارغة ويصبح حقل الملاحظات مطلوبًا.
- [ ] كتالوج كبير (~30 دورة): سرعة التمرير، وحد 10 دورات يظهر كرسالة واضحة.
- [ ] إرسال بأرقام عربية (٠٥…) ثم رقم مكرر خلال 10 دقائق (لا تكرار).
- [ ] الطلب السادس خلال 10 دقائق يرجع نص 429 (مع مراعاة بند UseForwardedHeaders).
- [ ] `/Admin/FrontendLeads`: المجهول يُحوَّل لتسجيل الدخول؛ الأدمن يرى الكروت وتُفلتر الجدول؛ Chips البرامج؛ فلتر الفترة.
- [ ] تغيير الحالة + ملاحظة من الـ Offcanvas: تتحدث الكروت والشارة في القائمة فورًا.
- [ ] تصدير Excel يحتوي الدورات مجمّعة حسب البرنامج؛ وطلب قديم يظهر بشارة «طلب قديم».
- [ ] مفاتيح «في التسجيل» في Projects/Courses Index (Toggle) وانعكاسها على `/register`.

### 5) الاختبارات
`dotnet test QdratNew.Tests` — 58 اختبارًا (تشمل RL: كتالوج، SubmitLead، /register GET/POST/429، حماية الأدمن، UpdateStatus). اختبارات التكامل تحتاج SQL Server محليًا وقاعدة `QdratNewDB` (تُنسخ إلى `QdratNewDB_IntegrationTests`)، وتعمل في Collection واحدة متسلسلة لأنها تشترك في نفس القاعدة.


## 📋 نشر Epic RTK (الخطة العلاجية العاجلة) — RTK-S7.4

### 1) قبل النشر (Backup إلزامي)
```sql
BACKUP DATABASE [QdratNewDB] TO DISK = N'/var/opt/mssql/backup/QdratNewDB_before_RTK.bak' WITH INIT, COMPRESSION;
```
لا تُطبَّق الـ Migration آليًا على الإنتاج. شغّل يدويًا بالترتيب (كلاهما Idempotent وإضافات فقط، يمكن إعادة تشغيلهما بأمان):
1. `Migrations/Scripts/RTK_RemedialTracks.sql` — 10 جداول `RemedialTrack*` + فهارسها (منها الفهرس الفريد المُصفّى `UX_RemedialTrackPublications_ActiveCode`). كل خطوة داخل `IF NOT EXISTS (… __EFMigrationsHistory …)`.
2. `Migrations/Scripts/RTK_FeatureFlag.sql` — صف واحد في `SystemSettings` للمفتاح `RemedialTrack.Enabled` (= `true`). التطبيق يزرعه أيضًا عند الإقلاع (`SystemSettingsSeeder`)، والسكربت للتحكم قبل نشر الكود.

### 2) التحقق بعد تشغيل السكربت
```sql
SELECT COUNT(*) FROM sys.tables WHERE name LIKE N'RemedialTrack%';   -- المتوقع 10
SELECT name, filter_definition FROM sys.indexes
 WHERE name = N'UX_RemedialTrackPublications_ActiveCode';           -- المتوقع: ([AccessCode] IS NOT NULL AND [Status]=(1))
SELECT [Value] FROM SystemSettings WHERE [Key] = N'RemedialTrack.Enabled';   -- المتوقع true
```

### 3) بعد نشر الكود
- تأكد من `symlink` مجلدي `uploads` و`images/profiles` (القسمان الأوّلان في هذا الملف).
- ⚠ **فحص CSP / الـ iframe (يدوي — لا يوجد `Content-Security-Policy` ولا `X-Frame-Options` في كود التطبيق):** تحقق في إعدادات Nginx (qdrat-server) أو Azure أن أي `Content-Security-Policy` مُضاف هناك يسمح بـ `script-src https://www.youtube.com https://www.youtube-nocookie.com https://s.ytimg.com https://player.vimeo.com` و`frame-src https://www.youtube.com https://www.youtube-nocookie.com https://player.vimeo.com`. لا تضف CSP صارمة جديدة تكسر بقية المنصة.
- جرّب نشرًا تجريبيًا: خطة صغيرة (محور + فيديو + نموذجا 101/102) ← أمر نشر على دفعة تجريبية بطالب اختبار (أونلاين ثم حضوري) ← ادخل بحساب الطالب وتأكد من البوابة والمشغّل والاختبار.
- `UseForwardedHeaders` مفعّل في Program.cs؛ تأكد من أن عنوان الـ Proxy ضمن الإعدادات كي يُحسب Rate Limit بالمستخدم (سياسات `rtk-code` 10/10د، `rtk-ping` 20/د، `rtk-exam` 120/د مفتاحها المستخدم لا الـ IP).

### 4) مفتاح التعطيل وخطة التراجع (بلا حذف جداول)
```sql
UPDATE SystemSettings SET [Value] = N'false' WHERE [Key] = N'RemedialTrack.Enabled';   -- تعطيل
UPDATE SystemSettings SET [Value] = N'true'  WHERE [Key] = N'RemedialTrack.Enabled';   -- تفعيل
```
- عند `false`: يختفي بند «الخطة العلاجية» من قائمة الطالب، وتُرجع كل أكشنات الطالب 404 (صفحة صيانة، أو JSON `{ok:false, reason:"disabled"}` لنداءات fetch)، دون المساس بالبيانات. لوحات الأدمن والتقارير تبقى متاحة.
- يسري التغيير خلال **30 ثانية** (كاش المفتاح) بلا إعادة تشغيل. الغياب أو قيمة غير صالحة = مفعّل؛ فقط `false` الصريحة تعطّل.
- إيقاف أمر نشر نشط بعينه: «إلغاء أمر النشر» من لوحة الأدمن (يُحرّر الرقم المرجعي ويحجب الوصول).

### 5) الاختبارات
- بلا قاعدة بيانات: `dotnet test QdratNew.Tests --filter "FullyQualifiedName~RemedialTrack"` (وحدات + حارس أمني + خدمات InMemory).
- SQL Server حقيقي (S7.2/S7.3): اضبط `QDRAT_TEST_SQL` على قاعدة اسمها يحوي `Test` (مثل `QdratNewDB_IntegrationTests`) — الفهارس الفريدة، RowVersion، ExecuteUpdate تحت CompatibilityLevel(120)، تكامل HTTP، وE2E Playwright (375px).
- الحمل (اختياري، مكلف): `RTK_LOAD=1` مع `QDRAT_TEST_SQL` — 500 طالب × نبضة/15ث × 5 دقائق؛ يكتب `rtk-load-report.txt` بجوار ملفات الاختبار. لا يُشغَّل أبدًا على قاعدة الإنتاج.

---

## 📋 نشر Epic APD (جمهور الإرسال: المنصة أو المدارس الشريكة) — APD-S1 إلى S6
> شاشات الإرسال الخمس في الأدمن تعرض «جمهور الإرسال»: دفعات المنصة (السلوك السابق على دفعات المعهد فقط) أو عدة مدارس شريكة. ما يُرسل لمدرسة يصبح ملكها، يُتابَع من منطقتها، ويختفي من قوائم الأدمن وتقاريره. التفاصيل لكل Sprint في البنود 1–9 أدناه.

### 0) ملخص نشر الـ Epic كاملًا (إن نُشر دفعة واحدة)
1. **Backup** لقاعدة الإنتاج (البند 1).
2. السكربتات بالترتيب، كلها Idempotent وإضافات فقط:
   `APD_PartnerDispatch.sql` ← `APD_PerformanceIndicatorTemplate.sql` ← `MSE_UniqueStudentAnswer.sql` ← `APD_MinistrySimPartnerHidden.sql`.
   تحقق بعد كل سكربت بالاستعلامات في بنده. **الكود لا يُنشر قبل السكربتات** لأنه يقرأ الأعمدة الجديدة.
3. نشر الكود، ثم التحقق من الـ symlinks في أعلى هذا الملف.
4. بعد النشر: فعّل «معمل القياس» ومؤشر الأداء في عقود المدارس المعنية، وراجع وحدة «الإرسال للمدارس الشريكة» في البروفايلات (`Send` ممنوحة تلقائيًا لمن يملك `ManageModelHomework`، و`Override` و`ReadLog` يدويًا).
5. **Smoke (مدرسة تجريبية واحدة):** أرسل من كل شاشة من الخمس، وتأكد من ظهور الإرسال في منطقة المدرسة وغيابه عن الأدمن، ومن ظهوره في `Admin/PartnerDispatchLog`. ثم اختبار يدوي وفق `Docs/UAT/APD_UAT.md`.
6. **المراقبة أول 24 ساعة:** أخطاء `PartnerDispatch`/`PartnerMinistrySim`/`PartnerPerformance` في سجلات Serilog، وزمن طلبات الإرسال لعدد كبير من المدارس.
7. **الإيقاف الفوري** بلا إعادة تشغيل: `PartnerDispatch.Enabled = false` (البند 4). **التراجع الكامل** بعد سحب الكود فقط وبعكس ترتيب السكربتات: `APD_MinistrySimPartnerHidden_Rollback.sql` ← `APD_PerformanceIndicatorTemplate_Rollback.sql` ← `APD_PartnerDispatch_Rollback.sql` (`MSE_UniqueStudentAnswer.sql` إصلاح مستقل لا يلزم التراجع عنه؛ والأخير يحذف أعمدة الملكية وسجل الإرسال، فما أُرسل للمدارس يعود ظاهرًا للأدمن كأنه للمعهد).

### 1) قبل نشر الكود (Backup إلزامي)
```sql
BACKUP DATABASE [QdratNewDB] TO DISK = N'/var/opt/mssql/backup/QdratNewDB_before_APD_S1.bak' WITH INIT, COMPRESSION;
```
شغّل يدويًا `Migrations/Scripts/APD_PartnerDispatch.sql` (Idempotent وإضافات فقط). الكود الجديد يقرأ الأعمدة (`HomeworkSets.OwnerPartnerId` في كل إرسال واجب)، فنشره قبل السكربت يكسر الواجبات.

### 2) التحقق بعد السكربت
```sql
SELECT COL_LENGTH(N'HomeworkSets', N'OwnerPartnerId');                          -- 4
SELECT COL_LENGTH(N'PerformanceIndicatorExams', N'OwnerPartnerId');             -- 4
SELECT COL_LENGTH(N'MinistrySimExamAssignmentsToBatches', N'OwnerPartnerId');   -- 4
SELECT COL_LENGTH(N'MinistrySimExamAssignmentsToStudents', N'OwnerPartnerId');  -- 4
SELECT COL_LENGTH(N'PartnerSubscriptions', N'CanUseMinistrySimExams');          -- 1
SELECT OBJECT_ID(N'PartnerDispatchLogs');                                       -- ليس NULL
SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = N'20261105004750_APD_PartnerDispatch'; -- 1
-- لا Backfill: يجب أن يكون 0
SELECT COUNT(*) FROM HomeworkSets WHERE OwnerPartnerId IS NOT NULL;
-- البروفايلات التي مُنحت PartnerDispatch.Send تلقائيًا (من يملك ManageModelHomework)
SELECT p.Name FROM AdminProfiles p JOIN AdminProfileControllerPermissions c ON c.AdminProfileId = p.Id WHERE c.ControllerName = N'PartnerDispatch';
```
اختياري (قراءة فقط): `Migrations/Scripts/APD_Diagnose_PartnerHomework.sql` يعدّ واجبات أرسلها الشركاء قبل APD وتبقى ظاهرة للأدمن.

### 3) بعد نشر الكود
- واجبات الشريك الجديدة (من مسوداته أو من نموذج) تحمل `OwnerPartnerId`، وتختفي من شاشات الأدمن بعد نشر APD-S2.
- فعّل «معمل القياس» في عقود الشركاء المعنيين من `Admin/PartnerSubscriptions/Edit` (الافتراضي غير مفعّل).
- راجع في مصفوفة البروفايل وحدة «الإرسال للمدارس الشريكة»: `Send` ممنوحة تلقائيًا كما في الاستعلام أعلاه، و`Override` و`ReadLog` لا تُمنح إلا يدويًا.

### 4) مفتاح التعطيل وخطة التراجع
- **إيقاف فوري:** `UPDATE SystemSettings SET Value = 'false' WHERE [Key] = N'PartnerDispatch.Enabled';` يخفي خيار «المدارس الشريكة» ويغلق `Admin/PartnerDispatch/AudienceOptions` (404) بلا إعادة تشغيل. الإرسال للمنصة لا يتأثر.
- **تراجع كامل** (بعد سحب الكود فقط): `Migrations/Scripts/APD_PartnerDispatch_Rollback.sql` — يحذف أعمدة الملكية وسجل الإرسال وصلاحيات الوحدة.

### 5) APD-S2 (الواجب والاختبار من نموذج)
- **لا Migration ولا سكربت جديد:** يكفي أن يكون سكربت APD-S1 مطبقًا.
- **تغيّر مرئي للأدمن:** شاشتا `CreateModelHomework` و`CreateModelExam` تعرضان «جمهور الإرسال»، ودفعات الشركاء لم تعد تُختار تحت «دفعات المنصة».
- **يختفي من الأدمن:** كل واجب أو اختبار `OwnerPartnerId` فيه قيمة، بما فيه ما أرسله الشريك من منطقته بعد نشر APD-S1. ما أُرسل قبل APD بلا مالك ويبقى ظاهرًا.
- **تحقق بعد النشر:** أرسل واجبًا لمدرسة تجريبية، ثم تأكد من صفحة النتيجة، ومن ظهوره في `Partner/SentHomeworks` بوسم «مرسل من المعهد»، ومن غيابه عن `Admin/AdminLessonCompletions/ModelAssignments`، ومن صف واحد لكل دفعة في `PartnerDispatchLogs`.
- **التراجع:** مفتاح `PartnerDispatch.Enabled = false` يوقف الإرسال للشركاء فورًا، وما أُرسل يبقى ملك الشريك.

### 6) APD-S3 (تحديد المستوى من نموذج)
- **لا Migration ولا سكربت جديد:** يكفي أن يكون سكربت APD-S1 مطبقًا.
- **تغيّر مرئي للأدمن:** `Admin/PlacementExams/CreateFromProfessionalModel` تعرض «جمهور الإرسال» بدل قائمة الدفعات. تحت «دفعات المنصة» دفعة واحدة من دفعات المعهد، وتحت «المدارس الشريكة» عدة مدارس مع «إرسال فوري» أو «مسودة لدى المدرسة».
- **تغيّر سلوك:** الإنشاء بنطاق الأدمن (من نموذج و`CreateAdvanced`) يرفض دفعة شريك. قراءة الاختبارات القديمة على دفعات شركاء لم تتغير.
- **الجديد عند الشريك:** اختبار المعهد قد يرتبط بعدة دفعات، والإخفاء والحذف يسريان عليها كلها (نص التأكيد يذكر ذلك).
- **تحقق بعد النشر:** أرسل مسودة لمدرسة تجريبية، ثم تأكد من صفحة النتيجة، ومن ظهورها في `Partner/PlacementExams/BatchExams` بوسم «من المعهد»، ومن غيابها عن `Admin/PlacementExams`، ومن عدم وصولها لأي طالب قبل إرسال المدرسة.
- **التراجع:** مفتاح `PartnerDispatch.Enabled = false` يوقف الإرسال للشركاء فورًا، وما أُرسل يبقى ملك الشريك.

### 7) APD-S4 (مؤشر الأداء)
- **قبل نشر الكود، بعد Backup:** شغّل `Migrations/Scripts/APD_PerformanceIndicatorTemplate.sql` (Idempotent، يضيف العمود فقط، ويتطلب سكربت APD-S1). الكود الجديد يقرأ هذا العمود، فلا يُنشر قبله.
```sql
SELECT COL_LENGTH(N'PerformanceIndicatorExams', N'IsPartnerTemplate');  -- 1
SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = N'20261105131916_APD_PerformanceIndicatorTemplate'; -- 1
```
- **بعد النشر:** رابط «مؤشر الأداء» في سايدبار الشريك يعمل لأول مرة (كان يشير إلى Controller غير موجود)، ويظهر للعقد الذي يشمل `CanUsePerformanceIndicatorExams`.
- **يختفي من الأدمن:** كل نسخة `OwnerPartnerId` فيها قيمة، من القوائم والتقارير ولوحة الأداء وتفاصيل المستخدم. القوالب تظهر في الأرشيف بوسم «قالب إرسال للشركاء».
- **تغيّر تحت «المنصة»:** دفعات المعهد وطلابه فقط في `Create` و`CreateFromProfessionalModel`، والدفعات المؤرشفة والمحذوفة لم تعد ضمن الاختيار.
- **تراجع** (بعد سحب الكود فقط): `Migrations/Scripts/APD_PerformanceIndicatorTemplate_Rollback.sql`. نسخ المدارس لا تتأثر، والقوالب تبقى اختبارات مؤرشفة عادية.

### 8) APD-S5 (معمل القياس)
- **الترتيب:** يتطلب سكربتات APD-S1 وAPD-S4 و`MSE_UniqueStudentAnswer.sql` مطبقة قبله.
- **قبل نشر الكود، بعد Backup:** شغّل `Migrations/Scripts/APD_MinistrySimPartnerHidden.sql` (Idempotent، يضيف عمودين يقبلان NULL فقط). الكود الجديد يقرأ العمودين، فلا يُنشر قبله.
```sql
SELECT COL_LENGTH(N'MinistrySimExamAssignmentsToBatches', N'HiddenByOwnerAt');   -- 8
SELECT COL_LENGTH(N'MinistrySimExamAssignmentsToStudents', N'HiddenByOwnerAt');  -- 8
SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = N'20261105214613_APD_MinistrySimPartnerHidden'; -- 1
```
- **بعد النشر:** رابط «معمل القياس» في سايدبار الشريك للعقد الذي يشمل `CanUseMinistrySimExams` (من شاشة اشتراك الشريك في الأدمن).
- **تغيّر في الأدمن:** شاشتا الإسناد (`AssignToBatch` و`AssignToStudent`) وشاشة `MinistrySimExamAssignments` تعرض دفعات المعهد فقط تحت «المنصة»، وإسناد دفعة شريك أو طالبها باسم المعهد يُرفض. نتائج طلاب المدارس وتحليلات دفعاتها وتقارير أولياء أمورها لا تظهر للأدمن (404)، وتفاصيل الاختبار تعرض عدد المدارس فقط.
- **ما أُسند قبل APD-S5** بلا مالك ويبقى ظاهرًا للأدمن كما هو.
- **تراجع** (بعد سحب الكود فقط): `Migrations/Scripts/APD_MinistrySimPartnerHidden_Rollback.sql`. ما أخفته المدارس يعود ظاهرًا لها، ولا أثر على الطلاب.

### 9) APD-S6 (سجل الإرسال والاعتماد)
- **لا Migration ولا سكربت جديد.** الصفحة تقرأ `PartnerDispatchLogs` من APD-S1.
- **الجديد في الأدمن:** `Admin/PartnerDispatchLog` (رابط «سجل الإرسال للمدارس» في السايدبار) بصلاحية `PartnerDispatch.ReadLog` فقط: فلاتر النوع والمدرسة والمرسل والتجاوز والتاريخ (بتوقيت السعودية)، صف لكل عملية إرسال وتفاصيل لكل مدرسة ودفعة، وتصدير Excel (حد 10,000 سطر؛ ضيّق الفلاتر لما بعده). لا روابط لنتائج الطلاب.
- **امنح `ReadLog` يدويًا** لمن يحتاجه من مصفوفة البروفايل (لا يُمنح تلقائيًا).
- **تحقق بعد النشر:** افتح السجل بحساب له `ReadLog` وتأكد من ظهور آخر إرسال تجريبي، ثم صدّر Excel. بحساب بلا `ReadLog` يجب أن يُرفض الرابط (403) ولا يظهر في السايدبار.

## 📋 نشر Epic PPM (إتاحة النماذج الاحترافية للشركاء والإرسال من نموذج) — PPM-S1 إلى S6

### 1) قبل نشر الكود (Backup إلزامي)
```sql
BACKUP DATABASE [QdratNewDB] TO DISK = N'/var/opt/mssql/backup/QdratNewDB_before_PPM.bak' WITH INIT, COMPRESSION;
```
شغّل يدويًا `Migrations/Scripts/PPM_PartnerModels.sql` (Idempotent وإضافات فقط). الكود الجديد يقرأ الأعمدة، فنشره قبل السكربت يكسر صفحات النماذج والواجبات والاختبارات. لا تعتمد على `Migrate()` عند الإقلاع.

### 2) التحقق بعد السكربت
```sql
SELECT COL_LENGTH(N'ProfessionalModels', N'IsForPartners');            -- المتوقع 1
SELECT COL_LENGTH(N'HomeworkSets', N'SourceProfessionalModelId');      -- المتوقع 4
SELECT COL_LENGTH(N'Exams', N'SourceProfessionalModelId');             -- المتوقع 4
SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = N'20261103000022_PPM_PartnerModels'; -- المتوقع 1
-- كل نموذج له جمهور اليوم يصبح موجّهًا للشركاء، وهذا العدد يجب أن يكون 0:
SELECT COUNT(*) FROM ProfessionalModels pm WHERE pm.IsForPartners = 0 AND (pm.IsGlobal = 1
    OR EXISTS (SELECT 1 FROM ProfessionalModelPartners p WHERE p.ProfessionalModelId = pm.Id)
    OR EXISTS (SELECT 1 FROM ProfessionalModelSubscriptionPeriods s WHERE s.ProfessionalModelId = pm.Id));
```

### 3) بعد نشر الكود
- ربط الفترة صار يُفحص بتاريخ اليوم، وكل نموذج يتطلب عقدًا نشطًا يحمل `CanUseProfessionalModels`. راجع نماذج الشركاء الحاليين في `Admin/ProfessionalModels` (الوسم بجوار كل نموذج).
- `HomeworkGeneration/FromProfessionalModel` يرفض الآن أي نموذج غير متاح للشريك.
- **صنّف النماذج بلا نوع قبل الاعتماد عليها:** `Admin/ProfessionalModels/Index?untyped=true`. لا تُرسل هذه النماذج من أي شاشة إرسال ولا تظهر للشريك.

### 4) ما يتغير عند الأدمن بعد PPM-S6
بلّغ مشرفي المعهد قبل النشر. `CreateModelHomework` و`CreateModelExam` صارا عبر `IProfessionalModelSendService`، بنفس الروابط والحقول:
- **يستلم الطلاب ذوو التسجيل النشط فقط** (`Status = Active`)، فلا يصل الإرسال للمنسحبين.
- **تحذير وتأكيد إلزامي** عند تكرار النموذج أو أسئلته على نفس الدفعة، ورفض إعادة الإرسال خلال 60 ثانية.
- **رفض** موعد النهاية قبل البداية، ومدة اختبار خارج 5–300 دقيقة، ودفعة غير صالحة ضمن الاختيار (كانت تُتجاهل)، وإرسال «لطلاب محددين» بلا طلاب (كان ينشئ اختبارًا فارغًا).
- **تخطي الدفعة بلا مستلمين** في الاختبار أيضًا، مع ذكر عددها في رسالة النجاح.
- **إسقاط السؤال المكرر داخل النموذج**، فقد يقل عدد أسئلة الطالب عن عدد صفوف النموذج.
- **إشعار واحد لكل مدرب** بدل إشعار لكل دفعة، ولكل مدربي الدفعة بدل أولهم.
- **موعد بطاقة الطالب** يظهر بتوقيت السعودية للإرسالات الجديدة. الإرسالات القديمة تبقى كما كانت.

### 5) التحقق بعد النشر (Smoke)
- أدمن: افتح `AdminLessonCompletions/CreateModelHomework` و`CreateModelExam`، وأرسل لدفعة تجريبية، وتأكد من ظهورها في `ModelAssignments`.
- شريك تجريبي: أرسل من `Partner/ProfessionalModels` واجبًا واختبارًا، وتأكد من وسم «من نموذج».
- طالب تجريبي: تأكد من ظهور الواجب والاختبار وموعدهما.
- ابحث في السجل عن `PPM` بعد الإرسال، ولا يجب أن يظهر `send failed`.

حزمة القبول الكاملة في `Docs/UAT/PPM_Partner_Models_UAT.md`.

### 6) خطة التراجع (بلا حذف بيانات)
- **إيقاف فوري لكل الشركاء:** عدّل قيمة `ProfessionalModels.PartnerAccess.Enabled` إلى `false` في `SystemSettings`. الأثر فوري بلا إعادة تشغيل، والأدمن لا يتأثر.
- **إيقاف نموذج واحد:** عطّل «موجّه للشركاء» من `Admin/ProfessionalModels/Edit`.
- **سحب الكود:** الرجوع لنسخة سابقة يكفي مع بقاء الأعمدة. لا تشغّل `PPM_PartnerModels_Rollback.sql` إلا بعد سحب الكود.
- **بعد سحب الكود:** ما أُرسل يبقى صالحًا عند الطلاب، وعمود `SourceProfessionalModelId` يبقى بلا أثر على النسخة السابقة.

## 📋 نشر Epic PPL (اختبار تحديد المستوى في منطقة الشركاء) — PPL-S7.4

⚠ **انشر خارج أوقات الاختبارات:** من فتح صفحة الحل قبل النشر لن يستطيع التسليم بالـ JavaScript القديم (الحفظ والتسليم أصبحا يتطلبان AntiForgery)، ويكفيه تحديث الصفحة.

### 1) قبل النشر (Backup إلزامي)
```sql
BACKUP DATABASE [QdratNewDB] TO DISK = N'/var/opt/mssql/backup/QdratNewDB_before_PPL.bak' WITH INIT, COMPRESSION;
```
لا تعتمد على `Migrate()` عند الإقلاع. شغّل يدويًا بالترتيب (كلاهما Idempotent وإضافات فقط):
1. `Migrations/Scripts/PPL_ExamOwnerPartner.sql` — عمود `Exams.OwnerPartnerId` (NULL) + المفتاح الأجنبي + الفهرسان.
2. `Migrations/Scripts/PPL_Placement_Indexes.sql` — فهارس `ExamStudentStatuses(ExamId)` و`ExamAssignmentsToBatches(ExamId)` و`(BatchId)`. الـ Model يفترض وجودها لكن لا توجد Migration أنشأتها، لذلك لن يضيفها `Migrate()`. شغّله في وقت هادئ تحسبًا لقفل قصير.

### 2) التحقق بعد تشغيل السكربتين
```sql
SELECT COL_LENGTH(N'Exams', N'OwnerPartnerId');                                -- المتوقع 4
SELECT COUNT(*) FROM Exams WHERE OwnerPartnerId IS NOT NULL;                   -- المتوقع 0 قبل أول استخدام
SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = N'20261102004719_PPL_ExamOwnerPartner'; -- المتوقع 1
SELECT name FROM sys.indexes
 WHERE name IN (N'IX_Exams_Type_OwnerPartnerId', N'IX_ExamStudentStatuses_ExamId',
                N'IX_ExamAssignmentsToBatches_ExamId', N'IX_ExamAssignmentsToBatches_BatchId');  -- المتوقع 4 صفوف
```

### 3) بعد نشر الكود
- تأكد من `symlink` مجلدي `uploads` و`images/profiles` (القسمان الأوّلان في هذا الملف). شعارات الشركاء في PDF تُقرأ من `wwwroot/uploads/partners`.
- **لا ميزة تعمل تلقائيًا لأي شريك:** تظهر فقط لمن يحمل عقده `CanUsePlacementExams`. فعّلها أولًا لشريك تجريبي واحد.
- نفّذ حزمة القبول: `Docs/UAT/PPL_Partner_Placement_UAT.md`.
- راقب في `Logs/log-*.txt` أي `PPL placement operation failed` و`PlacementExamsController:` خلال أول 24 ساعة، وأي 400 على `SubmitAnswerFetch` أو `SubmitFinal` في Nginx (تعني صفحة حل مفتوحة من قبل النشر).
- سياسة `ppl-report-pdf` (30/د لكل مستخدم) جديدة، و`VerifyPlacementExamCode` أصبح تحت `rtk-code` (10/10د لكل مستخدم).

### 4) خطة التراجع (بلا حذف بيانات)
- **إيقاف الميزة لشريك:** ألغِ `CanUsePlacementExams` من عقده في `Admin/PartnerSubscriptions`؛ يختفي الرابط من السايدبار وتُرجع الصفحات «غير مشمولة في عقدكم». اختبارات الشريك التي عند طلابه تبقى، فأخفِها أولًا إن كان يجب إيقافها فورًا.
- **إيقافها للجميع:**
```sql
UPDATE PartnerSubscriptions SET CanUsePlacementExams = 0;   -- سجّل القيم قبل التنفيذ لاسترجاعها
```
- **سحب الكود:** رجوع لنسخة سابقة يكفي مع بقاء العمود والفهارس. لا تشغّل `PPL_ExamOwnerPartner_Rollback.sql` إلا بعد سحب الكود، وبعده تظهر اختبارات الشركاء (إن وُجدت) للأدمن كاختبارات المعهد. `PPL_Placement_Indexes_Rollback.sql` آمن في أي وقت.

### 5) الاختبارات
- `QDRAT_TEST_SQL` على قاعدة اسمها يحوي `Test`، و`QDRAT_E2E_DLL` لبناء Debug منفصل إن كان الـ dll مقفولًا: `dotnet test QdratNew.Tests -c Release --filter "FullyQualifiedName~PartnerPlacement"`.

---

## 📋 نشر Epic OLC (المحتوى التعليمي أونلاين للمحاضرات) — OLC-S1 (الأساس)

> S1 يضيف الجداول ومفتاح الميزة فقط — لا واجهة ولا سلوك جديد للمستخدمين. الميزة **مغلقة** (`OnlineLectureContent.Enabled = false`).
> قسم الإطلاق الكامل (S2–S8) في «OLC — الإطلاق» أدناه.

### 1) قبل نشر الكود (Backup إلزامي)
```sql
BACKUP DATABASE [QdratNewDB] TO DISK = N'/var/opt/mssql/backup/QdratNewDB_before_OLC_S1.bak' WITH INIT, COMPRESSION;
```
ثم شغّل `Migrations/Scripts/OLC_AddOnlineLectureContent.sql` (Idempotent؛ يفترض تطبيق `APD_MinistrySimPartnerHidden.sql` قبله).
يضيف: `Batches.IsOnlineLearning` (افتراضي 0) و`Batches.OnlineLearningEnabledAt`، والجداول `LectureContents` و`LectureContentItems` و`LectureContentProgress` و`LectureContentItemProgress`، وصف `SystemSettings` بالمفتاح `OnlineLectureContent.Enabled = false`.

### 2) التحقق بعد السكربت
```sql
SELECT name FROM sys.tables WHERE name LIKE 'LectureContent%';            -- 4 جداول
SELECT COUNT(*) FROM Batches WHERE IsOnlineLearning = 1;                  -- 0
SELECT [Value] FROM SystemSettings WHERE [Key] = N'OnlineLectureContent.Enabled';  -- false
SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%OLC_AddOnlineLectureContent';
```

### 3) خطة التراجع
- لا حاجة لتراجع عن S1 وحده (إضافات فقط، الميزة مغلقة). عند الضرورة: اسحب كود OLC أولًا، ثم `Migrations/Scripts/OLC_AddOnlineLectureContent_Rollback.sql` (⚠ يحذف كل محتوى OLC وتقدّم الطلاب فيه).

---

## 📋 OLC — الإطلاق (OLC-S2 إلى S8)

> S2–S8 **بلا Migration جديدة**: السكربت الوحيد هو `OLC_AddOnlineLectureContent.sql` من S1. الميزة تبقى مغلقة حتى ينتهي الـ UAT.

### 1) النشر (الميزة مغلقة)
1. Backup كما في قسم S1 أعلاه، ثم `OLC_AddOnlineLectureContent.sql` إن لم يُطبَّق بعد (Idempotent — تشغيله مرة ثانية آمن).
2. تأكد أن `OnlineLectureContent.Enabled = false` قبل نشر الكود:
   ```sql
   SELECT [Value] FROM SystemSettings WHERE [Key] = N'OnlineLectureContent.Enabled';
   ```
3. انشر الكود. لا باكدجات جديدة ولا مفاتيح في `appsettings`.
4. **التجربة على نسخة من الإنتاج أولًا (OLC-S8.3):** استعِد آخر Backup باسم مختلف (مثل `QdratNewDB_OlcRehearsal`) وشغّل عليه السكربت ثم سكربت التراجع ثم السكربت مرة أخرى، وسجّل المدة. `Batches` يكسب عمودًا `NOT NULL DEFAULT 0`: على SQL Server Standard قد يعيد كتابة الجدول — صغير عندنا، لكن نفّذه خارج وقت الذروة.

### 2) قبل التفعيل
- **Nginx / CSP:** التطبيق نفسه لا يضبط `Content-Security-Policy`. إن كانت في إعداد Nginx فيجب أن تسمح بـ:
  `frame-src https://www.youtube-nocookie.com https://player.vimeo.com` و`script-src https://www.youtube.com https://player.vimeo.com` (و`https://s.ytimg.com` لمكتبة YouTube). بدونها يظهر المشغّل فارغًا.
- **P12 (تشغيلي):** كل فيديو YouTube «غير مدرج»، وكل فيديو Vimeo مقيّد بنطاق `qdrat.edu.sa` (Domain privacy).
- **مدة الفيديو إلزامية للنشر (OLC-S8.1):** الفيديو الكامل وكل فيديو مؤشر يحتاج مدة يُدخلها الأدمن، وإلا يُرفض النشر، والنبضة على محتوى بلا مدة تُرفض (404) بلا احتساب. السبب: مدة المتصفح قابلة للتزوير وتفتح المحاضرة التالية بلا مشاهدة.
- الصلاحيات: أضف `LectureContents` (Read/Create/Edit/Delete) لبروفايلات الأدمن المعنية، و`ManagePartners` لمن يدير دفعات الشركاء فقط.
- نفّذ `Docs/UAT/OLC_C7_Manual_Checklist.md` على Chrome وSafari iPhone.

### 3) التفعيل
```sql
UPDATE SystemSettings SET [Value] = N'true' WHERE [Key] = N'OnlineLectureContent.Enabled';
```
القيمة مخزّنة مؤقتًا 30 ثانية لكل نسخة من التطبيق — لا حاجة لإعادة تشغيل. ثم فعّل «التعليم أونلاين» لدفعة تجريبية واحدة من شاشة الدفعة.

### 4) بعد التفعيل — تصدير Schema لخطة OLN (OLC-S8.4)
```bash
sqlcmd -S . -E -C -d QdratNewDB -i Migrations/Scripts/OLC_ExportContractSchema.sql -s "|" -W -o olc_schema_$(date +%F).txt
```
قراءة فقط (أعمدة وأنواع وأطوال وافتراضيات وفهارس ومفاتيح أجنبية وأعداد صفوف — بلا بيانات طلاب). يُسلَّم الملف لفريق `qdratAPI` لخطوة `OLN-S8.1` ويُقارن بالعقد v1.2. ملاحظتان للمقارنة: جدول المحاضرات اسمه `[Lecture]` (مفرد) وعمود مفتاح الطلاب `Students.StudentID`.

### 5) المراقبة
- Serilog: ابحث عن `OLC: ping refused — no admin duration` (محتوى منشور قبل S8 بلا مدة — أدخل مدته) و`OLC ping lost a concurrent write` (طبيعي بأعداد قليلة).
- الـ Rate limit `olc-ping` (20/دقيقة لكل مستخدم) في ذاكرة النسخة — يكفي ما دام التطبيق نسخة واحدة خلف Nginx.

### 6) التراجع
- **أسرع تراجع:** `UPDATE SystemSettings SET [Value] = N'false' WHERE [Key] = N'OnlineLectureContent.Enabled';` — يختفي القسم خلال 30 ثانية ويبقى المحتوى وتقدّم الطلاب.
- تراجع الكود وحده لا يحتاج سكربتًا (الجداول إضافية). `OLC_AddOnlineLectureContent_Rollback.sql` فقط عند إلغاء الميزة نهائيًا (⚠ يحذف المحتوى والتقدّم).
