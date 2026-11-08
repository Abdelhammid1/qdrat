using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>
    /// OLC-S1 (العقد C.2 — جدول LectureContentProgress): تقدّم الطالب على المحتوى كله (للوضع FullVideo).
    /// WatchedSeconds بزمن الخادم (نبضات) — نفس خوارزمية RemedialTrackProgressService.ComputePing.
    /// </summary>
    public class LectureContentProgress
    {
        public int Id { get; set; }

        public int LectureContentId { get; set; }
        public LectureContent LectureContent { get; set; } = null!;

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        /// <summary>رصيد المشاهدة بزمن الخادم.</summary>
        public double WatchedSeconds { get; set; }
        /// <summary>أبعد موضع موثَّق — نقطة الاستئناف.</summary>
        public double MaxPositionSeconds { get; set; }
        public int? DurationSeconds { get; set; }
        /// <summary>playing | paused | ended</summary>
        [MaxLength(10)] public string? LastPingState { get; set; }
        public DateTime? FirstPingAtUtc { get; set; }
        public DateTime? LastPingAtUtc { get; set; }
        public bool EndedSeen { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
