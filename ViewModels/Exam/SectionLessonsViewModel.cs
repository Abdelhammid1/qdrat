namespace QdratNew.ViewModels.Exam
{
    public class SectionLessonsViewModel
    {
        public int? StudentId { get; set; }
        public int HomeworkSetId { get; set; }   // أضف هذا السطر

        public int AssignmentId { get; set; }
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public List<LessonPerformanceVm> Lessons { get; set; } = new();
    }

    public class LessonPerformanceVm
    {
        public string LessonTitle { get; set; }
        public double SuccessRate { get; set; }       // ✅ مضاف
        public double ExamSuccessRate { get; set; }   // ✅ مضاف
  
        public int QuestionCount { get; set; }
        public double AvgSuccessRate { get; set; }


        public int TotalQuestions { get; set; }

  

        // ✅ آخر نشاط أو محاولة
        public DateTime LastActivityDate { get; set; }

        public int LessonId { get; set; }
 
        public double AvgTimeSeconds { get; set; }
        public double HardnessIndex { get; set; } // مؤشر الصعوبة

  
        public string LessonName { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double Percent { get; set; }

   

    }
}
