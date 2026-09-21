# أنماط عرض الأسئلة — مرجع Razor Partials في مشروع قدرات

> **الغرض:** هذا الملف مرجع إلزامي يُستدعى عند بناء أي Partial يعرض سؤالاً أو إجابة في مشروع QdratNew —  
> سواء كان واجباً، اختباراً، بنك أسئلة، عرض نتيجة، أو أي شاشة أخرى.  
> كل قاعدة هنا مستخرجة من الكود الفعلي للمشروع.

---

## 1. نماذج البيانات الأساسية

### `QuestionDisplayViewModel`
```csharp
Guid     Id
string   Title                      // نص السؤال — قد يحتوي HTML
bool     IsRTL                      // true = عربي RTL، false = إنجليزي LTR
bool     IsQuantitative             // true = كمي (أرقام هندية)، false = لفظي
string?  ImageUrl                   // صورة السؤال (إن وجدت)
string?  ComparisonValue1           // قيمة المقارنة الأولى
string?  ComparisonValue2           // قيمة المقارنة الثانية
QuestionDisplayType DisplayType     // نوع عرض السؤال
List<QuestionOptionDisplayViewModel> Options
// القطعة اللفظية:
PassageType? VerbalPassageType       // Text | Audio | Video
string?  VerbalPassageContent        // نص القطعة
string?  VerbalPassageTitle          // عنوان القطعة
string?  VerbalPassageMediaUrl       // رابط الصوت أو الفيديو
bool     VerbalPassageRequireFullListen
int?     PassageStartSeconds         // نقطة البداية في الملف الصوتي
int?     PassageEndSeconds           // نقطة النهاية في الملف الصوتي
// نتائج/حل:
string?  CorrectAnswer
string?  SelectedAnswer
string?  StudentAnswer
bool     IsCorrect
string?  Explanation
string?  VideoUrl                   // فيديو الشرح
string?  Hint
string?  LessonTitle
string?  SectionTitle
```

### `QuestionOptionDisplayViewModel`
```csharp
string? Text       // نص الخيار — قد يحتوي HTML
string? ImageUrl   // صورة الخيار (إن وجدت)
bool    IsSelected // هل هو الإجابة المحددة؟
```

### Enums
```csharp
enum QuestionDisplayType { TextOnly = 0, WithImage = 1, ComparisonText = 2, ComparisonWithImage = 3 }
enum PassageType         { Text = 0, Audio = 1, Video = 2 }
```

---

## 2. قاعدة الاتجاه والترجمة (RTL / LTR)

**`q.IsRTL`** هو المتحكم الوحيد في الاتجاه والنصوص.

```razor
@{
    var isRTL = q?.IsRTL ?? true;
}
<div dir="@(isRTL ? "rtl" : "ltr")" class="@(isRTL ? "" : "ltr")">
```

### دالة الترجمة `T(key)`
يجب تعريفها في كل Partial يعرض نصوص UI:
```csharp
Func<string, string> T = key => isRTL ? key : key switch
{
    "السؤال"                      => "Question",
    "من"                          => "of",
    "القيمة الأولى"               => "Value A",
    "القيمة الثانية"              => "Value B",
    "القطعة اللفظية"              => "Passage",
    "مراجعة سريعة لجميع الأسئلة" => "Quick Review",
    "إنهاء وتسليم الواجب"        => "Submit Homework",
    "السابق"                      => "Previous",
    "التالي"                      => "Next",
    "ضع علامة للمراجعة"          => "Mark for Review",
    "مراجعة سريعة للأسئلة"       => "Review Questions",
    "الوقت"                       => "Time",
    _                             => key
};
```
> ⚠️ أضف مفاتيح جديدة لكل نص UI تضيفه.

---

## 3. قاعدة الأرقام (هندية / إنجليزية)

**المتحكم:** `Model.IsQuantitative` أو `Model.UseIndicNumbers`

| الحالة | عرض الأرقام |
|--------|-------------|
| `IsQuantitative = true` | أرقام هندية (١ ٢ ٣) |
| `UseIndicNumbers = true` | أرقام هندية |
| كلاهما false | أرقام إنجليزية (1 2 3) |

