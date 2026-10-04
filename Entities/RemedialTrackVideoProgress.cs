using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>RTK: تقدّم (محور-طالب × فيديو × جولة). WatchedSeconds بزمن الخادم (D4).</summary>
    public class RemedialTrackVideoProgress
    {
        public int Id { get; set; }
        public int AxisProgressId { get; set; }
        public RemedialTrackAxisProgress? AxisProgress { get; set; }
        public int VideoId { get; set; }
        public RemedialTrackVideo? Video { get; set; }
        public int VideoOrder { get; set; }
        public int Round { get; set; }

        public double WatchedSeconds { get; set; }           // D4: بزمن الخادم
        public int? DurationSeconds { get; set; }
        public bool EndedSeen { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? FirstPingAtUtc { get; set; }
        public DateTime? LastPingAtUtc { get; set; }
        [MaxLength(10)] public string? LastPingState { get; set; }   // playing|paused|ended — الرصيد الزمني يُمنح فقط إن كانت الحالة السابقة playing
        public DateTime? CompletedAtUtc { get; set; }
    }
}
