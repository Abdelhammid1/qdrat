using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    public class CreatePerformanceExamViewModel
    {
        public string ExamTitle { get; set; } = string.Empty;

        public int CurriculumId { get; set; }
        [ValidateNever]
        public List<int> SelectedBatchIds { get; set; } = new();
        [ValidateNever]
        public List<int>? SelectedStudentIds { get; set; } = new(); // إن كان الاختبار خاص بمجموعة طلاب

        public bool IsOnline { get; set; }
        public int TotalQuestions { get; set; } = 12;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        public bool ManualSelection { get; set; } // هل الاختيار يدوي أم تلقائي
        public int? ProfessionalModelId { get; set; } // في حال تم توليد الأسئلة من نموذج احترافي

        // بيانات العرض
        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Batches { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Students { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Models { get; set; } = new();

        public string? ErrorMessage { get; set; }


        // ✅ القوائم التي يجب تعبئتها قبل الوصول إلى الصفحة
        [ValidateNever]
        public List<SelectListItem> AvailableCurriculums { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> AvailableBatches { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> AvailableModels { get; set; } = new();

        // ✅ الجديد: قائمة الطلاب
        [ValidateNever]
        public List<SelectListItem> AvailableStudents { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> AvailableProfessionalModels { get; set; }  // ⬅️ أضف هذه

        // ✅ الجديد: قائمة الطلاب المختارين
    }

}