### دالة تحويل المحتوى `htmlConvert(s)`
تُطبَّق على **كل نص** قبل عرضه بـ `Html.Raw`:
```csharp
Func<string, string> htmlConvert = s =>
{
    if (string.IsNullOrWhiteSpace(s)) return s;
    try
    {
        if (Model.IsQuantitative)
        {
            if (s.Contains("style=", StringComparison.OrdinalIgnoreCase))
                return NumberHelper.ToIndicNumbersInsideHtml(s);   // نص به HTML styles
            return HtmlNumberHelper.ConvertHtmlPreservingTags(s, true); // نص عادي أو HTML
        }
        return s; // لفظي → لا تحويل
    }
    catch { return s; }
};
```

### استخدام التحويل في العرض
```razor
@Html.Raw(htmlConvert(q.Title ?? ""))
@Html.Raw(htmlConvert(opt.Text ?? ""))
@Html.Raw(htmlConvert(q.ComparisonValue1 ?? ""))
```
> ⚠️ **دائماً** استخدم `Html.Raw(htmlConvert(...))` وليس `@q.Title` مباشرة لأن النص قد يحتوي HTML.

### تحويل الأرقام في JavaScript (client-side)
```javascript
@if (Model.IsQuantitative || Model.UseIndicNumbers)
{
    <text>
(function convertToIndic() {
    const map = { '0':'٠','1':'١','2':'٢','3':'٣','4':'٤','5':'٥','6':'٦','7':'٧','8':'٨','9':'٩' };
    function walk(node) {
        if (node.nodeType === 3) {
            node.nodeValue = node.nodeValue.replace(/[0-9]/g, d => map[d]);
        } else if (!['SCRIPT','STYLE','INPUT','TEXTAREA'].includes(node.nodeName)) {
            node.childNodes.forEach(walk);
        }
    }
    const container = document.querySelector('.your-container-class');
    if (container) walk(container);
})();
    </text>
}
```

---

## 4. القطعة اللفظية (Verbal Passage)

تُعرض **قبل** بطاقة السؤال. منطق العرض:

```razor
@if (q.VerbalPassageType == PassageType.Audio && !string.IsNullOrWhiteSpace(q.VerbalPassageMediaUrl))
{
    <!-- 🎧 مشغّل صوتي -->
    <div class="passage-audio-wrapper">
        <div class="passage-label">
            🎧 @(isRTL ? "القطعة الصوتية" : "Audio Passage")
            @if (!string.IsNullOrWhiteSpace(q.VerbalPassageTitle))
            { <span> — @q.VerbalPassageTitle</span> }
        </div>
        <audio controls controlslist="nodownload"
               @(q.VerbalPassageRequireFullListen ? "onended=\"this.dataset.done='1'\"" : "")>
            @if (q.PassageStartSeconds.HasValue || q.PassageEndSeconds.HasValue)
            {
                <!-- تشغيل جزء محدد من الملف -->
                <source src="@q.VerbalPassageMediaUrl#t=@(q.PassageStartSeconds ?? 0),@(q.PassageEndSeconds ?? 0)" />
            }
            else
            {
                <source src="@q.VerbalPassageMediaUrl" />
            }
        </audio>
    </div>
}
else if (q.VerbalPassageType == PassageType.Video && !string.IsNullOrWhiteSpace(q.VerbalPassageMediaUrl))
{
    <!-- 🎥 مشغّل فيديو -->
    <div class="passage-video-wrapper">
        <div class="passage-label">🎥 @(isRTL ? "القطعة المرئية" : "Video Passage")</div>
        <video controls style="width:100%; max-height:280px; border-radius:8px;">
            <source src="@q.VerbalPassageMediaUrl" />
        </video>
    </div>
}
else if (!string.IsNullOrWhiteSpace(q.VerbalPassageContent))
{
    <!-- 📖 قطعة نصية -->
    <div class="passage-text-wrapper">
        <div class="passage-title">
            📖
            @if (!string.IsNullOrWhiteSpace(q.VerbalPassageTitle))
                { @q.VerbalPassageTitle }
            else
                { @T("القطعة اللفظية") }
        </div>
        <div class="passage-body">
            @Html.Raw(htmlConvert(q.VerbalPassageContent))
        </div>
    </div>
}
@* إذا لم توجد أي قطعة → لا تعرض شيئاً *@
```

---

## 5. أنواع عرض السؤال (`QuestionDisplayType`)

### 5.1 TextOnly (= 0)
نص السؤال فقط بدون أي إضافات في الـ body:
```razor
<div class="q-title">@Html.Raw(htmlConvert(q.Title ?? ""))</div>
@* الخيارات مباشرة بعد العنوان *@
```

