namespace QdratNew.ViewModels.Students
{
    public class StudentProfileViewModel
    {
        public string FullName { get; set; }
        public string ProfileImagePath { get; set; }
        public string Level { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string LastLoginAgo { get; set; }
        public bool IsRegular { get; set; }

        public int SolvedCount { get; set; }
        public int WrongCount { get; set; }
        public int SuccessRate { get; set; }

        public string AIMessage { get; set; }

        public List<StudentActivityViewModel> LatestActivities { get; set; } = new();
        public List<AchievementViewModel> Achievements { get; set; } = new();
        public List<SuggestedLessonViewModel> SuggestedLessons { get; set; } = new();
        public List<AssignedExamViewModel> AssignedExams { get; set; } = new();
        public StudentSidebarAnalyticsViewModel SidebarAnalytics { get; set; } = new();
    }

    public class StudentActivityViewModel
    {
        public StudentActivityViewModel(string title, string description, string timeAgo, string icon)
        {
            Title = title;
            Description = description;
            TimeAgo = timeAgo;
            Icon = icon;
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public string TimeAgo { get; set; }
        public string Icon { get; set; }
        public DateTime? Time { get; set; }
    }

    public class AchievementViewModel
    {
        public AchievementViewModel(string title, string description, string icon)
        {
            Title = title;
            Description = description;
            Icon = icon;
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }

    public class SuggestedLessonViewModel
    {
        public string LessonTitle { get; set; }
        public string Description { get; set; }
        public string VideoPath { get; set; }
        public string Reason { get; set; } // ✅ هذه السطر الجديد
    
        public List<VideoResourceViewModel> Videos { get; set; } = new();

        public string Url => !string.IsNullOrEmpty(VideoPath)
            ? VideoPath
            : Videos.FirstOrDefault()?.VideoUrl ?? "";
    }

    public class VideoResourceViewModel
    {
        public string Title { get; set; }
        public string VideoUrl { get; set; }
        public string Description { get; set; }
        public string SmartMessage { get; set; }

        public string Url => VideoUrl;
    }
}
