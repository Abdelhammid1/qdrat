using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Analytics.Interfaces
{
    public interface IWeakPointEngine
    {
        Task<List<WeakPointVM>> GetWeakQuestionsAsync(WeakPointFilter filter);

        Task<List<WeakLessonVM>> GetWeakLessonsAsync(WeakPointFilter filter);

    }
}