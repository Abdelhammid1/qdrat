using QdratNew.Enums;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S11.4 (D23/D28): نصوص تقارير ولي الأمر — دوال نقية تُغطّى باختبارات تحمي مسؤولية ولي الأمر من الحذف سهوًا.
    /// المعنى الملزم: (1) عدم اجتياز الطالب، (2) الانتقال بقرار الإدارة دون اجتياز، (3) المتابعة على عاتق ولي الأمر بالتعاون مع الطالب.
    /// </summary>
    public static class RemedialTrackParentCopy
    {
        public static string Title(RemedialTrackParentReportKind kind) => kind switch
        {
            RemedialTrackParentReportKind.AxisNotPassed => "تقرير متابعة: عدم اجتياز أحد محاور الخطة العلاجية",
            RemedialTrackParentReportKind.Final => "التقرير الختامي للخطة العلاجية",
            _ => "تقرير الخطة العلاجية"
        };

        /// <summary>نص الإشعار القصير الذي يصل ولي الأمر عند إنشاء التقرير.</summary>
        public static string NotificationMessage(RemedialTrackParentReportKind kind, string studentName, string? axisTitle) => kind switch
        {
            RemedialTrackParentReportKind.AxisNotPassed =>
                $"📋 لم يجتز الطالب {studentName} المحور «{axisTitle}» في الخطة العلاجية. تقرير المتابعة متاح لاطلاعكم.",
            RemedialTrackParentReportKind.Final =>
                $"📋 انتهت الخطة العلاجية للطالب {studentName}. التقرير الختامي متاح لاطلاعكم.",
            _ => $"📋 تقرير جديد عن الخطة العلاجية للطالب {studentName}."
        };

        /// <summary>فقرة تقرير «عدم اجتياز المحور»، وفيها مسؤولية ولي الأمر صراحة.</summary>
        public static string AxisNotPassedBody(string axisTitle) =>
            $"لم يجتز ابنكم/ابنتكم المحور ({axisTitle}) في الاختبارين. " +
            "سيُتاح المحور التالي بقرار من إدارة المعهد، ونرجو من ولي الأمر متابعة هذا المحور مع الطالب والتأكد من استيعابه، " +
            "إذ سيُنقل الطالب بقرار الإدارة دون اجتياز اختباره، وتقع المتابعة على عاتق ولي الأمر بالتعاون مع الطالب.";

        /// <summary>سطر يُضاف عند فتح الإدارة المحور التالي (إشعار نصي في هذا الإصدار).</summary>
        public static string AdminOpenedNextNotice(string studentName, string? axisTitle) =>
            $"📘 تم فتح المحور التالي للطالب {studentName} بقرار الإدارة" +
            (string.IsNullOrWhiteSpace(axisTitle) ? string.Empty : $" بعد المحور «{axisTitle}»") +
            "، والمحور السابق ما زال بحاجة لمتابعتكم.";

        /// <summary>فقرة التقرير الختامي: ملخص + نقاط المتابعة المتبقية.</summary>
        public static string FinalBody(int passedAxes, int totalAxes, int followUpAxes) =>
            followUpAxes > 0
                ? $"أنهى الطالب الخطة العلاجية باجتياز {passedAxes} من {totalAxes} محاور، و{followUpAxes} من المحاور لم تُجتز وانتقل منها الطالب بقرار الإدارة أو ما زالت بحاجة لمتابعة. " +
                  "نرجو من ولي الأمر متابعة هذه المحاور مع الطالب والتأكد من استيعابها، وتقع المتابعة على عاتق ولي الأمر بالتعاون مع الطالب."
                : $"أنهى الطالب الخطة العلاجية باجتياز {passedAxes} من {totalAxes} محاور. نرجو الاستمرار في المتابعة والحفاظ على نفس وتيرة المذاكرة.";

        /// <summary>تنبيه المسؤولية الثابت أسفل أي تقرير (يُعرض مع كل أنواع التقارير).</summary>
        public const string ResponsibilityFooter =
            "ملاحظة: متابعة المحاور التي لم يجتزها الطالب مسؤولية مشتركة بين الطالب وولي الأمر، والمعهد يوفّر التقارير والتوصيات لدعم هذه المتابعة.";
    }
}
