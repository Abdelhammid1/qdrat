using System;
using System.Collections.Generic;
using QdratNew.Entities;

namespace QdratNew.ViewModels.Exam
{
    public class ExamReplaceQuestionVm
    {
        public int AssignmentId { get; set; }

        // السؤال القديم + ترتيبه الحقيقي داخل الاختبار
        public Guid OldQuestionId { get; set; }
        public string OldTitle { get; set; }
        public int OldOrder { get; set; }
        public string OldLessonTitle { get; set; }
        public string OldSectionTitle { get; set; }

        // بدائل مرتبة لعرضها
        public List<AlternativeQuestionVm> Alternatives { get; set; } = new();
    }

    public class AlternativeQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public int DifficultyLevel { get; set; }
        public string CorrectAnswer { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
    }
}
