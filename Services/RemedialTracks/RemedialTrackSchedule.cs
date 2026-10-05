using QdratNew.Enums;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// جدولة المحاور على الأيام (دوال نقية قابلة للاختبار).
    /// اليوم 1 = وقت النشر نفسه؛ اليوم N = وقت النشر + (N-1) يومًا. التأجيل فقط: لا يُفتح محور قبل إتمام السابق.
    /// </summary>
    public static class RemedialTrackSchedule
    {
        public const int MinDay = 1;
        public const int MaxDay = 60;

        public static DateTime ReleaseAtUtc(DateTime publishAtUtc, int releaseDay)
            => publishAtUtc.AddDays(Math.Max(releaseDay, MinDay) - 1);

        /// <summary>
        /// أيام المحاور بترتيب المحاور. القواعد: ضمن 1..60، لا تتناقص، تبدأ من 1، بلا أيام فارغة.
        /// يعيد قائمة أخطاء (فارغة = جدول سليم).
        /// </summary>
        public static List<string> Validate(IReadOnlyList<int> daysInOrder)
        {
            var errors = new List<string>();
            if (daysInOrder.Count == 0) return errors;

            if (daysInOrder.Any(d => d < MinDay || d > MaxDay))
            {
                errors.Add($"رقم اليوم بين {MinDay} و{MaxDay}.");
                return errors;
            }
            if (daysInOrder[0] != 1)
                errors.Add("المحور الأول يجب أن يكون في اليوم 1.");

            for (var i = 1; i < daysInOrder.Count; i++)
            {
                if (daysInOrder[i] < daysInOrder[i - 1])
                {
                    errors.Add("لا يجوز أن يسبق محورٌ لاحق في الترتيب محورًا أسبق منه في اليوم.");
                    break;
                }
                if (daysInOrder[i] - daysInOrder[i - 1] > 1)
                {
                    errors.Add("لا يجوز ترك يوم فارغ بين الأيام (مثال: 1 ثم 3 بلا يوم 2).");
                    break;
                }
            }
            return errors;
        }

        /// <summary>يضغط الأيام المستخدمة إلى 1..k بنفس الترتيب (بعد حذف محور مثلًا).</summary>
        public static List<int> Compact(IReadOnlyList<int> daysInOrder)
        {
            var map = daysInOrder.Distinct().OrderBy(d => d).Select((d, i) => (d, i: i + 1)).ToDictionary(x => x.d, x => x.i);
            return daysInOrder.Select(d => map[d]).ToList();
        }

        public readonly record struct AxisRow(int AxisProgressId, int Order, RemedialTrackAxisStatus Status, int ReleaseDay);

        /// <summary>
        /// أول محور مغلق سابقُه منتهٍ (اجتاز أو فتحته الإدارة) وحان موعد يومه؛ null إن لم يوجد.
        /// المحاور الأولى (بلا سابق) لا تحتاج هذا: تُفتح عند النشر.
        /// </summary>
        public static AxisRow? FindDue(IReadOnlyList<AxisRow> axesInOrder, DateTime publishAtUtc, DateTime nowUtc)
        {
            for (var i = 1; i < axesInOrder.Count; i++)
            {
                var cur = axesInOrder[i];
                if (cur.Status != RemedialTrackAxisStatus.Locked) continue;

                var prev = axesInOrder[i - 1].Status;
                if (prev is not (RemedialTrackAxisStatus.Passed or RemedialTrackAxisStatus.FailedOpenedByAdmin)) return null;
                return ReleaseAtUtc(publishAtUtc, cur.ReleaseDay) <= nowUtc ? cur : null;
            }
            return null;
        }
    }
}
