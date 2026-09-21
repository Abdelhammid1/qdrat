using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class StudentWeaknessAnalyzerService : IStudentWeaknessAnalyzerService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentWeaknessAnalyzerService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<StudentWeaknessAnalysisResult> AnalyzeAsync(int studentId, int? curriculumId, int? sectionId)
        {
            using var db = _contextFactory.CreateDbContext();

            // جلب الأداء من StudentPerformance
            var performances = await db.StudentPerformances
                .AsNoTracking()
                .Where(sp => sp.StudentID == studentId &&
                             (!curriculumId.HasValue || sp.CurriculumId == curriculumId) &&
                             (!sectionId.HasValue || sp.SectionId == sectionId))
                .Select(sp => new { sp.SectionId, sp.Score, sp.CurriculumId })
                .ToListAsync();

            double overall = performances.Count > 0
                ? performances.Average(p => p.Score)
                : 50.0;

            // تحديد المستوى
            string level = overall >= 80 ? "Good"
                : overall >= 60 ? "Average"
                : overall >= 40 ? "Weak"
                : "VeryWeak";

            // المحاور الضعيفة
            var sectionIds = performances
                .Select(p => p.SectionId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var sectionTitles = await db.Sections
                .AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToListAsync();

            var titleDict = sectionTitles.ToDictionary(s => s.Id, s => s.Title ?? $"جزء {s.Id}");

            var weakSections = performances
                .Where(p => p.SectionId.HasValue && p.Score < 60)
                .GroupBy(p => p.SectionId!.Value)
                .Select(g => new WeakSectionSummary
                {
                    SectionId = g.Key,
                    SectionTitle = titleDict.GetValueOrDefault(g.Key, "جزء من المنهج"),
                    Score = Math.Round(g.Average(x => x.Score), 1),
                    SafeLabel = g.Average(x => x.Score) < 40
                        ? "يحتاج دعماً مكثفاً"
                        : "يحتاج متابعة إضافية"
                })
                .OrderBy(w => w.Score)
                .ToList();

            bool isImproving = false;
            var recentAttempts = await db.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.AttemptedAt)
                .Take(20)
                .Select(a => new { a.IsCorrect, a.AttemptedAt })
                .ToListAsync();

            if (recentAttempts.Count >= 6)
            {
                double recent = recentAttempts.Take(10).Count(a => a.IsCorrect) * 10.0;
                double older = recentAttempts.Skip(10).Count(a => a.IsCorrect) * 10.0;
                isImproving = recent > older + 5;
            }

            string summary = weakSections.Count == 0
                ? "أداء الطالب عام جيد."
                : $"يحتاج الطالب إلى تدريب إضافي في {weakSections.Count} جانب من المنهج.";

            return new StudentWeaknessAnalysisResult
            {
                StudentId = studentId,
                StudentLevel = level,
                OverallScore = Math.Round(overall, 1),
                SafeWeaknessSummary = summary,
                IsImproving = isImproving,
                WeakSections = weakSections
            };
        }
    }
}
