namespace QdratNew.ViewModels.Homework
{
    public class HomeworkBatchDetailsPageVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseTitle { get; set; }
        public int TotalStudents { get; set; }
        public List<HomeworkCardForBatchVM> Homeworks { get; set; } = new();
        public List<BatchStudentCardVM> Students { get; set; } = new();
        public List<CurriculumInstructorVM> CurriculumInstructors { get; set; } = new();
    }

    public class CurriculumInstructorVM
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }
        public string InstructorName { get; set; }
    }

    public class BatchStudentCardVM
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public int AttendedCount { get; set; }
        public int TotalLectures { get; set; }
        public int SolvedHomeworks { get; set; }
        public int TotalHomeworks { get; set; }
        public List<StudentLectureVM> Lectures { get; set; } = new();
        public List<StudentHwVM> Homeworks { get; set; } = new();
    }

    public class StudentLectureVM
    {
        public string Title { get; set; }
        public string Date { get; set; }       // "yyyy/MM/dd"
        public string MonthLabel { get; set; } // "يناير 2025"
        public bool HasRecord { get; set; }
        public bool IsPresent { get; set; }
        public bool IsLate { get; set; }
    }

    public class StudentHwVM
    {
        public string Title { get; set; }
        public string Date { get; set; }       // "yyyy/MM/dd"
        public bool IsSolved { get; set; }
        public double? Score { get; set; }
        public bool StudentContacted { get; set; }
        public bool ParentContacted { get; set; }
        public int? CurriculumId { get; set; }
        public string? CurriculumTitle { get; set; }
    }

    public class HomeworkCardForBatchVM
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int TotalAssigned { get; set; }
        public int SubmittedCount { get; set; }
        public int SolvedPercentage { get; set; }
        public int Late24hCount { get; set; }
        public int Critical3daysCount { get; set; }
        public bool IsClosed { get; set; }
    }
}
