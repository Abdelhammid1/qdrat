using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>
    /// يسجّل حالات التلاعب الزمني المشبوهة لكل سؤال
    /// </summary>
    public class HomeworkTimeTamperLog
    {
        [Key]
        public int Id { get; set; }

        public int HomeworkSetId { get; set; }
        public int StudentId { get; set; }
        public Guid QuestionId { get; set; }

        /// <summary>الوقت الذي أرسله الـ Client (بالثواني)</summary>
        public double ClientTimeSecs { get; set; }

        /// <summary>الحد الأقصى المسموح به بناءً على وقت الجلسة (بالثواني)</summary>
        public double MaxAllowedSecs { get; set; }

        /// <summary>الفارق بين الوقتين</summary>
        public double DiscrepancySecs { get; set; }

        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(256)]
        public string? Note { get; set; }
    }
}
