using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Attendance
{
    public class AttendanceDashboardVM
    {
        public int? SelectedBatchId { get; set; }
        public DateTime? SelectedDate { get; set; }

        // 🔹 الكروت
        public int TotalBatches { get; set; }
        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public double OverallAttendanceRate { get; set; }

        // 🔹 جدول الدفعات
        public List<BatchAttendanceSummaryViewModel> Batches { get; set; }
            = new List<BatchAttendanceSummaryViewModel>();

        public List<SelectListItem> BatchFilter { get; set; }
            = new List<SelectListItem>();
    }
}