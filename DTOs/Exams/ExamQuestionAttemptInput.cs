namespace QdratNew.DTOs.Exams
{
    public class ExamQuestionAttemptInput
    {

        public int StudentId { get; set; }

        // أحدهما فقط يكون له قيمة
        public int? ExamAssignmentId { get; set; }
        public int? ExamAssignmentToStudentId { get; set; }

        public Guid QuestionId { get; set; }

        public string SelectedAnswer { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public int TimeTakenSeconds { get; set; }

        public bool IsMarkedForReview { get; set; }




        // ⏱️ الوقت الفعلي فقط (يأتي من الواجهة)

    }
}
