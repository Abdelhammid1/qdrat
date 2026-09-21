namespace QdratNew.ViewModels.Instructor
{
    public class HomeworkSummaryViewModel
    {
        public int HomeworkId { get; set; }
        public string StudentName { get; set; }
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string SectionTitle { get; set; }
        public DateTime? LectureDate { get; set; }
        public bool IsCompleted { get; set; }
        public double? Score { get; set; }

     
        public string LessonTitle { get; set; }

        public bool? IsCorrect { get; set; }
        public DateTime? SubmittedAt { get; set; }

        // 🔹 FK لمجموعة الواجب (خليها nullable لو العمود ممكن يكون NULL)
        public int? HomeworkSetId { get; set; }

        // 🔹 FK للدرس (int? لو العمود ممكن يكون NULL في القاعدة)
        public int? LessonId { get; set; }



    }
}
