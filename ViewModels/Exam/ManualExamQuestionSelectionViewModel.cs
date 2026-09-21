using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class ManualExamQuestionSelectionViewModel
    {
        [Required]
        public int ExamId { get; set; }

        [Required]
        public int AssignmentId { get; set; } // ExamAssignmentToBatchId

        public int RequiredCount { get; set; } // ← من إعدادات النظام

        public List<QuestionItemViewModel> AvailableQuestions { get; set; } = new();

        public List<Guid> SelectedQuestionIds { get; set; } = new(); // ← يتم تعبئتها من Checkboxes

        // Sprint 2 (EB3) — تأكيد إجباري + سبب عند وجود محاولات إجابة سابقة على هذا التكليف (دفعة)
        public bool ConfirmResetAttempts { get; set; } = false;
        public string? EditReason { get; set; }
    }

    public class QuestionItemViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public QuestionUsageType UsageTypes { get; set; }
        public string? LessonTitle { get; set; }
        public string? SectionTitle { get; set; }
    }
}
