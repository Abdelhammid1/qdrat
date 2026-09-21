using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    // شاشة قائمة اختبارات محاكاة الوزارة — نقطة الدخول الوحيدة للميزة من قائمة الأدمن (لم تكن موجودة في أي Sprint سابق)
    public class MinistrySimExamIndexVm
    {
        public List<MinistrySimExamListItemVm> Exams { get; set; } = new();
        public List<SelectListItem> Courses { get; set; } = new();
    }

    public class MinistrySimExamListItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
