using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    // ✅ لعرض قائمة اختبارات المستوى
    public class PlacementExamListVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime CreatedAt { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }




        // ✅ عدد الطلاب المرسَل إليهم الاختبار
        public int StudentCount { get; set; }

        // ✅ رقم الدفعة المرتبطة بالاختبار
        public int BatchId { get; set; }


        public bool IsActive { get; set; }
        public string? ReferenceCode { get; set; }

        // ✅ عدد الطلاب الذين قاموا فعلاً باختبار هذا الاختبار ضمن الدفعة
        public int TestedCount { get; set; }

    }

    // ✅ لإنشاء اختبار جديد
    public class PlacementExamCreateVm
    {
        [Required(ErrorMessage = "اختيار الطالب مطلوب")]
        public int? StudentId { get; set; }

        [Required(ErrorMessage = "اختيار الدورة مطلوب")]
        public int? CourseId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Students { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new();
    }


    // ✅ لعرض تفاصيل اختبار المستوى
    public class PlacementExamDetailsVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<int> Questions { get; set; } = new();
    }
}
