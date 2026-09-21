using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    public class SimpleMassExamViewModel
    {
        public string Title { get; set; }
        public int BatchId { get; set; }
        public List<int> SelectedStudentIds { get; set; } = new();

        public int QuestionCount { get; set; }
        public int DurationMinutes { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public string GenerationMode { get; set; } // "Model" or "Auto"
        public int? SelectedModelId { get; set; }

        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Students { get; set; } = new();
        public List<SelectListItem> Models { get; set; } = new();


    

        public List<ProfessionalModelSelectionVm> ModelSelections { get; set; } = new();

  
    }

    public class ProfessionalModelSelectionVm
    {
        public int ModelId { get; set; }
        public string ModelTitle { get; set; }
        public int QuestionCount { get; set; } = 0; // عدد الأسئلة من هذا النموذج
    }

}
