using QdratNew.ViewModels.Question;
using System.Collections.Generic;

namespace QdratNew.ViewModels.ProfessionalModel
{
    public class ProfessionalModelAddQuestionsVm
    {
        public int ModelId { get; set; }
        public string ModelTitle { get; set; }

        // اختياري – لو احتجته لاحقًا
        public List<QuestionVm> AvailableQuestions { get; set; } = new();
    }
}
