using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Lecture
{
    public class LectureBatchesIndexViewModel
    {
        public int TotalBatches { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int UpcomingLectures { get; set; }

        public List<LectureBatchCardViewModel> Batches { get; set; } = new();
    }

    public class LectureBatchCardViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;

        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int UpcomingLectures { get; set; }

        public string InstructorSummary { get; set; } = string.Empty;
        public DateTime? LastLectureDate { get; set; }
        public DateTime? NextLectureDate { get; set; }
    }

    public class BatchLecturesPageViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;

        public int TotalStudents { get; set; }
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int UpcomingLectures { get; set; }

        public List<LectureListViewModel> Lectures { get; set; } = new();
    }
}