using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>
    /// يسجّل بداية كل جلسة حل واجب + آخر نشاط + معرّف التبويب (لمنع التعدد)
    /// </summary>
    public class HomeworkSessionLog
    {
        [Key]
        public int Id { get; set; }

        public int HomeworkSetId { get; set; }
        public HomeworkSet HomeworkSet { get; set; } = null!;

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public DateTime SessionStartedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        /// <summary>معرّف فريد للتبويب — يُستخدم لمنع فتح الواجب في أكثر من تبويب</summary>
        [MaxLength(64)]
        public string TabId { get; set; } = string.Empty;

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        /// <summary>هل هذا التبويب هو التبويب النشط حالياً؟</summary>
        public bool IsActive { get; set; } = true;
    }
}
