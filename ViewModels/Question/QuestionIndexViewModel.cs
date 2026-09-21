// ViewModels/QuestionIndexViewModel.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Entities;

namespace QdratNew.ViewModels.Question
{
    public class QuestionIndexViewModel
    {
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }
        public bool? IsReviewed { get; set; } // فلتر مراجعة السؤال

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public List<SelectListItem> Curriculums { get; set; } = new();
        public List<SelectListItem> Sections { get; set; } = new();
        public List<SelectListItem> Lessons { get; set; } = new();

        public List<QuestionListViewModel> Questions { get; set; } = new();

        public string? SearchTitle { get; set; }  // 🔍 للبحث داخل نص السؤال (بالمعادلات)

        // ✅ أضف هذا السطر
        [Display(Name = "التلميح")]
        public string? InternalNote { get; set; }
        public int UnansweredCount { get; set; }

        public int? UnitId { get; set; }
        public bool OnlyUnanswered { get; set; }  // لتصفية الأسئلة التي لا تحتوي على إجابة صحيحة

        public List<SelectListItem> Units { get; set; } = new();          // ✅ جديد

        public List<string> SelectedLabels { get; set; } = new();

        public QuestionAuditLog? LatestAuditLog { get; set; }

        public int UnreviewedCount { get; set; }
        public int PendingReviewCount { get; set; }
        public int SimilarGroupsCount { get; set; }
        public int TotalQuestionsCount { get; set; }

        public QuestionAuditLog? LatestAudit { get; set; }

        public List<PendingReviewCurriculumStatViewModel> PendingReviewCurriculumStats { get; set; } = new();
        public int PendingReviewFilteredCount { get; set; }
        public int PendingReviewConfirmedAnswersCount { get; set; }
        public int PendingReviewUnconfirmedAnswersCount { get; set; }
        public int PendingReviewQuantitativeCount { get; set; }
        public int PendingReviewVerbalCount { get; set; }

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

    }

    public class PendingReviewCurriculumStatViewModel
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = "—";
        public int PendingCount { get; set; }
        public int ConfirmedAnswersCount { get; set; }
        public int UnconfirmedAnswersCount { get; set; }
        public int QuantitativeCount { get; set; }
        public int VerbalCount { get; set; }
        public int SectionsCount { get; set; }
        public DateTime? OldestCreatedAt { get; set; }
    }
}
