using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class RemedialLesson
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("RemedialPlan")]
        public int RemedialPlanId { get; set; }
        public RemedialPlan RemedialPlan { get; set; }

        [ForeignKey("Section")]
        public int SectionId { get; set; }  // المؤشر/المحور الضعيف
        public Section Section { get; set; }

        [Required]
        public string VideoUrl { get; set; } // رابط الفيديو التعليمي
        public string Title { get; set; } // رابط الفيديو التعليمي

        public string SupplementaryMaterial { get; set; } // مواد إضافية (PDF/Link)

        public bool IsCompleted { get; set; } = false;

        // اختبار قصير مرتبط بالمؤشر
        public int? QuizId { get; set; }
        public RemedialQuiz Quiz { get; set; }

        public int? IndicatorSectionId { get; set; }
        [ForeignKey("IndicatorSectionId")]
        public Section? IndicatorSection { get; set; }

    }
}
