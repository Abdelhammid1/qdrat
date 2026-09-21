namespace QdratNew.ViewModels.Partner.Exam
{
    public class PartnerExamAttemptsVM
    {
        public int ExamAssignmentId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string ExamTitle { get; set; }

        public int ResendCount { get; set; }

        public List<PartnerExamAttemptRowVM> Attempts { get; set; } = new();
    }

    public class PartnerExamAttemptRowVM
    {
        public int AttemptNumber { get; set; }
        public DateTime AttemptedAt { get; set; }
        public bool IsCorrect { get; set; }
        public double TimeTakenSeconds { get; set; }
    }

}
