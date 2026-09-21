using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QdratNew.AI.MLModels.Branches;
using QdratNew.Entities;

namespace QdratNew.AI.Helpers
{
    public static class TimeSeriesDataHelper
    {
        // 🟦 البيانات الزمنية لعدد الطلاب شهريًا
        public static List<ChartTimeSeriesData> PrepareMonthlyStudentCounts(List<Branch> branches)
        {
            var allDates = new List<DateTime>();
            var allValues = new List<float>();

            foreach (var b in branches)
            {
                foreach (var p in b.Students.SelectMany(s => s.StudentPerformances))
                {
                    allDates.Add(p.ExamDate);
                    allValues.Add(1); // كل أداء طالب = طالب واحد
                }
            }

            return ExtractMonthlyData(allDates, allValues);
        }

        // 🟨 البيانات الزمنية لمتوسط الأداء شهريًا
        public static List<ChartTimeSeriesData> PrepareMonthlyAveragePerformance(List<Branch> branches)
        {
            var performances = branches.SelectMany(b => b.Students)
                                       .SelectMany(s => s.StudentPerformances)
                                       .Where(p => p.Score > 0)
                                       .ToList();

            return ExtractMonthlyData(performances, p => p.ExamDate, p => (float)p.Score);
        }

        // 🟩 البيانات الزمنية لعدد الدورات شهريًا
        public static List<ChartTimeSeriesData> PrepareMonthlyCourseCounts(List<Branch> branches)
        {
            var courses = branches.SelectMany(b => b.Courses).ToList();

            return ExtractMonthlyData(courses, c => c.StartDate, c => 1f); // كل كورس = 1
        }

        // 🔁 دالة مساعدة لتجميع البيانات شهريًا
        private static List<ChartTimeSeriesData> ExtractMonthlyData<T>(IEnumerable<T> items, Func<T, DateTime> dateSelector, Func<T, float> valueSelector)
        {
            return items
                .GroupBy(x => dateSelector(x).ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .Select(g => new ChartTimeSeriesData
                {
                    Date = g.Key,
                    Value = g.Average(valueSelector)
                })
                .ToList();
        }

        private static List<ChartTimeSeriesData> ExtractMonthlyData(List<DateTime> dates, List<float> values)
        {
            var combined = dates.Zip(values, (d, v) => new { Date = d, Value = v });

            return combined
                .GroupBy(x => x.Date.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .Select(g => new ChartTimeSeriesData
                {
                    Date = g.Key,
                    Value = g.Sum(x => x.Value) // عدد الطلاب
                })
                .ToList();
        }
    }
}
