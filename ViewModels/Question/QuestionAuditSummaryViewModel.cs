using QdratNew.Entities;

namespace QdratNew.ViewModels.Question
{
    public class QuestionAuditSummaryViewModel
    {
        public Guid QuestionId { get; set; }
        public string ModalId { get; set; } = "auditModal";

        public QuestionAuditLog? LatestAudit { get; set; }

    }

}