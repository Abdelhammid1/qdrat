using System.Collections.Generic;
using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels
{
    public class StudentHomeworkSolveViewModel
    {
        public int HomeworkSetId { get; set; }

        // قائمة بالأسئلة المرتبطة بالواجب
        public List<HomeworkQuestionItemViewModel> Questions { get; set; } = new();
    }

    public class HomeworkQuestionItemViewModel
    {
        public Guid HomeworkId { get; set; }               // ✅ ID الواجب
        public QuestionDisplayViewModel Question { get; set; } = new();  // ✅ البيانات اللازمة للعرض من البارشال
    }
}
