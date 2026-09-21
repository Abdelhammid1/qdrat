using QdratNew.Entities;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Admin.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalInstructors { get; set; }
        public int TotalStudents { get; set; }

        public int ActiveBatches { get; set; }
        public int SentHomeworks { get; set; }
        public int UnsolvedHomeworks { get; set; }
        public int StudentsDidNotSolveLastHomework { get; set; }
        public int StudentsLateTwoHomeworks { get; set; }

        public int TotalRemedialPlans { get; set; }
        public int StudentsWithPlansStarted { get; set; }
        public int StudentsWithPlansNotResponded { get; set; }

        public int TotalExams { get; set; }
        public int TotalQuestions { get; set; }
        public List<string> BatchesMissingLessonsNotifications { get; set; } = new();

        public List<QuestionBankStatsViewModel> QuestionBankStats { get; set; } = new();

        public List<QuestionBankByCurriculumViewModel> QuestionBankStatsByCurriculum { get; set; } = new();

        public List<Notification> AdminNotifications { get; set; }

        public List<AdminActivityLog> RecentAdminActivities { get; set; } = new();
    }

    public class QuestionBankStatsViewModel
    {
        public string SectionTitle { get; set; }
        public int LessonsCount { get; set; }
        public int QuestionsCount { get; set; }
        public int SectionId { get; set; }

    }

    public class QuestionBankByCurriculumViewModel
    {
        public string CurriculumTitle { get; set; }
        public List<QuestionBankStatsViewModel> Sections { get; set; } = new();
    }
}
