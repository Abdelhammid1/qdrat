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
