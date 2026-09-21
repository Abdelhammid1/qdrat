using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Parents
{
    public class ParentSmartPracticeCreateViewModel
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public int CurriculumId { get; set; }

        public int? SectionId { get; set; }

        [Required, Range(3, 15)]
        public int QuestionCount { get; set; } = 10;

        [Required, Range(5, 30)]
        public int DurationMinutes { get; set; } = 15;

        [Required]
        public string PracticeMode { get; set; } = "QuickPractice";

        // للعرض فقط
        public List<ParentChildCardViewModel> AvailableStudents { get; set; } = new();
        public List<CurriculumSelectItem> AvailableCurriculums { get; set; } = new();
        public List<SectionSelectItem> AvailableSections { get; set; } = new();

        public bool CanCreate { get; set; } = true;
        public string? BlockingReason { get; set; }
        public string? StudentInsightSummary { get; set; }
    }

    public class CurriculumSelectItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class SectionSelectItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}
