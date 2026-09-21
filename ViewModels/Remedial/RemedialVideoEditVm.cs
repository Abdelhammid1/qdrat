using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialVideoEditVm
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يجب اختيار المؤشر (الدرس).")]
        [Display(Name = "المؤشر")]
        public int LessonId { get; set; }

        [Required(ErrorMessage = "يجب اختيار المحور.")]
        [Display(Name = "المحور")]
        public int? SectionId { get; set; }

        [Required(ErrorMessage = "يجب اختيار المنهج.")]
        [Display(Name = "المنهج")]
        public int? CurriculumId { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب.")]
        [StringLength(200)]
        [Display(Name = "عنوان الفيديو")]
        public string Title { get; set; }

        [Required(ErrorMessage = "رابط الفيديو مطلوب.")]
        [StringLength(500)]
        [Display(Name = "رابط الفيديو (YouTube أو Vimeo)")]
        public string VimeoUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "نبذة عن الفيديو")]
        public string? Description { get; set; }

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; }

        // 🟩 الأسئلة المرتبطة بالفيديو
        [Display(Name = "الأسئلة المرتبطة")]
        public List<Guid> SelectedQuestionIds { get; set; } = new();

        // 🟦 عرض الأسئلة داخل صفحة التعديل (جدول DataTable)
        public List<QuestionDisplayVm> LessonQuestions { get; set; } = new();
    }

    public class QuestionDisplayVm
    {
        public Guid QuestionId { get; set; }

        // 🟨 نستخدم Html.Raw في الـ View لعرض HTML بشكل طبيعي
        public string QuestionTitle { get; set; }
    }
}
