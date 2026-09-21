using QdratNew.Entities;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Project
{
    public class ProjectsDashboardViewModel
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int UpcomingProjects { get; set; }

        public Dictionary<string, int> ProjectsPerBranch { get; set; } = new Dictionary<string, int>();
        public List<QdratNew.Entities.Project> RecentProjects { get; set; } = new List<QdratNew.Entities.Project>();

        // 🆕 إضافات جديدة لدعم الشارتات الذكية
        public List<ExecutionDurationViewModel> ExecutionDurations { get; set; } = new List<ExecutionDurationViewModel>(); // متوسط مدة التنفيذ لكل مشروع
        public List<string> UpcomingProjectNames { get; set; } = new List<string>(); // أسماء البرامج القادمة خلال 30 يوم

        public int ProjectsOnTime { get; set; }   // ✅ البرامج التي أنهت قبل أو في الموعد المتوقع
        public int ProjectsOverdue { get; set; }  // ✅ البرامج التي تجاوزت الموعد المتوقع


        // ✅ الإضافات الجديدة المطلوبة عشان الأخطاء تختفي
        public double AverageExecutionDays { get; set; }  // متوسط مدة التنفيذ لكل البرامج
        public double SuccessRate { get; set; }           // نسبة نجاح البرامج
        public int UpcomingProjectsIn30Days { get; set; } // عدد البرامج القادمة خلال 30 يوم


        // ✅ إضافات جديدة للشارتات
        public Dictionary<string, int> ProjectsPerStatus { get; set; } = new(); // نشط/مكتمل
        public Dictionary<string, double> ExecutionDurationPerProject { get; set; } = new(); // مدة التنفيذ لكل مشروع
        public Dictionary<string, int> ProjectsAddedPerMonth { get; set; } = new(); // عدد المشاريع كل شهر
        public Dictionary<string, Dictionary<string, int>> ProjectsPerBranchAndStatus { get; set; } = new(); // فرع × حالة
        public Dictionary<string, int> ProjectsPerType { get; set; } = new(); // نوع المشروع (لو في المستقبل)


        // 🎯 إضافات جديدة:
        public Dictionary<string, int> CoursesPerProject { get; set; } = new();  // ✅ عدد الدورات لكل مشروع
        public Dictionary<string, Dictionary<string, int>> StudentsPerCourseInProjects { get; set; } = new(); // ✅ طلاب كل دورة داخل مشروع
        public Dictionary<string, double> CourseCompletionRatePerProject { get; set; } = new(); // ✅ نسبة إكمال الدورات لكل مشروع
        public Dictionary<string, int> StudentsPerProject { get; set; } = new(); // ✅ طلاب لكل مشروع
        public Dictionary<string, int> StudentsRegistrationPerMonth { get; set; } = new(); // ✅ تطور تسجيل الطلاب شهريًا
   

}

    // ✅ موديل صغير لتمثيل مدة التنفيذ لكل مشروع
    public class ExecutionDurationViewModel
    {
        public string Name { get; set; }
        public double DurationDays { get; set; }
    }
}
