namespace QdratNew.ViewModels.Students
{
    public class StudentHomeViewModel
    {
        public string FullName { get; set; }
        public string Level { get; set; }
        public string LastVisitDate { get; set; }
        public string LastSessionDuration { get; set; }
        public string LastActionSummary { get; set; }
        public string ProfileImagePath { get; set; }

        public int PendingHomeworkCount { get; set; }
        public int UpcomingExamCount { get; set; }
        public int UpcomingRemedialSessions { get; set; }

        public List<string> Notifications { get; set; }

        public int SolvedQuestions { get; set; }
        public int WrongQuestions { get; set; }
        public int SuccessRate { get; set; }

        public string CurrentRemedialPlanTitle { get; set; }
        public string RemedialPlanPerformanceLevel { get; set; }
        public string RemedialPlanPeriod { get; set; }

        public string LastRemedialInteraction { get; set; }
        public string LastRemedialComment { get; set; }


    }
}
