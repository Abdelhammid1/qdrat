using System;

namespace QdratNew.ViewModels.Partner.Homework
{
    public class SentHomeworkListVM
    {
        public int HomeworkSetId { get; set; }

        public string Title { get; set; }

        public string CourseName { get; set; }

        public string BatchName { get; set; }

        public int TotalStudents { get; set; }

        public int SubmittedStudents { get; set; }

        public int PendingStudents { get; set; }

        public DateTime SentAt { get; set; }

        // PPM-S4.3: أُرسل من نموذج احترافي
        public bool IsFromModel { get; set; }

        public string? SourceModelTitle { get; set; }
    }
}
