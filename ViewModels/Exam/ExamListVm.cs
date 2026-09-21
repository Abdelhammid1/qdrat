namespace QdratNew.ViewModels.Exam
{
    public class ExamListVm
    {
        public int ExamId { get; set; }
        public int ExamAssignmentId { get; set; }
        public string Title { get; set; } = "";
        public DateTime AssignedAt { get; set; }
        public bool IsSubmitted { get; set; }
        public int? Score { get; set; }

        public bool IsExpired { get; set; }
        public DateTime? ScheduledDate { get; set; }  // موعد الاختبار (محلي لتوقيت السعودية)
        public string StatusText { get; set; }

        public bool IsUpcoming { get; set; } // قبل وقت البدء
        public bool IsActive { get; set; }   // داخل الوقت الحالي
        public string Status { get; set; }  // الحالة (مطلوب - منتهي - تم الحل)
        public bool CanStart { get; set; }  // للتحكم بزر البدء

        // ✅ أضف هذا السطر الجديد
        public int DurationMinutes { get; set; }
        public string StartUrl { get; internal set; }




    

        // وقت نهاية الاختبار — مصدر أساسي
        public DateTime? EndAt { get; set; }
        public bool IsIndividual { get; internal set; }
        public string Type { get; internal set; }
        public bool IsExpiringSoon { get; internal set; }
        public bool IsOnline { get; internal set; }
        public bool RequiresPassword { get; internal set; }
        public int CourseId { get; internal set; }
        public int BatchId { get; internal set; }
    }
}
