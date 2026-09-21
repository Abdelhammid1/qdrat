using QdratNew.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class QuestionAttemptNew
    {
        public int Id { get; set; } // PK
        
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public int? ExamId { get; set; }
        public Exam Exam { get; set; }

        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
        public string SelectedAnswer { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
        public double TimeTakenSeconds { get; set; }
        public string? Notes { get; set; }

        // 🟢 لـ واجبات الطالب
        public int? HomeworkSetId { get; set; }
        public int? HomeworkSetAttemptId { get; set; }
        public int? AttemptNumber { get; set; }

        // 🟢 روابط الدرس والمحور
        public int? LessonId { get; set; }
        public int? SectionId { get; set; }

        // 🟡 اختبار الدفعات
        public int? ExamAssignmentId { get; set; }

        // 🟣 اختبار فردي (كان Object وهذا خطأ قاتل)
        public int? ExamAssignmentToStudentId { get; set; }

        // 🟢 مراجعة السؤال
        public bool? IsMarkedForReview { get; set; } = false;

        // 🟢 اختبارات مؤشر الأداء
        public int? PerformanceIndicatorExamId { get; set; }

        [ForeignKey("PerformanceIndicatorExamId")]
        public PerformanceIndicatorExam PerformanceIndicatorExam { get; set; }
        public DateTime? AnsweredAt { get; set; }

        // 🆕 يشير إلى أن الطالب اختار "لا أعرف الإجابة" (اختبارات تحديد المستوى فقط عند تفعيلها)
        // تبقى IsCorrect = false دائمًا في هذه الحالة (تُحتسب كإجابة خاطئة في النتيجة)
        // لكن هذا الحقل يسمح بفصل إحصائياتها عن الأخطاء العادية في التقارير
        public bool IsDontKnowAnswer { get; set; } = false;
    }
}
