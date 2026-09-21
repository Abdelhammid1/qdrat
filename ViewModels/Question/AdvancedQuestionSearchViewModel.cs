using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Question;

public class AdvancedQuestionSearchViewModel
{
    public int? CurriculumId { get; set; }
    public int? SectionId { get; set; }
    public string? SearchText { get; set; }

    public List<SelectListItem> Curriculums { get; set; } = new();
    public List<SelectListItem> Sections { get; set; } = new();

    public List<QuestionListViewModel> Results { get; set; } = new();
}
