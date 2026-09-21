namespace QdratNew.Services.Interfaces
{
    public interface IStudentActivityLogger
    {
        Task LogAsync(int studentId,
                      string activityType,
                      string activityTitle,
                      string source,
                      bool? wasCorrect = null,
                      int? score = null,
                      string? note = null,
                      int? lessonId = null,
                      int? sectionId = null);
    }

}
