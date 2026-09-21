using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using QdratNew.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace QdratNew.ViewModels.Question
{
    public class QuestionCreateViewModel
    {
        public QuestionCreateViewModel()
        {
            // ✅ افتراضيًا تفعيل جميع التصنيفات (باستثناء None)
            SelectedUsageTypes = Enum.GetValues(typeof(QuestionUsageType))
                .Cast<QuestionUsageType>()
                .Where(x => x != QuestionUsageType.None)
                .ToList();
        }

        public Guid Id { get; set; }
       
        [AllowHtmlInput]
        public string? Title { get; set; }

        public QuestionTemplate Template { get; set; }
        [AllowHtmlInput]

        public string? ValueA { get; set; }
        [AllowHtmlInput]

        public string? ValueB { get; set; }

        public string? CorrectAnswer { get; set; }
        [AllowHtmlInput]

        public string? Explanation { get; set; }
        public string? VideoUrl { get; set; }

        public DifficultyLevel Difficulty { get; set; }
        [AllowHtmlInput]

        public string? Hint { get; set; }

        public IFormFile? ImageFile { get; set; }
        public string? ExistingImageUrl { get; set; }
        public bool RemoveImage { get; set; }

        [Required]
        public int CurriculumId { get; set; }
        [Required]
        public int SectionId { get; set; }

        [Required]
        public int LessonId { get; set; }

        public bool IsQuantitative { get; set; } = false;

        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Sections { get; set; }
        [ValidateNever]
        public List<SelectListItem> Lessons { get; set; } = new();

        // ✅ التصنيفات المتعددة باستخدام Flags Enum
        public List<QuestionUsageType> SelectedUsageTypes { get; set; }

        [Display(Name = "التلميح")]
        [AllowHtmlInput]

        public string? InternalNote { get; set; }

        public int? SelectedCorrectIndex { get; set; }

        public string? ImageUrl { get; set; } // للمعاينة فقط (Base64)
        [AllowHtmlInput]

        public string? ComparisonValue1 { get; set; }
        [AllowHtmlInput]

        public string? ComparisonValue2 { get; set; }

        public List<SelectListItem> VerbalPassages { get; set; } = new();

        // =========================
        // 🔹 Media Segmentation (جديد)
        // =========================

        [Display(Name = "بداية المقطع")]
        public int? PassageStartSeconds { get; set; }

        [Display(Name = "نهاية المقطع")]
        public int? PassageEndSeconds { get; set; }


        public int? VerbalPassageId { get; set; } // هذا هو الذي يُرسل للفورم



        public List<QuestionOptionViewModel> Options { get; set; } = new()
        {
            new QuestionOptionViewModel(),
            new QuestionOptionViewModel(),
            new QuestionOptionViewModel(),
            new QuestionOptionViewModel()
        };

        public List<SelectListItem> Units { get; set; } = new();
        public bool IsRTL { get; internal set; }
    }

    public class QuestionOptionViewModel
    {
        [AllowHtmlInput]

        public string? Text { get; set; }
        public IFormFile? ImageFile { get; set; }

        public bool RemoveImage { get; set; } // ✅ أضف هذا السطر
        public string? ExistingImageUrl { get; set; }

        public string? ImageUrl { get; set; }

    }
}
