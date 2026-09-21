# كيفية تفعيل HomeworkSecurity في صفحة الواجب

## 1. في View أو Layout الواجب — أضف الـ Overlays والـ Script

```cshtml
@* في نهاية صفحة Start.cshtml أو داخل _QuestionStudentLayout *@

@await Html.PartialAsync("_HomeworkSecurityOverlays")

<script src="~/js/homework-security.js"></script>
<script>
    document.addEventListener('DOMContentLoaded', async () => {
        await HomeworkSecurity.init({
            homeworkSetId:    @Model.HomeworkSetId,
            antiForgeryToken: '@Antiforgery.GetAndStoreTokens(Context).RequestToken',

            // تُستدعى عند ضغط زر الرجوع (اختياري — الافتراضي: overlay)
            onBackAttempt: () => {
                document.getElementById('hw-back-warning').classList.remove('hw-hidden');
                setTimeout(() => document.getElementById('hw-back-warning').classList.add('hw-hidden'), 3000);
            },

            // تُستدعى عند اكتشاف تبويب مكرر
            onDuplicateTab: () => {
                document.getElementById('hw-duplicate-warning').classList.remove('hw-hidden');
            },

            // تُستدعى إذا اكتشف الـ Server أن الواجب منتهٍ
            onSubmitted: () => {
                document.getElementById('hw-submitted-warning').classList.remove('hw-hidden');
            }
        });
    });
</script>
```

## 2. عند إرسال إجابة — تحقق من الوقت

```javascript
// في كود حفظ الإجابة (SubmitAnswerFetch أو SaveAnswerJson)
const timeSecs = getElapsedSeconds(); // من مؤقتك الحالي

// تحقق أمني في الخلفية (لا يوقف العملية)
HomeworkSecurity.checkTime(currentQuestionId, timeSecs);

// ثم أرسل الإجابة كالمعتاد
await fetch('/Students/StudentHomeworkDashboard/SaveAnswerJson', { ... });
```

## 3. منع الإجابة إذا كان التبويب مقيداً (اختياري)

```javascript
document.getElementById('btn-next').addEventListener('click', async () => {
    if (HomeworkSecurity.isTabLocked()) {
        // أظهر تحذير التبويب المكرر بدلاً من المتابعة
        document.getElementById('hw-duplicate-warning').classList.remove('hw-hidden');
        return;
    }
    // ... المتابعة الطبيعية
});
```

## 4. للحصول على anti-forgery token في View

```cshtml
@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery
```

أو استخدم:
```javascript
const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
```
