using System;

namespace QdratNew.Entities
{
    public class BatchInstructorGraduatedAccess
    {
        public int Id { get; set; }

        public int BatchId { get; set; }
        public Batch Batch { get; set; }

        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        public string GrantedByUserId { get; set; } = string.Empty;
        public ApplicationUser GrantedByUser { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
