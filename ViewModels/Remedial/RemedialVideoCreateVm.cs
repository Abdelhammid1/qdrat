using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialVideoCreateVm
    {
        [Required]
        public int LessonId { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required, StringLength(500)]
        [Display(Name = "رابط الفيديو (Vimeo أو YouTube)")]
        public string VimeoUrl { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public List<Guid> SelectedQuestionIds { get; set; } = new();

        public List<QuestionOptionVm> LessonQuestions { get; set; } = new();

        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }

        public class QuestionOptionVm
        {
            public Guid QuestionId { get; set; }
            public string QuestionText { get; set; }
        }

    }
}
