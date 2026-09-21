using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Lesson
{
    public class LessonViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string UnitTitle { get; set; }
        public string SectionTitle { get; set; }
        public bool IsQuantitative { get; set; } // ✅ تم إضافته


    }
}
