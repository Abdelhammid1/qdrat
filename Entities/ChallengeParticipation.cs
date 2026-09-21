using System;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class ChallengeParticipation
    {
        public int Id { get; set; }

        // ✅ ربط الطالب بالتحدي
        public int StudentId { get; set; }
        public required Student Student { get; set; }

        // ✅ ربط المشاركة بالتحدي
        public int ChallengeId { get; set; }
        public required Challenge Challenge { get; set; } // ✅ يجبر المطور على تعيين قيمة عند الإنشاء

        // ✅ درجة التحدي
        public float? Score { get; set; } // 👈 يمكن أن يكون `NULL` في حال لم يتم تصحيح التحدي بعد

        // ✅ تاريخ المشاركة
        public DateTime ParticipationDate { get; set; } = DateTime.UtcNow;

        // ✅ حالة التحدي (مكتمل أم لا)
        public bool IsCompleted { get; set; } = false;

        // ✅ تاريخ إكمال التحدي
        public DateTime? CompletedAt { get; set; }
    }
}
