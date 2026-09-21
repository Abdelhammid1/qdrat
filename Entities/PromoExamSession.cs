using System;
using System.Collections.Generic;

namespace QdratNew.Entities
{
    public class PromoExamSession
    {
        public int Id { get; set; }
        public string TempIdentifier { get; set; } // Cookie / Session key
        public int? CourseId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsCompleted { get; set; } = false;
        public bool IsLeadSubmitted { get; set; } = false;
        public ICollection<PromoExamAttempt> Attempts { get; set; } = new List<PromoExamAttempt>();
        public PromoExamResult Result { get; set; }
    }
}
