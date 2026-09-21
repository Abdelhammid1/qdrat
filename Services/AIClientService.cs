using System.Net.Http.Json;
using QdratNew.ViewModels.AI;

namespace QdratNew.Services.AI
{
    public class AIClientService : IAIClientService
    {
        private readonly HttpClient _http;

        public AIClientService(HttpClient http)
        {
            _http = http;
            _http.BaseAddress = new Uri("http://localhost:5151"); // عنوان خدمة الذكاء الاصطناعي
        }

        public async Task<string> AnalyzeStudentAsync(AIPerformanceRequestVM model)
        {
            var response = await _http.PostAsJsonAsync("/api/performance/analyze", model);
            var result = await response.Content.ReadFromJsonAsync<AIResponseVM>();

            return result?.Analysis ?? "لا يوجد تحليل حالياً.";
        }
    }
}
