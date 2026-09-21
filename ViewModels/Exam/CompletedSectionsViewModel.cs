namespace QdratNew.ViewModels.Exam
{
    // ViewModels/Exam/CompletedSectionsViewModel.cs
public class CompletedSectionsViewModel
{
    public int BatchId { get; set; }
    public string BatchName { get; set; }
    public int SectionId { get; set; }
    public string SectionTitle { get; set; }
    public int CurriculumId { get; set; }
    public string CurriculumTitle { get; set; }
    public int CompletedLessonsCount { get; set; }
    public int TotalLessonsInSection { get; set; }
    public bool IsExamGenerated { get; set; }
    public int? LessonId { get; set; } // لأول درس مثلًا أو آخر درس
     public List<CompletedSectionItemViewModel> CompletedSections { get; set; } = new();


    }


    public class CompletedSectionItemViewModel
{
    public int SectionId { get; set; }
    public string SectionTitle { get; set; }
    public int CompletedLessonsCount { get; set; }
    public string? LastLectureTitle { get; set; }
    public DateTime? LastLectureDate { get; set; }

    }
}
