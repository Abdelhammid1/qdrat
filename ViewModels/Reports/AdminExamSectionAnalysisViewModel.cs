namespace QdratNew.ViewModels.Reports
{
    public class AdminExamSectionAnalysisViewModel
    {
        
        public int ExamId { get; set; }           // ✅ لإرسال معرف الاختبار
        public int AssignmentId { get; set; }         
        public int ExaAssignmentIdmId { get; set; }         
        public int StudentId { get; set; }        // ✅ لإرسال معرف الطالب
        public string ExamTitle { get; set; }
        public string StudentName { get; set; }
        public List<SectionPerformanceEntry> SectionReports { get; set; } = new();
    }

    public class SectionPerformanceEntry
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Score { get; set; }
        public List<LessonPerformanceEntry> LessonBreakdown { get; set; } = new();
    }

    public class LessonPerformanceEntry
    {
        public int AssignmentId { get; set; }   // ✅ أضف هذا السطر

        public int LessonId { get; set; }           // ✅ لإرسال معرف المؤشر

        public string LessonTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Score { get; set; }
    }
}
