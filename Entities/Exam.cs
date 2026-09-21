using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class Exam
    {
        public int Id { get; set; }

        [Display(Name = "عنوان الاختبار")]
        public string Title { get; set; }

        [Display(Name = "نوع الاختبار")]
        public ExamType Type { get; set; }

        [Display(Name = "المنهج")]
        public int? CurriculumId { get; set; }
        public Curriculum Curriculum { get; set; }

        [Display(Name = "الدورة")]
        public int? CourseId { get; set; }
        public Course Course { get; set; }

        [Display(Name = "المؤشر المرتبط")]
        public int? LessonId { get; set; }
        public Lesson Lesson { get; set; }

        [Display(Name = "المحور")]
        public int? SectionId { get; set; }
        public Section Section { get; set; }

        [Display(Name = "عدد الأسئلة")]
        public int TotalQuestions { get; set; } = 10;

        [Display(Name = "عدد الأسئلة السهلة")]
        public int EasyQuestionCount { get; set; } = 3;

        [Display(Name = "عدد الأسئلة المتوسطة")]
        public int MediumQuestionCount { get; set; } = 4;

        [Display(Name = "عدد الأسئلة الصعبة")]
        public int HardQuestionCount { get; set; } = 3;

        [Display(Name = "المدة الزمنية (بالدقائق)")]
        public int DurationMinutes { get; set; } = 30;

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "نشط؟")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "من نموذج احترافي؟")]
        public bool IsFromProfessionalModel { get; set; } = false;

        public List<ExamQuestion> Questions { get; set; } = new();
        //public DateTime StartAt { get; internal set; }
        //public DateTime EndAt { get; internal set; }

        public string? ReferenceCode { get; set; }

        public bool RandomizeQuestions { get; set; } = true;

        // ✅ يسمح بإضافة اختيار خامس "لا أعرف الإجابة" لهذا الاختبار تحديدًا (اختبارات تحديد المستوى)
        // لا يُخزَّن كخيار حقيقي في QuestionOption حتى لا يظهر في استخدامات أخرى لنفس السؤال (واجبات/اختبارات أخرى)
        [Display(Name = "السماح بخيار \"لا أعرف الإجابة\"")]
        public bool AllowDontKnowOption { get; set; } = false;

    }
}
