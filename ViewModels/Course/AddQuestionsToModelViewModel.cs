using QdratNew.Entities;
using QdratNew.Models;
using QdratNew.ViewModels.Question;
using System.Collections.Generic;


namespace QdratNew.ViewModels.Course
{
    public class AddQuestionsToModelViewModel
    {
        public int ModelId { get; set; }
        public string ModelTitle { get; set; }
      
        public List<QuestionVm> AvailableQuestions { get; set; } = new List<QuestionVm>();
    }


}
