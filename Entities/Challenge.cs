using System;
using System.Collections.Generic;
using QdratNew.Entities; // ✅ لإضافة العلاقة مع `Course`

namespace QdratNew.Entities
{
    public class Challenge
    {
        public int Id { get; set; }
        public required string Title { get; set; } // 🔹 اسم التحدي
        public required string Description { get; set; } // 🔹 وصف التحدي
        public required string Level { get; set; } // 🔹 مستوى التحدي (مبتدئ، متوسط، متقدم)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 🔹 تاريخ الإنشاء
        public TimeSpan Duration { get; set; } // 🔹 مدة التحدي
        public string Status { get; set; } = "نشط"; // 🔹 حالة التحدي (نشط، منتهي)

        // ✅ علاقة `One-to-Many` مع `ChallengeParticipation`
        public ICollection<ChallengeParticipation> Participants { get; set; } = new HashSet<ChallengeParticipation>();

        // ✅ ربط التحدي بدورة تدريبية
        public int CourseId { get; set; } // 🔹 معرف الدورة
        public required Course Course { get; set; } // 🔹 الدورة المرتبطة بالتحدي
    }
}
