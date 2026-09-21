using QdratNew.Entities;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Promo
{
    public class PromoExamSolveViewModel
    {
        public int SessionId { get; set; }
        public Guid CurrentQuestionId { get; set; }
        public List<Guid> AllQuestionIds { get; set; } = new();
        public int CurrentIndex { get; set; }
        public int Total => AllQuestionIds?.Count ?? 0;
        public QdratNew.Entities.Question Question { get; set; }

        public string? SelectedAnswer { get; set; }
    }
}
