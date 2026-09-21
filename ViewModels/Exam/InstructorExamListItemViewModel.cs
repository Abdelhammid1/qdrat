using System;

namespace QdratNew.ViewModels.Exam
{
    public class InstructorExamListItemViewModel
    {
        public int ExamAssignmentId { get; set; }

        public string Title { get; set; }
        public string BatchName { get; set; }
        public string SectionTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int DurationMinutes { get; set; }

        public DateTime? ScheduledDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool IsSent { get; set; }

        // عرض حالة الإرسال بشكل بصري
        public string StatusText => IsSent ? "📤 تم الإرسال" : "⏳ لم يُرسل بعد";
        public string StatusClass => IsSent ? "text-success" : "text-warning";

        // هل هو حضوري بناءً على التاريخ؟
        public bool IsScheduled => ScheduledDate.HasValue;
        public string DateText => IsScheduled
            ? $"📅 {ScheduledDate.Value:yyyy-MM-dd HH:mm}"
            : "⏱ لم يُحدد موعد";

        // روابط محتملة لاحقًا
        public string ViewResultsUrl { get; set; } // يمكن توليده في View
        public string SendUrl { get; set; }
    }
}
