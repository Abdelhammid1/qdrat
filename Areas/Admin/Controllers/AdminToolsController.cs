using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.AI.MLModels.Branches;
using QdratNew.AI.MLModels.Charts;
using QdratNew.AI.Trainers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.MLModels;
using QdratNew.Services.AI;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Branches;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer")]
    public class AdminToolsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StudentPerformanceTrainer _studentTrainer;
        private readonly IWebHostEnvironment _env;
        private readonly IExamResultEngine _examResultEngine;
        private readonly IAdminActivityLogger _activityLogger;

        public AdminToolsController(
            ApplicationDbContext context,
            StudentPerformanceTrainer studentTrainer,
            IWebHostEnvironment env,
            IExamResultEngine examResultEngine,
            IAdminActivityLogger activityLogger)
        {
            _context = context;
            _studentTrainer = studentTrainer;
            _env = env;
            _examResultEngine = examResultEngine;
            _activityLogger = activityLogger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ================================
        // ✅ الأكشنات التقليدية (Redirect)
        // ================================

        [HttpGet]
        public IActionResult TrainTimeSeriesModels()
        {
            if (!_env.IsDevelopment())
                return Unauthorized("❌ هذا الزر مفعل فقط في بيئة التطوير.");

            var studentData = new List<ChartTimeSeriesData>();
            var performanceData = new List<ChartTimeSeriesData>();
            var courseData = new List<ChartTimeSeriesData>();

            for (int i = 0; i < 12; i++)
            {
                var month = DateTime.Now.AddMonths(-11 + i).ToString("yyyy-MM");
                studentData.Add(new ChartTimeSeriesData { Date = month, Value = 200 + i * 10 });
                performanceData.Add(new ChartTimeSeriesData { Date = month, Value = 60 + (i % 3) * 5 });
                courseData.Add(new ChartTimeSeriesData { Date = month, Value = 20 + (i % 4) * 3 });
            }

            TimeSeriesChartTrainer.TrainAndSave("MLModels/Branches/StudentsTrend/StudentCountModel.zip", studentData);
            TimeSeriesChartTrainer.TrainAndSave("MLModels/Branches/PerformanceTrend/PerformanceModel.zip", performanceData);
            TimeSeriesChartTrainer.TrainAndSave("MLModels/Branches/CoursesTrend/CourseCountModel.zip", courseData);

            TempData["SuccessMessage"] = "✅ تم توليد نماذج الاتجاه الزمني بالبيانات التجريبية بنجاح.";
            return RedirectToAction("Index", "Branches");
        }

        [HttpGet]
        public IActionResult TrainChartModel()
        {
            ChartModelTrainer.TrainAndSave();
            TempData["SuccessMessage"] = "✅ تم تدريب نموذج الرسوم البيانية (Chart AI) بنجاح.";
            return RedirectToAction("Index", "Branches");
        }

        [HttpGet]
        public IActionResult TrainStudentChart()
        {
            var sampleData = new List<float> { 50, 62, 71, 90, 105, 120, 135 };
            StudentCountTrainer.TrainAndSaveModel(sampleData);

            TempData["SuccessMessage"] = "✅ تم تدريب نموذج عدد الطلاب وحفظه بنجاح.";
            return RedirectToAction("Index", "Branches");
        }

        [HttpGet]
        public IActionResult TrainTrendsModels()
        {
            var reports = _context.Branches
                .Include(b => b.Students).ThenInclude(s => s.StudentPerformances)
                .Include(b => b.Courses)
                .Include(b => b.Projects)
                .Select(b => new BranchAIReport
                {
                    BranchId = b.Id,
                    BranchName = b.Name,
                    City = b.City,
                    IsPartner = b.IsPartner,
                    EstablishedDate = b.EstablishedDate,
                    TotalStudents = b.Students.Count,
                    TotalCourses = b.Courses.Count,
                    TotalProjects = b.Projects.Count,
                    AveragePerformance = b.Students.Any()
                        ? b.Students.SelectMany(s => s.StudentPerformances).DefaultIfEmpty().Average(p => p != null ? p.Score : 0)
                        : 0
                }).ToList();

            StudentsTrendTrainer.Train(reports);
            PerformanceTrendTrainer.Train(reports);
            CoursesTrendTrainer.Train(reports);

            TempData["SuccessMessage"] = "✅ تم تدريب جميع نماذج الاتجاه الزمني بنجاح.";
            return RedirectToAction("Index", "Branches");
        }

        [HttpGet]
        public IActionResult TrainBranchModel()
        {
            BranchMLTrainer.TrainAndSaveModel(_context);
            TempData["SuccessMessage"] = "✅ تم تدريب نموذج الفروع بنجاح.";
            return RedirectToAction("Index", "Branches");
        }

        [HttpGet]
        public IActionResult TrainAllModels()
        {
            BranchMLTrainer.TrainAndSaveModel(_context);
            _studentTrainer.TrainAndSaveModel(_context);
            ChartModelTrainer.TrainAndSave();

            TempData["SuccessMessage"] = "✅ تم تدريب جميع نماذج الذكاء الاصطناعي بنجاح.";
            return RedirectToAction("Index", "Branches");
        }

        [HttpGet]
        public IActionResult TrainStudentModel()
        {
            _studentTrainer.TrainAndSaveModel(_context);
            return Json(new { success = true, message = "✅ تم تدريب نموذج الطلاب بنجاح." });
        }


        [HttpPost]
        public IActionResult GenerateStudentPrediction(int id)
        {
            var student = _context.Students
                .Include(s => s.StudentPerformances)
                .FirstOrDefault(s => s.StudentID == id);

            if (student == null || !student.StudentPerformances.Any())
            {
                TempData["ErrorMessage"] = "❌ لا يمكن توليد تنبؤ: لا توجد بيانات كافية.";
                return RedirectToAction("Details", "Students", new { id });
            }

            var predicted = _studentTrainer.PredictScore(
                previousScore: (float)student.StudentPerformances.Average(p => p.Score),
                studyHours: (float)student.StudentPerformances.Average(p => p.StudyHours),
                exercises: (float)student.StudentPerformances.Average(p => p.ExercisesCompleted),
                attendance: (float)student.StudentPerformances.Average(p => p.AttendanceCount),
                engagement: (float)student.StudentPerformances.Average(p => p.EngagementRate)
            );

            _context.StudentPredictions.Add(new StudentPrediction
            {
                StudentId = id,
                Score = predicted,
                GeneratedAt = DateTime.Now
            });

            _context.SaveChanges();

            TempData["SuccessMessage"] = "✅ تم توليد التنبؤ الذكي لهذا الطالب.";
            return RedirectToAction("Details", "Students", new { id });
        }

        [HttpGet]
        public IActionResult GenerateAllStudentPredictions()
        {
            var students = _context.Students.Include(s => s.StudentPerformances).ToList();
            int updated = 0;

            foreach (var student in students)
            {
                if (!student.StudentPerformances.Any())
                    continue;

                var predicted = _studentTrainer.PredictScore(
                    previousScore: (float)student.StudentPerformances.Average(p => p.Score),
                    studyHours: (float)student.StudentPerformances.Average(p => p.StudyHours),
                    exercises: (float)student.StudentPerformances.Average(p => p.ExercisesCompleted),
                    attendance: (float)student.StudentPerformances.Average(p => p.AttendanceCount),
                    engagement: (float)student.StudentPerformances.Average(p => p.EngagementRate)
                );

                _context.StudentPredictions.Add(new StudentPrediction
                {
                    StudentId = student.StudentID,
                    Score = predicted,
                    GeneratedAt = DateTime.Now
                });

                updated++;
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = $"✅ تم توليد التنبؤات لجميع الطلاب ({updated}) بنجاح.";
            return RedirectToAction("Index", "Students");
        }

        [HttpGet]
        public async Task<IActionResult> TrainAIStudentProgressModel()
        {
            var trainer = HttpContext.RequestServices.GetRequiredService<AIStudentProgressTrainer>();
            var path = await trainer.TrainAndSaveModelAsync();
            TempData["SuccessMessage"] = "✅ تم تدريب النموذج الجديد وحُفظ في: " + path;
            return RedirectToAction("Index", "Students");
        }

        // ================================
        // ✅ API Actions (للـ fetch / AJAX)
        // ================================

        [HttpGet]
        public IActionResult ApiTrainAllModels()
        {
            try
            {
                BranchMLTrainer.TrainAndSaveModel(_context);
                _studentTrainer.TrainAndSaveModel(_context);
                ChartModelTrainer.TrainAndSave();

                return Json(new { success = true, message = "✅ تم تدريب جميع النماذج بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ خطأ: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ApiTrainStudentModel()
        {
            try
            {
                _studentTrainer.TrainAndSaveModel(_context);
                return Json(new { success = true, message = "✅ تم تدريب نموذج الطلاب بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ خطأ: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ApiTrainChartModel()
        {
            try
            {
                ChartModelTrainer.TrainAndSave();
                return Json(new { success = true, message = "✅ تم تدريب نموذج الرسوم البيانية بنجاح" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ خطأ: " + ex.Message });
            }
        }

        // Sprint 3 (ED3) — إعادة الحساب الفوري الجماعي لكل حالة ExamStudentStatuses
        // تم تصفير Note لها (سواء عبر Fix_ExamReport_ScoreIntegrity.sql أو غيره) دون
        // انتظار زيارة كل طالب لتقريره بنفسه. راجع
        // PROMPT/ExamReport_Integrity_Execution_Prompt_QdratNew.md قسم ED3.
        [HttpGet]
        public async Task<IActionResult> ApiRecomputeExamReportIntegrity()
        {
            try
            {
                var targets = await _context.ExamStudentStatuses
                    .Where(s => s.IsSubmitted && string.IsNullOrEmpty(s.Note))
                    .Select(s => new
                    {
                        s.ExamAssignmentId,
                        s.ExamAssignmentToStudentId,
                        s.StudentId
                    })
                    .ToListAsync();

                int fixedCount = 0, failedCount = 0;

                foreach (var t in targets)
                {
                    try
                    {
                        if (t.ExamAssignmentId.HasValue)
                            await _examResultEngine.GenerateSnapshotIfMissingAsync(t.ExamAssignmentId.Value, t.StudentId);
                        else if (t.ExamAssignmentToStudentId.HasValue)
                            await _examResultEngine.GenerateSnapshotIfMissingForIndividualAsync(t.ExamAssignmentToStudentId.Value, t.StudentId);
                        else
                            continue;

                        fixedCount++;
                    }
                    catch
                    {
                        failedCount++;
                    }
                }

                var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var adminName = User.Identity?.Name ?? "غير معروف";

                await _activityLogger.LogAsync(
                    actionType: "إعادة حساب جماعي — سلامة نتائج الاختبارات (حادثة تكليف 2116)",
                    description: $"تمت إعادة حساب {fixedCount} حالة اختبار كانت تحمل نتيجة متضاربة رياضيًا " +
                                 $"(بعد تصفير Note عبر Fix_ExamReport_ScoreIntegrity.sql). فشلت إعادة الحساب في {failedCount} حالة.",
                    adminId, adminName);

                return Json(new
                {
                    success = true,
                    message = $"✅ تمت إعادة حساب {fixedCount} حالة بنجاح" + (failedCount > 0 ? $" (فشل {failedCount})" : "") + "."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ خطأ: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ApiGenerateAllStudentPredictions()
        {
            try
            {
                var students = _context.Students.Include(s => s.StudentPerformances).ToList();
                int updated = 0;

                foreach (var student in students)
                {
                    if (!student.StudentPerformances.Any()) continue;

                    var predicted = _studentTrainer.PredictScore(
                        previousScore: (float)student.StudentPerformances.Average(p => p.Score),
                        studyHours: (float)student.StudentPerformances.Average(p => p.StudyHours),
                        exercises: (float)student.StudentPerformances.Average(p => p.ExercisesCompleted),
                        attendance: (float)student.StudentPerformances.Average(p => p.AttendanceCount),
                        engagement: (float)student.StudentPerformances.Average(p => p.EngagementRate)
                    );

                    _context.StudentPredictions.Add(new StudentPrediction
                    {
                        StudentId = student.StudentID,
                        Score = predicted,
                        GeneratedAt = DateTime.Now
                    });

                    updated++;
                }

                _context.SaveChanges();
                return Json(new { success = true, message = $"✅ تم توليد التنبؤات لجميع الطلاب ({updated})" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ خطأ: " + ex.Message });
            }
        }

        // Sprint 4 (EC4) — شاشة عرض سجل تعديلات أسئلة الاختبار (من/ليه/الأثر)، فلترة بالطالب
        // أو بالتكليف. لا كونترولر آخر يعرض AdminActivityLogs عدا HomeworkManagementController.
        // ArchiveHistory (خاص بالواجبات فقط) — هذه شاشة جديدة مخصصة لسياق الاختبارات، مُضافة هنا
        // بجانب أدوات سلامة نتائج الاختبارات الأخرى (ApiRecomputeExamReportIntegrity).
        [HttpGet]
        public async Task<IActionResult> ExamActivityLog(int? studentId, int? examAssignmentId, int? examAssignmentToStudentId)
        {
            var query = _context.AdminActivityLogs
                .AsNoTracking()
                .Where(l => l.ExamAssignmentId != null || l.ExamAssignmentToStudentId != null);

            if (studentId.HasValue)
                query = query.Where(l => l.StudentId == studentId.Value);

            if (examAssignmentId.HasValue)
                query = query.Where(l => l.ExamAssignmentId == examAssignmentId.Value);

            if (examAssignmentToStudentId.HasValue)
                query = query.Where(l => l.ExamAssignmentToStudentId == examAssignmentToStudentId.Value);

            var items = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(300)
                .ToListAsync();

            var vm = new QdratNew.ViewModels.Admin.ExamActivityLogViewModel
            {
                StudentId = studentId,
                ExamAssignmentId = examAssignmentId,
                ExamAssignmentToStudentId = examAssignmentToStudentId,
                Items = items
            };

            return View(vm);
        }
    }
}
