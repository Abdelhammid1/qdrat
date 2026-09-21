using Microsoft.AspNetCore.Http;
using QdratNew.ViewModels.Remedial;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IRemedialTrackingService
    {
        Task CacheVideoProgressAsync(HttpContext http, int studentId, int sessionId, int videoId, int secondsWatched);
        Task CacheQuizAttemptAsync(HttpContext http, int studentId, int quizId, bool isCorrect);
        Task FlushCachedProgressAsync(HttpContext http, int studentId, int sessionId);

        Task<RemedialSessionTrackingSummaryVm> GetSessionTrackingSummaryAsync(int sessionId);
        Task LogVideoWatchAsync(int sessionId, int videoId, int secondsWatched);
        Task SaveQuizAttemptAsync(int studentId, int videoId, int quizId, bool isCorrect);

    }
}
