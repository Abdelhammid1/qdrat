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
