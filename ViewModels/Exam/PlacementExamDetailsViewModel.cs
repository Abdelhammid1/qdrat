using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamDetailsViewModel
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; } = string.Empty;

        public int TotalQuestions { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsInLab { get; set; }
        public string? ReferenceCode { get; set; }
        public List<PlacementExamReviewSectionVm> Sections { get; set; } = new();

        // الدفعات المرتبطة بهذا الاختبار
        public List<PlacementExamBatchInfo> AssignedBatches { get; set; } = new();

        // جميع طلاب الدفعة مع حالتهم (حتى لو لم يبدأوا بعد)
        public List<PlacementExamStudentViewModel> StudentStatuses { get; set; } = new();
    }

    public class PlacementExamBatchInfo
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public bool IsSentToStudents { get; set; }
        public int StudentCount { get; set; }
    }
}
