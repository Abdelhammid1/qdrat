using System;

namespace QdratNew.ViewModels.Partner.Homework
{
    public class SentHomeworkStudentVM
    {
        public int StudentId { get; set; }

        public string StudentName { get; set; }

        public bool IsSubmitted { get; set; }

        public DateTime? SubmittedAt { get; set; }
    }
}
