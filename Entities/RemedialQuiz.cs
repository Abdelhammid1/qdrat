using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class RemedialQuiz
    {
        [Key]
        public int Id { get; set; }

        public string Title { get; set; } // مثال: اختبار قصير - مؤشر الكسور

        public int TotalQuestions { get; set; }
        public int PassingScore { get; set; } = 70; // النسبة المطلوبة للنجاح %

        // ربط بالأسئلة (من بنك الأسئلة الموجود)
        public ICollection<Question> Questions { get; set; }

        // نتائج الطالب
        public ICollection<StudentRemedialQuizResult> Results { get; set; }

        public string QuizType { get; set; } = "Remedial"; // Remedial / Indicator / PostRemedial



        // ✅ ربط الاختبار بالمؤشر (الدرس)
        [ForeignKey("Lesson")]
        public int? LessonId { get; set; }  // Nullable لتفادي مشاكل البيانات القديمة
        public Lesson? Lesson { get; set; }

        // ✅ ربط الاختبار بالخطة العلاجية (اختياري)
        [ForeignKey("RemedialPlan")]
        public int? RemedialPlanId { get; set; }
        public RemedialPlan? RemedialPlan { get; set; }

        // ✅ تاريخ الإنشاء
        public DateTime CreatedAt { get; set; } = DateTime.Now;


     
        // 🟢 الربط مع الجلسة العلاجية
        [ForeignKey("RemedialSession")]
        public int RemedialSessionId { get; set; }
        public RemedialSession RemedialSession { get; set; }

        // 🔹 خصائص أخرى موجودة عندك
        public string Description { get; set; }



    }


}
