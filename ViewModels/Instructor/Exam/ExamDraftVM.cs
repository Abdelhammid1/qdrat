using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class ExamDraftVM
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public DateTime CreatedAt { get; set; }

        public int QuestionCount { get; set; }
        [ValidateNever] // ✅ تجاهل التحقق لهذا الحقل

        public List<ExamDraftQuestionVM> Questions { get; set; } = new();
        public List<SectionGroupVM> SectionGroups { get; internal set; }
    }
}