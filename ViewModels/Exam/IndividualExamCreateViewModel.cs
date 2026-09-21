using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class IndividualExamCreateViewModel
    {
        public string Title { get; set; }

        // اختيار الدفعة
        public int BatchId { get; set; }
        public List<SelectListItem> Batches { get; set; } = new();

        // اختيار مجموعة طلاب من الدفعة
        public List<int> SelectedStudentIds { get; set; } = new();
        public List<SelectListItem> Students { get; set; } = new();

        // التوليد: نموذج أو تلقائي
        public string GenerationMode { get; set; } = "Auto";

        // لو التوليد من النماذج الاحترافية
        public List<int> SelectedModelIds { get; set; } = new();
        public List<SelectListItem> Models { get; set; } = new();

        // عدد الأسئلة
        public int QuestionCount { get; set; } = 20;

        // الزمن بالدقائق
        public int DurationMinutes { get; set; } = 30;

        // وقت البدء والنهاية (اختياري)
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        // النموذج المختار (عند استخدام Model)
        public int? SelectedModelId { get; set; }
        // 🔹 الطالب المستهدف
        public int StudentId { get; set; }
        public object ExamId { get; internal set; }
        public int AssignmentId { get; internal set; }
    }
}
