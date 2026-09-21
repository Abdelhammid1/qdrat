using QdratNew.DTOs;
using QdratNew.Entities;
using QdratNew.Helpers;
using QdratNew.ViewModels.Batch;

namespace QdratNew.Services.AI
{
    public static class AIBatchAnalyzer
    {
        // Overload يقبل BatchProjection
        public static List<BatchAnalysisViewModel> AnalyzeAll(
            List<BatchProjection> batches,
            List<StudentBatchEnrollment> enrollments,
            List<StudentPerformance> performances)
        {
            var result = new List<BatchAnalysisViewModel>();
            if (batches == null || enrollments == null || performances == null) return result;

            var perfByStudent = performances
                .GroupBy(p => p.StudentID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var studentIdsByBatch = enrollments
                .GroupBy(e => e.BatchId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.StudentID).ToList());

            foreach (var b in batches)
            {
                var studentIds = studentIdsByBatch.TryGetValue(b.Id, out var list) ? list : new List<int>();

                var batchPerformances = new List<StudentPerformance>(studentIds.Count * 2);
                foreach (var sid in studentIds)
                    if (perfByStudent.TryGetValue(sid, out var ps))
                        batchPerformances.AddRange(ps);

                double avg = 0, pass = 0;
                if (batchPerformances.Count > 0)
                {
                    avg = batchPerformances.Average(p => p.Score);
                    var passed = batchPerformances.Count(p => p.Score >= 50);
                    pass = passed * 100.0 / batchPerformances.Count;
                }

                result.Add(new BatchAnalysisViewModel
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    CourseName = b.CourseName,
                    StudentCount = studentIds.Count,
                    AverageScore = Math.Round(avg, 1),
                    PassRate = Math.Round(pass, 1),
                    PerformanceLevel = GetPerformanceLevel(avg),
                    Gender = b.Gender.GetDisplayName(),
                    BranchName = b.BranchName ?? "غير محدد"
                });
            }

            return result;
        }

        // (لو محتاج) النسخة القديمة للكيانات تقدر تسيبها كما هي
        public static List<BatchAnalysisViewModel> AnalyzeAll(
            List<Batch> batches,
            List<StudentBatchEnrollment> enrollments,
            List<StudentPerformance> performances)
        {
            // ... (نسختك الحالية)
            throw new NotImplementedException();
        }

        private static string GetPerformanceLevel(double averageScore)
        {
            if (averageScore >= 85) return "ممتاز";
            if (averageScore >= 70) return "جيد جدًا";
            if (averageScore >= 50) return "جيد";
            return "ضعيف";
        }
    }
}
