using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner.Homework
{
    public class HomeworkAutoGenerateVM
    {
        // ===============================
        // 📘 السياق التعليمي
        // ===============================

        [Required]
        public int CourseId { get; set; }

        // Legacy (لا نحذفه لتجنب كسر أي كود قديم)
        public int? CurriculumId { get; set; }
        public List<int> SelectedLessonIds { get; set; } = new();

        // ===============================
        // ✅ النظام الفعلي المستخدم
        // ===============================
        public List<LessonQuestionCountVM> LessonQuestionCounts { get; set; } = new();

        // ===============================
        // 🔢 إعدادات
        // ===============================
        public int QuestionsPerLesson { get; set; } = 4;

        // ===============================
        // 🏦 مصدر الأسئلة
        // ===============================
        public bool UsePlatformQuestionBank { get; set; } = true;
        public bool UsePrivateQuestionBank { get; set; } = false;

        // ===============================
        // 📝 بيانات عامة
        // ===============================
        [Required(ErrorMessage = "عنوان الواجب مطلوب.")]
        public string Title { get; set; }
    }

    // ===============================
    // ✅ المستخدم فعليًا في النظام
    // ===============================
    public class LessonQuestionCountVM
    {
        public int LessonId { get; set; }

        [Range(0, 30, ErrorMessage = "عدد الأسئلة من 0 إلى 20")]
        public int QuestionCount { get; set; }
    }
}