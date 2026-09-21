using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Homework
{
    public class CreateExtraHomeworkViewModel
    {
        // أساسيات
        public int BatchId { get; set; }
        public string Title { get; set; } = string.Empty;

        // نافذة الحل
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        // إعادة المحاولة
        public bool AllowRetake { get; set; } = true;
        public int MaxRetakes { get; set; } = 2;
        public bool KeepSameQuestionsOnRetake { get; set; } = true;
        public int QuestionsPerStudent { get; set; }

        // اختيار
        [ValidateNever]
        public List<SelectableSectionItem> Sections { get; set; } = new();
        [ValidateNever]
        public List<SelectableStudentItem> Students { get; set; } = new();
        [ValidateNever]
        public List<int> SelectedBatchIds { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem>? Batches { get; set; }

        public List<int> SelectedStudentIds { get; set; } = new List<int>();

    }

    public class SelectableSectionItem
    {
        public int SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public class SelectableStudentItem
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

}
