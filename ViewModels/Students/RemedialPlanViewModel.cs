using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Students
{
    public class RemedialPlanViewModel
    {
        public int StudentID { get; set; }
        public string StudentName { get; set; }

        [Required]
        public string WeakTopics { get; set; }

        [Required(ErrorMessage = "عنوان الخطة مطلوب")]
        [StringLength(100, ErrorMessage = "العنوان لا يجب أن يتجاوز 100 حرف")]
        public string Title { get; set; }

        [StringLength(500, ErrorMessage = "الوصف لا يجب أن يتجاوز 500 حرف")]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;


        public string RecommendedMaterials { get; set; }

        [Range(1, 30)]
        public int TotalSessions { get; set; }

        [Range(1, 50)]
        public int TotalLessons { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string PerformanceLevel { get; set; }
        public string Recommendations { get; set; }

        // AI
        public string AISuggestedTopics { get; set; }
        public string AIRecommendations { get; set; }
        public double AIAssessedRiskScore { get; set; }
        public string AIAssessedLevel { get; set; }

        public double CompletionPercentage { get; set; }



    }
}
