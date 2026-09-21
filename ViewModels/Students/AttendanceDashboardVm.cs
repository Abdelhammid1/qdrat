using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Attendance;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class AttendanceDashboardVm
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




        public List<StudentAttendanceViewModel> Records { get; set; } = new();
        public int PresentLectures { get; set; }
        public int AbsentLectures { get; set; }
        public double AttendancePercent { get; set; }
    }
}
