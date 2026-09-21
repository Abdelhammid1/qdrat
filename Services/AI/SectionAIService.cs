using QdratNew.Entities;
using QdratNew.ViewModels.Section;

namespace QdratNew.Services.AI
{
    public class SectionAIService : ISectionAIService
    {
        public async Task<SectionAIDashboardViewModel> AnalyzePerformancesAsync(List<StudentPerformance> performances, List<Section> sections)
        {
            var viewModel = new SectionAIDashboardViewModel
            {
                TotalSections = sections.Count,
                SectionAnalytics = new List<SectionAnalysisData>(),
                DifficultyDistribution = new List<PieChartData>(),
                SuccessRatesPerSection = new List<BarChartData>()
            };

            var difficultyCounter = new Dictionary<string, int>();

            foreach (var section in sections)
            {
                var sectionPerformances = performances
                    .Where(p => p.SectionId == section.Id)
                    .ToList();

                int count = sectionPerformances.Count;
                double avgScore = count > 0 ? sectionPerformances.Average(p => p.Score) : 0;
                double successRate = count > 0 ? sectionPerformances.Count(p => p.Score >= 60) * 100.0 / count : 0;
                double failureRate = 100 - successRate;

                string difficulty = successRate switch
                {
                    >= 80 => "سهل",
                    >= 50 => "متوسط",
                    _ => "صعب"
                };

                if (!difficultyCounter.ContainsKey(difficulty))
                    difficultyCounter[difficulty] = 0;
                difficultyCounter[difficulty]++;

                viewModel.SuccessRatesPerSection.Add(new BarChartData
                {
                    SectionTitle = section.Title,
                    SuccessRate = Math.Round(successRate, 1)
                });

                viewModel.SectionAnalytics.Add(new SectionAnalysisData
                {
                    SectionId = section.Id,
                    SectionTitle = section.Title,
                    CurriculumTitle = section.Curriculum?.Title ?? "غير معروف",
                    TotalQuestions = count,
                    TotalAttempts = count,
                    SuccessRate = Math.Round(successRate, 1),
                    FailureRate = Math.Round(failureRate, 1),
                    DifficultyLevel = difficulty
                });
            }

            viewModel.DifficultyDistribution = difficultyCounter.Select(kvp => new PieChartData
            {
                Label = kvp.Key,
                Count = kvp.Value
            }).ToList();

            return await Task.FromResult(viewModel);
        }
    }
}
