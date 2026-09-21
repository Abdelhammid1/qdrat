using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class SaveExamDraftVM
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public int CurriculumId { get; set; }

        [Required]
        public List<Guid> QuestionIds { get; set; } = new();
    }
}
