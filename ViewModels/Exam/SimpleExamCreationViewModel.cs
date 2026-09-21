using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    public class SimpleExamCreationViewModel
    {
        // -------------------------------
        // العنوان
        // -------------------------------
        public string Title { get; set; }

        // -------------------------------
        // اختيار الدفعة
        // -------------------------------
        public int BatchId { get; set; }
        public List<SelectListItem> Batches { get; set; } = new();

        // -------------------------------
        // اختيار الطالب
        // -------------------------------
        public int StudentId { get; set; }
        public List<SelectListItem> Students { get; set; } = new();

        // -------------------------------
        // طريقة التوليد
        // -------------------------------
        public string GenerationMode { get; set; } = "Model";  // Model أو Auto

        // -------------------------------
        // النموذج (عند اختيار From Model)
        // -------------------------------
        public int? SelectedModelId { get; set; }
        public List<SelectListItem> Models { get; set; } = new();

        // -------------------------------
        // عدد الأسئلة
        // -------------------------------
        public int QuestionCount { get; set; } = 10;

        // -------------------------------
        // مدة الاختبار
        // -------------------------------
        public int DurationMinutes { get; set; } = 30;

        // -------------------------------
        // وقت البدء والنهاية
        // -------------------------------
        public DateTime? StartAt { get; set; }    // Nullable حتى لا يسبب خطأ
            = DateTime.Now.AddMinutes(5);         // قيمة افتراضية

        public DateTime? EndAt { get; set; }
            = DateTime.Now.AddHours(1);           // نهاية افتراضية بعد ساعة
    }
}
