using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentPracticeService
    {
        // 🟢 تدريب عام على المحاور الضعيفة
        Task<List<PracticeQuestionVm>> GetWeaknessPracticeAsync(int studentId, int count = 10);

        // 🟢 تدريب على الأخطاء السابقة
        Task<List<PracticeQuestionVm>> GetMistakePracticeAsync(int studentId, int count = 10);

        // 🟢 تدريب على محور محدد
        Task<List<PracticeQuestionVm>> GetWeaknessPracticeForSectionAsync(int studentId, int sectionId, int count = 45);
    }
}
