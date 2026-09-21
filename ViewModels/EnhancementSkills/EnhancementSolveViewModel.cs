using QdratNew.ViewModels.Question;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementSolveViewModel
    {
        public int SetId { get; set; }

        public Guid CurrentQuestionId { get; set; }

        public List<Guid> AllQuestionIds { get; set; } = new List<Guid>();

        public QuestionDisplayViewModel? Question { get; set; }

        public string? SelectedAnswer { get; set; }

        public Dictionary<Guid, string?> AnswersMap { get; set; } = new Dictionary<Guid, string?>();

        public List<Guid> ReviewMarkedIds { get; set; } = new List<Guid>();

        public bool IsSubmitted { get; set; }




        // اجعلهم read/write بدلاً من read-only
        public int CurrentIndex { get; set; }
        public int Total { get; set; }

        // خصائص المنهج
        public bool IsQuantitative { get; set; }   // أرقام هندية
        public bool IsRTL { get; set; } = true;    // اتجاه النص
    }
}
