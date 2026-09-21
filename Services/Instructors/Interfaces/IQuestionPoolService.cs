namespace QdratNew.Services.Instructors.Interfaces
{
    public interface IQuestionPoolService
    {
        List<Guid> FilterUsedQuestions(List<Guid> questionIds, int studentId);
    }
}
