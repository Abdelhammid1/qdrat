using QdratNew.MLModels.StudentProgressModel;

namespace QdratNew.Services.Interfaces
{
    public interface IAIStudentProgressTrainingService
    {
        Task<List<AIStudentProgressTrainingData>> GetTrainingDataAsync();
    }
}
