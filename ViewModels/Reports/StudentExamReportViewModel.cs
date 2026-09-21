using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Reports
{
    public class StudentExamReportViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public string CurriculumTitle { get; set; }
        public string InstructorName { get; set; }
        public string ExamTitle { get; set; }
        public DateTime ExamDate { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int Skipped { get; set; }
        public double OverallPercent { get; set; }
        public string ElapsedTimeFormatted { get; set; }

        public string SpeedAccuracyFeedback { get; set; }
        public string MotivationalMessage { get; set; }

        public List<SectionReportItem> Sections { get; set; } = new();

        // Existing fields (internal setters — preserved)
        public object ParentName { get; internal set; }
        public int Answered { get; internal set; }
        public double PercentTimeUsed { get; internal set; }
        public string TrackCard1 { get; internal set; }
        public string TrackCard1Desc { get; internal set; }
        public string TrackCard2 { get; internal set; }
        public string TrackCard2Desc { get; internal set; }
        public object Level { get; internal set; }
        public object ParentPhone { get; internal set; }
        public double TimeUsagePercent { get; set; }
        public string CheatingFlagMessage { get; set; }

        // Extended: lecture attendance (for heatmap + detail table)
        public List<LectureAttendanceItem> LectureAttendances { get; set; } = new();

        // Extended: homework submissions
        public List<HomeworkItem> Homeworks { get; set; } = new();
        public double HomeworkAverage { get; set; }
    }

    public class SectionReportItem
    {
        public string SectionTitle { get; set; }
        public int Total { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }
        public string Guidance { get; set; }
    }

    public class LectureAttendanceItem
    {
        public int LectureNumber { get; set; }
        public string LectureTitle { get; set; }
        public DateTime LectureDate { get; set; }
        /// <summary>"حاضر" | "متأخر" | "غائب"</summary>
        public string AttendanceStatus { get; set; }
        public int LateMinutes { get; set; }
        public int PermissionsCount { get; set; }
    }

    public class HomeworkItem
    {
        public int Number { get; set; }
        public string Title { get; set; }
        public bool IsSubmitted { get; set; }
        public double? Score { get; set; }
        public double MaxScore { get; set; }
    }
}
