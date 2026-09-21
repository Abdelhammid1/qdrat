namespace QdratNew.Entities
{
    public class StudySessionReportViewModel
    {
        public DateTime RequestedDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public required string StudentName { get; set; }
        public required string BranchName { get; set; } // ✅ اسم الفرع
        public required string InstructorName { get; set; } // ✅ اسم المدرب (اختياري)
        public required string SessionType { get; set; } // ✅ نوع الجلسة (عادية - علاجية)
        public required string Status { get; set; }
        public decimal Fee { get; set; }
        public required string PaymentStatus { get; set; } // ✅ حالة الدفع
    }
}
