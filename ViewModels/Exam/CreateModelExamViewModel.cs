using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Services.ProfessionalModels;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class CreateModelExamViewModel
    {
        [Required]
        public int ModelId { get; set; }
        public int BatchId { get; set; }

        public int? CurriculumId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();

        // ✅ بدل BatchId إلى قائمة دفعات متعددة
        [Required]
        public List<int> SelectedBatchIds { get; set; } = new List<int>();

        public string? Title { get; set; } // اختياري كما في الشاشة: كان Required ضمنيًا فيمنع المتصفح الإرسال بلا عنوان

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        [Required]
        [Range(5, 180, ErrorMessage = "مدة الامتحان يجب أن تكون بين 5 و 180 دقيقة")]
        public int DurationMinutes { get; set; }

        public List<int> SelectedStudentIds { get; set; } = new List<int>();

        public List<SelectListItem> Models { get; set; }
        public List<SelectListItem> Batches { get; set; }
        public List<SelectListItem> Students { get; set; }

        public bool SendToBatch { get; set; }  // true = إرسال للدفعة كاملة، false = لطلاب محددين فقط
        public bool IsInLab { get; set; } = false;  // اختياري – حضوري أو لا
        public bool RandomizeQuestions { get; set; } = true;

        // APD-S2.2: اختيار جمهور الإرسال. ما يُرسل منه يُربط في AudienceSelection
        [BindNever]
        [ValidateNever]
        public QdratNew.ViewModels.PartnerDispatch.DispatchAudiencePickerVm AudiencePicker { get; set; } = null!;

        // تأكيد تحذيرات التكرار (PPM-S6.2)
        public bool ConfirmWarnings { get; set; }

        [BindNever]
        [ValidateNever]
        public ModelSendPreview? SendPreview { get; set; }
    }
}
