namespace QdratNew.ViewModels.Homework
{
    public class ModelAssignmentViewModel
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string Title { get; set; }
        public string BatchName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int StudentCount { get; set; }
        public int QuestionCount { get; set; }
        public string Type { get; set; } // واجب أو اختبار
        public bool IsArchived { get; set; }

        /// <summary>عدد الطلاب الموقوفين حاليًا بسبب رصد ترجمة المتصفح (Translation Guard) على هذا الواجب/الاختبار</summary>
        public int BlockedStudentsCount { get; set; }
    }
}