### 5.2 WithImage (= 1)
نص السؤال + صورة في body:
```razor
<div class="q-title">@Html.Raw(htmlConvert(q.Title ?? ""))</div>
@if (!string.IsNullOrWhiteSpace(q.ImageUrl))
{
    <div class="q-image">
        <img src="@Url.Content(q.ImageUrl)" alt="" loading="lazy" />
    </div>
}
@* الخيارات *@
```

### 5.3 ComparisonText (= 2)
نص السؤال + مقارنة بين قيمتين نصيتين:
```razor
<div class="q-title">@Html.Raw(htmlConvert(q.Title ?? ""))</div>
<div class="q-comparison">
    <div class="cmp-box">
        <div class="cmp-label">@T("القيمة الأولى")</div>
        <div class="cmp-val">@Html.Raw(htmlConvert(q.ComparisonValue1 ?? ""))</div>
    </div>
    <div class="cmp-divider">⟷</div>
    <div class="cmp-box">
        <div class="cmp-label">@T("القيمة الثانية")</div>
        <div class="cmp-val">@Html.Raw(htmlConvert(q.ComparisonValue2 ?? ""))</div>
    </div>
</div>
@* الخيارات *@
```

### 5.4 ComparisonWithImage (= 3)
نص السؤال + مقارنة + صورة في المنتصف بدلاً من السهم:
```razor
<div class="q-title">@Html.Raw(htmlConvert(q.Title ?? ""))</div>
<div class="q-comparison">
    <div class="cmp-box">
        <div class="cmp-label">@T("القيمة الأولى")</div>
        <div class="cmp-val">@Html.Raw(htmlConvert(q.ComparisonValue1 ?? ""))</div>
    </div>
    @if (!string.IsNullOrWhiteSpace(q.ImageUrl))
    {
        <div class="cmp-img-center">
            <img src="@Url.Content(q.ImageUrl)" alt="" loading="lazy" />
        </div>
    }
    else { <div class="cmp-divider">⟷</div> }
    <div class="cmp-box">
        <div class="cmp-label">@T("القيمة الثانية")</div>
        <div class="cmp-val">@Html.Raw(htmlConvert(q.ComparisonValue2 ?? ""))</div>
    </div>
</div>
@* الخيارات *@
```

---

## 6. أنماط عرض الخيارات

### تحديد نوع الخيارات (قبل الحلقة)
```csharp
bool allImgOnly = q.Options.All(o => !string.IsNullOrWhiteSpace(o.ImageUrl) 
                                  && string.IsNullOrWhiteSpace(o.Text));
string gridClass = allImgOnly ? "img-grid" : "";
```

### هيكل الخيارات
```razor
<div class="options-list @gridClass">
    @foreach (var opt in q.Options)
    {
        bool isSelected = opt.IsSelected ||
            (!string.IsNullOrWhiteSpace(Model.SelectedAnswer) &&
             string.Equals(opt.Text, Model.SelectedAnswer, StringComparison.OrdinalIgnoreCase));

        <label class="option @(isSelected ? "selected" : "")">
            <input type="radio" name="SelectedOption" value="@opt.Text"
                   @(isSelected ? "checked" : "") />
            <span class="radio-indicator"></span>

            @* ─── ثلاثة أنماط للخيار: ─── *@

            @if (!string.IsNullOrWhiteSpace(opt.ImageUrl) && string.IsNullOrWhiteSpace(opt.Text))
            {
                @* خيار صورة فقط *@
                <img src="@Url.Content(opt.ImageUrl)" class="option-img" alt="" loading="lazy" />
            }
            else if (!string.IsNullOrWhiteSpace(opt.ImageUrl) && !string.IsNullOrWhiteSpace(opt.Text))
            {
                @* خيار صورة + نص *@
                <div class="option-mixed">
                    <img src="@Url.Content(opt.ImageUrl)" class="option-img-small" alt="" loading="lazy" />
                    <span class="option-text">@Html.Raw(htmlConvert(opt.Text ?? ""))</span>
                </div>
            }
            else
            {
                @* خيار نص فقط (الأكثر شيوعاً) *@
                <span class="option-text">@Html.Raw(htmlConvert(opt.Text ?? ""))</span>
            }
        </label>
    }
</div>
```

### CSS لعرض الخيارات-صور بشبكة
```css
.options-list.img-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
}
.options-list.img-grid .option {
    flex-direction: column;
    align-items: center;
    text-align: center;
}
```

---

## 7. قاعدة إخفاء عنوان السؤال (Hidden Title)

