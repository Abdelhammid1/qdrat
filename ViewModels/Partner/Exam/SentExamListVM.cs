using System;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class SentExamListVM
    {
        public int ExamId { get; set; }
        public int ExamAssignmentId { get; set; }
        public int BatchId { get; set; }

        public string ExamTitle { get; set; }
        public string BatchName { get; set; }

        public int StudentsCount { get; set; }

        // ✅ إضافات جديدة
        public int AttemptedCount { get; set; }
        public int NotAttemptedCount { get; set; }

        public DateTime SentAt { get; set; }


    }
}
