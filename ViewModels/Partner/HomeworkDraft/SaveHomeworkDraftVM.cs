using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class SaveHomeworkDraftVM
    {
        public string Title { get; set; }
        public int CurriculumId { get; set; }
        public List<Guid> QuestionIds { get; set; } = new();
    }
}
