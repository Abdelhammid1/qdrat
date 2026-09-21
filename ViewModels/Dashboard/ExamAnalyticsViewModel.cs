namespace QdratNew.ViewModels.Dashboard
{
    public class ExamAnalyticsViewModel
    {
        // 🧠 معلومات عامة
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string BatchName { get; set; }
        public string InstructorName { get; set; }
        public string CurriculumTitle { get; set; }
        public DateTime CreatedAt { get; set; }

        // 📊 مؤشرات الأداء
        public int TotalStudents { get; set; }
        public int PassedStudents { get; set; }
        public int FailedStudents { get; set; }
        public int AbsentStudents { get; set; }
        public double SuccessRate { get; set; }

        // 🔍 تحليل تفصيلي لكل محور
        public List<SectionPerformanceItem> Sections { get; set; } = new();

        // 🔥 الطلاب الذين يحتاجون خطة علاجية
        public List<StudentWeaknessItem> AtRiskStudents { get; set; } = new();
    }

    public class StudentWeaknessItem
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public List<string> WeakSections { get; set; } = new();
    }
}
