namespace QdratNew.Services.OnlineLecture
{
    /// <summary>OLC-S5.2 — مدخل القفل التسلسلي لمحاضرة واحدة ذات محتوى منشور (R13).</summary>
    public sealed record SequenceLecture(int LectureContentId, int LectureId, int CurriculumId,
                                         DateTime Date, TimeSpan? ScheduledTime, bool IsCompleted);

    /// <summary>
    /// OLC-S5.2 — حالة المحاضرة في التسلسل. <see cref="CanOpen"/> إضافة للعرض والمشاهدة فقط (لا تغيّر Compute):
    /// المكتملة تبقى قابلة للمراجعة حتى لو صارت مقفلة بنشر محاضرة أقدم لاحقًا (ملاحظة R15: «لا يتأثر ما اكتمل»).
    /// </summary>
    public sealed record SequenceState(int LectureContentId, bool IsLocked, bool IsCompleted)
    {
        public bool CanOpen => !IsLocked || IsCompleted;
    }

    /// <summary>
    /// OLC-S5.2 — دالة نقية تُنسخ حرفيًا في المشروع الجديد (OLN). لا قاعدة بيانات ولا وقت ولا حالة.
    /// R13: لكل منهج مستقلًا؛ الترتيب (Date, ScheduledTime ?? 0, LectureId)؛ مفتوحة إن كانت كل السابقة مكتملة.
    /// المحتوى Hidden/Draft/المحذوف لا يُمرَّر لها أصلًا (يستبعده ILectureSequenceService).
    /// </summary>
    public static class LectureSequenceCalculator
    {
        public static IReadOnlyDictionary<int, SequenceState> Compute(IEnumerable<SequenceLecture> lectures)
        {
            var result = new Dictionary<int, SequenceState>();
            foreach (var g in lectures.GroupBy(x => x.CurriculumId))
            {
                var allPrevDone = true;
                foreach (var x in g.OrderBy(x => x.Date.Date).ThenBy(x => x.ScheduledTime ?? TimeSpan.Zero).ThenBy(x => x.LectureId))
                {
                    result[x.LectureContentId] = new SequenceState(x.LectureContentId, !allPrevDone, x.IsCompleted);
                    allPrevDone = allPrevDone && x.IsCompleted;
                }
            }
            return result;
        }
    }
}
