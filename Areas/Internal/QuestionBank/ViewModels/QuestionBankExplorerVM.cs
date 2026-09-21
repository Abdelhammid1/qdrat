using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Modules.QuestionBank.Read.Dtos;
using System.Collections.Generic;

namespace QdratNew.Areas.Internal.QuestionBank.ViewModels
{
    public class QuestionBankExplorerVM
    {


        // Selected Filters
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }

        // Dropdowns
        public List<SelectListItem> Curriculums { get; set; } = new();
        public List<SelectListItem> Sections { get; set; } = new();
        public List<SelectListItem> Lessons { get; set; } = new();

        // Data
        public List<QuestionBankItemDto> Items { get; set; } = new();

        // Stats
        public QuestionBankStatsVM Stats { get; set; } = new();


      
        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }

      
    }
}
