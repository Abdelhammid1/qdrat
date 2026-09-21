namespace QdratNew.ViewModels.Students
{
    public class StudentExamSolveSubmitVm
    {
        public int ExamAssignmentId { get; set; }
        public Guid QuestionId { get; set; }

        // إجابة الطالب (قد تكون فارغة = تخطي)
        public string? SelectedAnswer { get; set; }

        // وسم للمراجعة
        public bool IsMarkedForReview { get; set; }

        // زمن الحل بالثواني
        public int TimeTakenSeconds { get; set; }

        // نوع التنقل
        // next | prev | review
        public string Nav { get; set; } = "next";

        public int? NextQuestionId { get; set; }
    }
}
