using System.Text.Json.Serialization;

namespace QdratNew.ViewModels.OnlineLecture
{
    // OLC-S7.1 — عقد C.5 «نبضة» حرفيًا:
    // POST … ping — { lectureContentId, itemId?, state: "playing"|"paused"|"ended", position, duration }
    //   → { ok, completed, reason?: "insufficient"|"locked"|"state-conflict"|"notfound", message?, watchedSeconds,
    //       requiredSeconds, nextItemId?, lectureCompleted, nextLectureUnlocked }
    // أسماء JSON صريحة لأن التطبيق يعطّل camelCase عمومًا (PropertyNamingPolicy = null).
    // studentId لا يُقبل من الطلب أبدًا — يأتي من سياق الطالب في الخادم.

    public sealed class StudentLecturePingRequest
    {
        [JsonPropertyName("lectureContentId")] public int LectureContentId { get; set; }
        /// <summary>IndicatorVideos فقط: العنصر الجاري. FullVideo: null.</summary>
        [JsonPropertyName("itemId")] public int? ItemId { get; set; }
        [JsonPropertyName("state")] public string? State { get; set; }
        [JsonPropertyName("position")] public double Position { get; set; }
        [JsonPropertyName("duration")] public double Duration { get; set; }
    }

    public sealed class StudentLecturePingResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("completed")] public bool Completed { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("watchedSeconds")] public int WatchedSeconds { get; set; }
        [JsonPropertyName("requiredSeconds")] public int RequiredSeconds { get; set; }
        [JsonPropertyName("nextItemId")] public int? NextItemId { get; set; }
        [JsonPropertyName("lectureCompleted")] public bool LectureCompleted { get; set; }
        [JsonPropertyName("nextLectureUnlocked")] public bool NextLectureUnlocked { get; set; }
    }
}
