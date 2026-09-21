using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.ViewModels.Exam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Implementations
{
    public class StudentExamDashboardService : IStudentExamDashboardService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentExamDashboardService(
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ======================================================
        // 🟢 Dashboard Summary (Cards فقط)
        // ======================================================
        public async Task<StudentExamDashboardViewModel> GetDashboardAsync(int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var vm = new StudentExamDashboardViewModel();

            // القيم الأساسية تُستكمل داخل الكنترولر
            // هنا نُعيد VM فارغًا آمنًا
            return vm;
        }

        // ======================================================
        // 🟢 Curriculums (OLD CONTRACT)
        // ======================================================
        public async Task<List<SelectListItem>> GetStudentCurriculumsAsync(int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var batchId = await db.StudentBatchEnrollments
                .Where(x => x.StudentID == studentId)
                .Select(x => x.BatchId)
                .FirstOrDefaultAsync();

            if (batchId == 0)
                return new List<SelectListItem>();

            var data = await (
                from ea in db.ExamAssignmentsToBatches
                join c in db.Curriculums on ea.CurriculumId equals c.Id
                where ea.BatchId == batchId && ea.CurriculumId != null
                select new
                {
                    c.Id,
                    c.Title
                }
            )
            .Distinct()
            .OrderBy(x => x.Title)
            .ToListAsync();

            return data.Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Title
            }).ToList();
        }

        // ======================================================
        // 🟢 Charts Data (كما كان)
        // ======================================================
        public async Task<object> GetDashboardDataByCurriculumAsync(
            int studentId,
            int curriculumId)
        {
            using var db = _contextFactory.CreateDbContext();

            // هذه الدالة يُعاد محتواها JSON
            // والكنترولر يتعامل معها مباشرة
            // نُعيد كائنًا فارغًا آمنًا مؤقتًا

            return new
            {
                sectionLabels = new List<string>(),
                studentScores = new List<double>(),
                batchAverages = new List<double>(),

                examTitles = new List<string>(),
                studentExamScores = new List<double>(),
                batchExamScores = new List<double>(),

                correctAnswers = new List<int>(),
                remainingQuestions = new List<int>()
            };
        }
    }
}
