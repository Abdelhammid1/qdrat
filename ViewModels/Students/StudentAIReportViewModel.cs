using QdratNew.ViewModels;
using QdratNew.Entities;
using QdratNew.Services.AI;

namespace QdratNew.ViewModels.Students
{
    public class StudentAIReportViewModel
    {
        public int StudentID { get; set; }
        public string StudentName { get; set; }
        public int SectionId { get; set; }                    // ✅ معرف المحور

        public double AverageScore { get; set; }
        public double SuccessRate { get; set; }

        public List<SectionPerformanceSummary> SectionSummaries { get; set; }
        public List<string> AIRecommendations { get; set; }
        public DateTime? LastCommentTime { get; set; } // لتحديد آخر وقت تعليق

        public List<string> Recommendations { get; set; } = new List<string>(); // ✅ هذه السطر مهم

        // ✅ أضف هذه الخاصية لو مش موجودة

        public List<RemedialRecommendation> RemedialRecommendations { get; set; } = new();
        public List<RemedialSessionViewModel> RemedialSessions { get; set; }

        public int TotalSessions { get; set; }
        public int AttendedSessions { get; set; }
        public int RemainingSessions => TotalSessions - AttendedSessions;
        public DateTime? LastAttendanceDate { get; set; }

        public double ClassAverageScore { get; set; } // متوسط زملائه
        public int Percentile { get; set; } // أفضل من كم %
        public List<PerformanceTimelineItem> PerformanceTimeline { get; set; }

        public RemedialSession? NextSession { get; set; }
        public int TotalLectures { get; set; }
        public int AttendedLectures { get; set; }
        public string? LastLectureTitle { get; set; }
        public DateTime? LastLectureDate { get; set; }
        public int MissedLectures => TotalLectures - AttendedLectures;
       public double AttendanceRate => TotalLectures > 0
            ? Math.Round((AttendedLectures * 100.0) / TotalLectures, 1)
            : 0;



    }

    public class PerformanceTimelineItem
    {
        public string DateLabel { get; set; }
        public double Score { get; set; }
    }



}
