using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum ExamType
    {
        [Display(Name = "اختبار قياس مستوى")]
        LevelAssessment = 0,

        //[Display(Name = "اختبار على المنهج")]
        //Curriculum = 1,

        [Display(Name = "اختبار على الدورة")]
        Course = 2,

        //[Display(Name = "واجب المحاضرة")]
        //SessionHomework = 3,

        [Display(Name = "اختبار محاكاة الوزارة")]
        GovernmentMock = 4,

        [Display(Name = "اختبار المهارات المعززة")]
        LessonSkillsReinforcement = 5,
        [Display(Name = "اختبار مقياس مؤشرات الأداء")]
        PerformanceScale = 6,
        [Display(Name = "اختبار المحور")]
        SectionExam = 7,
        [Display(Name = "اختبار النماذج")]
        Manual = 8,

        [Display(Name = "اختبار نهاية القسم")]
        CurriculumFinal = 9,

    }

}
