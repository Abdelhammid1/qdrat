using QdratNew.Enums;

namespace QdratNew.ViewModels.Students.Exams
{
    public class StudentExamListItemVm
    {
        public int ExamAssignmentId { get; set; }
        public int ExamId { get; set; }
        public string Title { get; set; } = "";
        public DateTime? ScheduledDate { get; set; }
        public DateTime? EndAt { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }

        // 🔹 فقط لتحديد المصدر
        public StudentExamSource Source { get; set; }

        public bool IsParentRequest { get; set; }
    }

}
