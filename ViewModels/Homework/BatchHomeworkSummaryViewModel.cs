using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class BatchHomeworkSummaryViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string InstructorName { get; set; }
        public string CompletionTitle { get; set; }
        public DateTime CompletionDate { get; set; }

        public List<LessonSummary> Lessons { get; set; }
        public List<StudentHomeworkStatus> Students { get; set; }
    }

    public class LessonSummary
    {
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
    }

    public class StudentHomeworkStatus
    {
        public string StudentName { get; set; }
        public bool HasSolved { get; set; }
        public double? Score { get; set; }
    }
}
