using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using System.Collections.Generic;

namespace QdratNew.Services
{
    public class BranchAnalysisService
    {
        private readonly ApplicationDbContext _context;

        public BranchAnalysisService(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<BranchPerformanceData> GetBranchPerformance()
        {
            var branches = _context.Branches
                .Select(branch => new BranchPerformanceData
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    StudentCount = _context.Students.Count(s => s.BranchId == branch.Id),
                    CourseCount = _context.Courses.Count(c => c.BranchId == branch.Id),
                    AverageSuccessRate = _context.StudentPerformances
                        .Where(sp => sp.Student.BranchId == branch.Id)
                        .Select(sp => (double?)sp.Score)
                        .Average() ?? 0,
                    EnrollmentTrend = _context.Students
                        .Where(s => s.BranchId == branch.Id)
                        .GroupBy(s => s.RegistrationDate.Year)
                        .Select(g => new { Year = g.Key, Count = g.Count() })
                        .OrderByDescending(g => g.Year)
                        .Take(5) // تحليل بيانات آخر 5 سنوات
                        .ToDictionary(g => g.Year, g => g.Count)
                })
                .ToList();

            return branches;
        }
    }

    public class BranchPerformanceData
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int StudentCount { get; set; }
        public int CourseCount { get; set; }
        public double AverageSuccessRate { get; set; }
        public Dictionary<int, int> EnrollmentTrend { get; set; } // تسجيل الطلاب حسب السنة
    }
}
