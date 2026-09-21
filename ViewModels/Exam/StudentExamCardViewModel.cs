using QdratNew.Enums;

namespace QdratNew.ViewModels.Exam
{
    public class StudentExamCardViewModel
    {


        public int ExamAssignmentId { get; set; }
        public int ExamId { get; set; }
        public string Title { get; set; }

        public DateTime AssignedAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool IsSubmitted { get; set; }
        public int? Score { get; set; }

        public bool IsOnline { get; set; }
        public bool RequiresPassword { get; set; }






 
        public bool IsCompleted { get; set; }
        public bool IsIndividual { get; internal set; }
        public string StatusText { get; internal set; }
        public DateTime? ScheduledDate { get; internal set; }
        public int DurationMinutes { get; internal set; }
        public string ExamTitle { get; internal set; }
        public string BatchName { get; internal set; }
        public ExamStatus Status { get; internal set; }
    }
}
