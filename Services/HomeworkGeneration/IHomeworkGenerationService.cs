using QdratNew.Entities;
using QdratNew.Services.Homework.Models;

namespace QdratNew.Services.HomeworkGeneration
{
    public interface IHomeworkGenerationService
    {
        Task<HomeworkGenerationResult> GenerateAsync(
            HomeworkGenerationContext context);
    }
}
