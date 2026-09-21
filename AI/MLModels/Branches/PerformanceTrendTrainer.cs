using Microsoft.ML;
using Microsoft.ML.Data;
using QdratNew.AI.MLModels.Branches;
using QdratNew.ViewModels.Branches;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QdratNew.AI.MLModels.Branches
{
    public static class PerformanceTrendTrainer
    {
        public static string ModelPath => Path.Combine("MLModels", "Branches", "PerformanceTrend", "performance_trend_model.zip");

        public static void Train(List<BranchAIReport> reports)
        {
            // ⬅️ تجهيز بيانات متوسط الأداء حسب كل شهر
            var data = reports
                .GroupBy(r => r.EstablishedDate.ToString("yyyy-MM"))
                .Select(g => new ChartTimeSeriesData
                {
                    Date = g.Key,
                    Value = g.Average(r => (float)r.AveragePerformance)
                })
                .OrderBy(d => DateTime.ParseExact(d.Date, "yyyy-MM", CultureInfo.InvariantCulture))
                .ToList();

            if (data.Count < 6)
            {
                Console.WriteLine("⚠️ بيانات الاتجاه الزمني لمتوسط الأداء غير كافية للتدريب.");
                return;
            }

            TimeSeriesChartTrainer.TrainAndSave(ModelPath, data);
        }
    }
}
