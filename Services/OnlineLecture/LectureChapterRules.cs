namespace QdratNew.Services.OnlineLecture
{
    /// <summary>توقيت فصل واحد داخل الفيديو الكامل. <see cref="Label"/> للرسائل فقط.</summary>
    public readonly record struct ChapterTiming(string Label, int? StartSeconds, int? EndSeconds);

    /// <summary>
    /// OLC-S3.1 — قاعدة R4 للفصول (دالة نقية تُنسخ حرفيًا في المشروع الجديد):
    /// الفصول اختيارية؛ إن وُجدت: <c>0 ≤ Start &lt; End ≤ Duration</c>، بلا تداخل، مرتبة تصاعديًا بالبداية.
    /// الترتيب يُفرض بالفرز (SortOrder = ترتيب البداية) فلا يُرفض إدخال غير مرتب — التداخل فقط يُرفض.
    /// </summary>
    public static class LectureChapterRules
    {
        /// <summary>يعيد قائمة الأخطاء (فارغة = صالح).</summary>
        public static List<string> Validate(IReadOnlyList<ChapterTiming> chapters, int? durationSeconds)
        {
            var errors = new List<string>();
            if (chapters.Count == 0)
                return errors;

            if (durationSeconds is not > 0)
            {
                errors.Add("أدخل مدة الفيديو الكامل أولًا (من تبويب «البيانات والوضع») — نهاية أي فصل لا تتجاوز مدة الفيديو.");
                return errors;
            }

            var duration = durationSeconds.Value;
            foreach (var c in chapters)
            {
                if (c.StartSeconds is null || c.EndSeconds is null)
                {
                    errors.Add($"حدد بداية ونهاية الفصل «{c.Label}».");
                    continue;
                }
                if (c.StartSeconds < 0)
                    errors.Add($"بداية الفصل «{c.Label}» لا تكون سالبة.");
                else if (c.StartSeconds >= c.EndSeconds)
                    errors.Add($"بداية الفصل «{c.Label}» يجب أن تسبق نهايته.");
                else if (c.EndSeconds > duration)
                    errors.Add($"نهاية الفصل «{c.Label}» ({LectureTimeFormat.Format(c.EndSeconds)}) تتجاوز مدة الفيديو ({LectureTimeFormat.Format(duration)}).");
            }

            if (errors.Count > 0)
                return errors;

            var ordered = Order(chapters);
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartSeconds < ordered[i - 1].EndSeconds)
                    errors.Add($"الفصل «{ordered[i].Label}» يتداخل مع الفصل «{ordered[i - 1].Label}» — بداية كل فصل لا تسبق نهاية الفصل قبله.");
            }

            return errors;
        }

        /// <summary>الترتيب المعتمد للفصول: بالبداية ثم النهاية (ثابت للمدخلات المتساوية).</summary>
        public static List<ChapterTiming> Order(IEnumerable<ChapterTiming> chapters)
            => chapters
                .Select((c, index) => (c, index))
                .OrderBy(x => x.c.StartSeconds ?? int.MaxValue)
                .ThenBy(x => x.c.EndSeconds ?? int.MaxValue)
                .ThenBy(x => x.index)
                .Select(x => x.c)
                .ToList();
    }
}
