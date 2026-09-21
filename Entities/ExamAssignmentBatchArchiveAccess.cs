using System;

namespace QdratNew.Entities
{
    public class ExamAssignmentBatchArchiveAccess
    {
        public int Id { get; set; }

        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; }

        public string GrantedByUserId { get; set; } = string.Empty;
        public ApplicationUser GrantedByUser { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
