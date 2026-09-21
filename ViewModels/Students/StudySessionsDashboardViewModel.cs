using QdratNew.Entities;

namespace QdratNew.ViewModels.Students
{
    public class StudySessionsDashboardViewModel
    {
        public int Id { get; set; }  // موجودة باسم "Id" بحرف صغير

        // ✅ أقرب جلسة مفصلة
        public StudySessionReservation NextSession { get; set; }

        // ✅ نص جاهز للعرض لو حبيت تستخدمه في واجهة مختصرة
        public string NearestSessionInfo => NextSession != null
            ? $"{NextSession.RequestedDate:dddd - dd MMM} | الساعة {NextSession.StartTime:hh\\:mm}"
            : "لا توجد جلسات قادمة";

        // ✅ عدد الجلسات الموافق عليها خلال الأسبوع
        public int ApprovedThisWeekCount { get; set; }

        // ✅ أسماء المدربين الذين تم الحجز معهم
        public List<string> InstructorsNames { get; set; } = new();

        // ✅ المبلغ المستحق غير المدفوع
        public decimal TotalUnpaidFees { get; set; }

        // ✅ قائمة بكل الجلسات الخاصة بالطالب (اختصرنا الاسم ليكون أنظف)
        public List<StudentSessionReservationListViewModel> AllReservations { get; set; }

    }

}
