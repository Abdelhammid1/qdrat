using QdratNew.Enums;
using System;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamStudentViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; } = string.Empty;

        // هل يوجد سجل حالة في ExamStudentStatuses؟
        public bool HasStatus { get; set; }

        public ExamStatus Status { get; set; }

        public DateTime AssignedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public int? Score { get; set; }
    }
}
