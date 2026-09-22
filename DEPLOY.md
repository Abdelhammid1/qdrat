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
