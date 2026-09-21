using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class HomeworkArchiveAccess
    {
        public int Id { get; set; }

        public int HomeworkSetId { get; set; }
        public HomeworkSet HomeworkSet { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; }

        [Required]
        public string GrantedByUserId { get; set; } = string.Empty;
        public ApplicationUser GrantedByUser { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