في بعض الأسئلة يكون `Title` فارغاً أو يحتوي HTML مخصص بـ `display:none`. التعامل الصحيح:

```razor
@if (!string.IsNullOrWhiteSpace(q.Title))
{
    <div class="q-title">@Html.Raw(htmlConvert(q.Title))</div>
}
```

> ⚠️ لا تحذف `Html.Raw` — النص قد يحتوي HTML styles يخفي جزءاً منه عمداً.  
> لا تضف نص احتياطي ("السؤال" مثلاً) إذا كان `Title` فارغاً.

---

## 8. Helpers المطلوب استيرادها

في أعلى كل Partial يعرض أسئلة كمية:
```razor
@using QdratNew.Helpers
@using QdratNew.Enums
```

| Helper | الاستخدام |
|--------|-----------|
| `NumberHelper.ToIndicNumbersInsideHtml(s)` | نص يحتوي `style=` — يحوّل الأرقام فقط داخل HTML |
| `HtmlNumberHelper.ConvertHtmlPreservingTags(s, true)` | نص عادي أو HTML بدون styles — يحوّل مع الحفاظ على التاغز |

---

## 9. المتغيرات المشتركة — Template لأي Partial

```razor
@{
    var q = Model.Question;  // أو المتغير المناسب لـ ViewModel الخاص بالـ Partial
    var isRTL = q?.IsRTL ?? true;

    Func<string, string> T = key => isRTL ? key : key switch { /* ... */ _ => key };

    Func<string, string> htmlConvert = s =>
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        try
        {
            if (Model.IsQuantitative)
            {
                if (s.Contains("style=", StringComparison.OrdinalIgnoreCase))
                    return NumberHelper.ToIndicNumbersInsideHtml(s);
                return HtmlNumberHelper.ConvertHtmlPreservingTags(s, true);
            }
            return s;
        }
        catch { return s; }
    };
}
```

---

## 10. ترتيب العناصر في الصفحة

الترتيب **الإلزامي** لعناصر عرض السؤال من الأعلى للأسفل:

```
1. [القطعة اللفظية — إن وجدت]
   └─ Audio  → مشغّل صوت
   └─ Video  → مشغّل فيديو
   └─ Text   → صندوق نصي
2. [بطاقة السؤال]
   ├─ العنوان (q.Title)
   └─ body بحسب DisplayType:
       ├─ TextOnly         → لا شيء إضافي
       ├─ WithImage        → صورة
       ├─ ComparisonText   → صندوق مقارنة نصي
       └─ ComparisonWithImage → صندوق مقارنة + صورة مركزية
3. [الخيارات]
   └─ نص | صورة فقط (img-grid) | مختلط
```

---

## 11. حالات خاصة يجب مراعاتها

| الحالة | السلوك المطلوب |
|--------|----------------|
| `opt.Text` و `opt.ImageUrl` كلاهما فارغ | تخطّى الخيار أو اعرض placeholder |
| `q.Options` قائمة فارغة | لا تعرض قسم الخيارات نهائياً |
| `q.ComparisonValue1` فارغ في ComparisonText | اعرض الصندوق بقيمة فارغة، لا تخفيه |
| `IsRTL = false` مع خيارات تحتوي أرقاماً | `htmlConvert` لا يحوّل الأرقام للإنجليزي (السلوك صحيح) |
| صوت يحتوي `PassageStartSeconds` | يجب إضافة `#t=start,end` في الـ `src` |
| `VerbalPassageRequireFullListen = true` | أضف `onended` لتتبع إتمام الاستماع |

---

## 12. Design Tokens المعتمدة

```css
:root {
    --hw-primary:      #4f46e5;
    --hw-primary-soft: #eef2ff;
    --hw-success:      #16a34a;
    --hw-success-soft: #dcfce7;
    --hw-warning:      #d97706;
    --hw-warning-soft: #fef3c7;
    --hw-danger:       #dc2626;
    --hw-surface:      #ffffff;
    --hw-bg:           #f1f5f9;
    --hw-border:       #e2e8f0;
    --hw-text-main:    #1e293b;
    --hw-text-muted:   #64748b;
    --hw-radius:       14px;
    --hw-radius-sm:    8px;
    --hw-font:         'Cairo', 'Tajawal', system-ui, sans-serif;
}
```
> استخدم هذه المتغيرات دائماً في أي Partial جديد لضمان الاتساق البصري.

---

*آخر تحديث: مستخرج من `_SolveHomeworkQuestionPartial.cshtml` + `QuestionDisplayViewModel` + Enums*
