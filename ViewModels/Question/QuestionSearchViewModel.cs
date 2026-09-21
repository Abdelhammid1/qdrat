using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Question
{
    public class QuestionSearchViewModel
    {
        public string? SearchText { get; set; }

        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }

        public List<SelectListItem>? Curriculums { get; set; } = new();
        public List<SelectListItem>? Sections { get; set; } = new();

        public List<QuestionListViewModel>? Results { get; set; } = new();
    }
}
