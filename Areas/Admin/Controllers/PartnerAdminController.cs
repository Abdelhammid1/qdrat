using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.DTOs.Exams;
using QdratNew.Services.Interfaces;
using QdratNew.Services.PartnerHomework;
using QdratNew.ViewModels.Admin;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Partner.Exam;
using QdratNew.ViewModels.Partner.HomeworkDraft;

// ⚠️ System Critical Controller
// Restricted to system-level roles only — SuperAdmin, Owner, Developer.

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class PartnerAdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IPartnerHomeworkAssignmentService _homeworkService;
        private readonly IPartnerExamDraftService _examDraftService;
        private readonly IHomeworkRecommendationService _homeworkRecommendationService;
        private readonly IExamRecommendationService _examRecommendationService;

        public PartnerAdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IPartnerHomeworkAssignmentService homeworkService,
            IPartnerExamDraftService examDraftService,
            IHomeworkRecommendationService homeworkRecommendationService,
            IExamRecommendationService examRecommendationService)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _homeworkService = homeworkService;
            _examDraftService = examDraftService;
            _homeworkRecommendationService = homeworkRecommendationService;
            _examRecommendationService = examRecommendationService;
        }

        // ════════════════════════════════════════════════════════════
        //  HOMEWORK — تقرير واجب
        // ════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> HomeworkReport(int homeworkSetId, int partnerId)
        {
            var homeworkSet = await _db.HomeworkSets
                .AsNoTracking()
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound();

            // Verify the batch belongs to this partner
            var batch = await _db.Batches
                .AsNoTracking()
                .Where(b => b.Id == homeworkSet.BatchId)
                .Select(b => new { b.Id, b.Name, b.Branch.PartnerId })
                .FirstOrDefaultAsync();

            if (batch == null || batch.PartnerId != partnerId)
                return Forbid();

            var partner = await _db.Partners.AsNoTracking()
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(p => p.Id == partnerId);

            // Load students with their homework status
            var students = await _db.HomeworkSetStudents
                .AsNoTracking()
                .Where(hss => hss.HomeworkSetId == homeworkSetId)
                .Join(_db.Students,
                    hss => hss.StudentId,
                    s => s.StudentID,
                    (hss, s) => new PartnerAdminHomeworkStudentRow
                    {
                        StudentId = s.StudentID,
                        FullName = s.FullName,
                        NationalID = s.NationalID,
                        IsSubmitted = hss.IsSubmitted,
                        Score = hss.Score,
                        AssignedAt = hss.AssignedAt,
                        SubmittedAt = hss.SubmittedAt
                    })
                .OrderBy(s => s.FullName)
                .ToListAsync();

            var vm = new PartnerAdminHomeworkReportVM
            {
                PartnerId = partnerId,
                PartnerName = partner?.Name ?? "",
                HomeworkSetId = homeworkSetId,
                HomeworkTitle = homeworkSet.Title,
                BatchName = batch.Name,
                SentAt = homeworkSet.CreatedAt,
                Students = students
            };

            return View(vm);
        }

        // ════════════════════════════════════════════════════════════
        //  HOMEWORK — تقرير واجب مفصّل لطالب محدد
        // ════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> StudentHomeworkDetail(int homeworkSetId, int studentId, int partnerId)
        {
            var homeworkSet = await _db.HomeworkSets
                .AsNoTracking()
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (homeworkSet == null) return NotFound();

            // Verify the batch belongs to this partner
            var batch = await _db.Batches
                .AsNoTracking()
                .Where(b => b.Id == homeworkSet.BatchId)
                .Select(b => new { b.Id, b.Name, b.Branch.PartnerId })
                .FirstOrDefaultAsync();

            if (batch == null || batch.PartnerId != partnerId) return Forbid();

            // Verify student has submitted
            var summary = await _db.HomeworkSetStudents
                .AsNoTracking()
                .Where(x => x.StudentId == studentId && x.HomeworkSetId == homeworkSetId && x.IsSubmitted)
                .Select(x => new { x.Score })
                .FirstOrDefaultAsync();

            if (summary == null)
                return RedirectToAction("HomeworkReport", new { homeworkSetId, partnerId });

            var studentInfo = await _db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.FullName })
                .FirstOrDefaultAsync();

            // Question attempts — take last attempt per question
            var rawAttempts = await (
                from a in _db.QuestionAttemptNew.AsNoTracking()
                join q in _db.Questions.AsNoTracking() on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.HomeworkSetId == homeworkSetId
                select new
                {
                    a.QuestionId,
                    a.AttemptedAt,
                    SelectedAnswer = a.SelectedAnswer,
                    a.IsCorrect,
                    a.TimeTakenSeconds,
                    q.LessonId,
                    QuestionTitle = q.Title,
                    CorrectAnswer = q.CorrectAnswer
                }
            ).ToListAsync();

            var questions = rawAttempts
                .GroupBy(x => x.QuestionId)
                .Select(g =>
                {
                    var last = g.OrderByDescending(x => x.AttemptedAt).First();
                    return new HomeworkQuestionAnalyticsVm
                    {
                        QuestionId    = g.Key,
                        QuestionTitle = last.QuestionTitle,
                        StudentAnswer = last.SelectedAnswer,
                        CorrectAnswer = last.CorrectAnswer,
                        IsCorrect     = last.IsCorrect,
                        TimeTakenSeconds = last.TimeTakenSeconds
                    };
                })
                .ToList();

            var totalSeconds    = questions.Sum(q => q.TimeTakenSeconds);
            var timeSpentMinutes = totalSeconds > 0 ? Math.Round(totalSeconds / 60.0, 1) : 0;

            var vm = new HomeworkAnalyticsViewModel
            {
                HomeworkSetId    = homeworkSetId,
                StudentId        = studentId,
                StudentName      = studentInfo?.FullName ?? "الطالب",
                BatchName        = batch.Name,
                Questions        = questions,
                ScorePercentage  = summary.Score ?? 0,
                TimeSpentMinutes = timeSpentMinutes
            };

            // Sections performance
            var sectionRaw = await (
                from h in _db.Homeworks.AsNoTracking()
                join q in _db.Questions.AsNoTracking() on h.QuestionId equals q.Id
                join l in _db.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _db.Sections.AsNoTracking() on l.SectionId equals s.Id
                where h.StudentId == studentId && h.HomeworkSetId == homeworkSetId
                select new { h.QuestionId, s.Id, s.Title }
            ).ToListAsync();

            vm.SectionsPerformance = sectionRaw
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g =>
                {
                    var total   = g.Count();
                    var correct = g.Count(x => questions.Any(q => q.QuestionId == x.QuestionId && q.IsCorrect));
                    return new SectionPerformanceVm
                    {
                        SectionId    = g.Key.Id,
                        SectionTitle = g.Key.Title,
                        Accuracy     = total > 0 ? Math.Round(correct * 100.0 / total, 1) : 0
                    };
                })
                .ToList();

            vm.BestSection = vm.SectionsPerformance
                .OrderByDescending(x => x.Accuracy)
                .Select(x => x.SectionTitle)
                .FirstOrDefault();

            // Lessons performance with full counts
            var lessonIds    = rawAttempts.Select(a => a.LessonId).Distinct().ToList();
            var lessonTitles = await _db.Lessons
                .AsNoTracking()
                .Where(l => lessonIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Title);

            vm.LessonsPerformance = rawAttempts
                .GroupBy(x => x.LessonId)
                .Select(g =>
                {
                    var totalQ   = g.Select(x => x.QuestionId).Distinct().Count();
                    var correct  = g.Count(x => x.IsCorrect);
                    var wrong    = g.Count(x => !x.IsCorrect && !string.IsNullOrEmpty(x.SelectedAnswer));
                    var skipped  = g.Count(x => string.IsNullOrEmpty(x.SelectedAnswer));
                    var totalSec = g.Sum(x => x.TimeTakenSeconds);
                    lessonTitles.TryGetValue(g.Key, out var title);
                    return new LessonPerformanceVm
                    {
                        LessonId         = g.Key,
                        LessonTitle      = title ?? "—",
                        TotalQuestions   = totalQ,
                        CorrectCount     = correct,
                        WrongCount       = wrong,
                        SkippedCount     = skipped,
                        SuccessRate      = totalQ > 0 ? Math.Round(correct * 100.0 / totalQ, 1) : 0,
                        TimeSpentMinutes = totalSec > 0 ? Math.Round(totalSec / 60.0, 1) : 0
                    };
                })
                .ToList();

            // Recommendations
            vm.Recommendation = _homeworkRecommendationService.GenerateRecommendation(new HomeworkAnalyticsVm
            {
                ScorePercentage  = vm.ScorePercentage,
                TimeSpentMinutes = vm.TimeSpentMinutes,
                QuestionsCount   = vm.TotalQuestions,
                CorrectAnswers   = vm.CorrectCount
            });

            ViewBag.HomeworkSetId = homeworkSetId;
            ViewBag.PartnerId     = partnerId;

            return View(vm);
        }

        // ════════════════════════════════════════════════════════════
        //  HOMEWORK — إرسال مسودة واجب لدفعات
        // ════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AssignHomework(
            int partnerId,
            int draftId,
            [FromForm] List<int> batchIds,
            DateTime startAt,
            DateTime endAt,
            int subscriptionPeriodId)
        {
            if (!batchIds.Any())
            {
                TempData["Error"] = "يجب اختيار دفعة واحدة على الأقل.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            if (endAt <= startAt)
            {
                TempData["Error"] = "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            // Validate that the draft belongs to this partner
            var draftExists = _db.HomeworkDrafts
                .Any(hd => hd.Id == draftId && hd.PartnerId == partnerId && !hd.IsDeleted && !hd.IsArchived);

            if (!draftExists)
            {
                TempData["Error"] = "المسودة المحددة غير موجودة أو لا تنتمي لهذا الشريك.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            // Validate batches belong to partner
            var partnerBatchIds = _db.Batches
                .Where(b => b.Branch.PartnerId == partnerId)
                .Select(b => b.Id)
                .ToList();

            var validBatchIds = batchIds.Where(id => partnerBatchIds.Contains(id)).ToList();
            if (!validBatchIds.Any())
            {
                TempData["Error"] = "الدفعات المحددة لا تنتمي لهذا الشريك.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            var model = new SendHomeworkDraftVM
            {
                DraftId = draftId,
                BatchIds = validBatchIds,
                StartAt = startAt,
                EndAt = endAt
            };

            try
            {
                _homeworkService.SendDraftToBatches(model, partnerId, subscriptionPeriodId);
                TempData["Success"] = "✅ تم إرسال الواجب بنجاح للدفعات المحددة.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "فشل إرسال الواجب: " + ex.GetBaseException().Message;
            }

            return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
        }

        // ════════════════════════════════════════════════════════════
        //  EXAMS — تقرير اختبار
        // ════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> ExamReport(int assignmentId, int partnerId)
        {
            var assignment = await _db.ExamAssignmentsToBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(ea => ea.Id == assignmentId);

            if (assignment == null)
                return NotFound();

            // Verify the batch belongs to this partner
            var batch = await _db.Batches
                .AsNoTracking()
                .Where(b => b.Id == assignment.BatchId)
                .Select(b => new { b.Id, b.Name, b.Branch.PartnerId })
                .FirstOrDefaultAsync();

            if (batch == null || batch.PartnerId != partnerId)
                return Forbid();

            var partner = await _db.Partners.AsNoTracking()
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(p => p.Id == partnerId);

            // Load students enrolled in the batch with their exam status
            var enrolled = await _db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == assignment.BatchId)
                .Join(_db.Students,
                    e => e.StudentID,
                    s => s.StudentID,
                    (e, s) => new { s.StudentID, s.FullName, s.NationalID })
                .ToListAsync();

            var statuses = await _db.ExamStudentStatuses
                .AsNoTracking()
                .Where(st => st.ExamAssignmentId == assignmentId)
                .ToListAsync();

            var statusLookup = statuses.ToDictionary(st => st.StudentId);

            var rows = enrolled.Select(s =>
            {
                statusLookup.TryGetValue(s.StudentID, out var st);
                return new PartnerAdminExamStudentRow
                {
                    StudentId = s.StudentID,
                    FullName = s.FullName,
                    NationalID = s.NationalID,
                    IsSubmitted = st?.IsSubmitted ?? false,
                    Score = st?.Score,
                    Status = st != null ? st.Status.ToString() : "لم يبدأ",
                    StartedAt = st?.StartedAt,
                    SubmittedAt = st?.SubmittedAt
                };
            }).OrderBy(r => r.FullName).ToList();

            var vm = new PartnerAdminExamReportVM
            {
                PartnerId = partnerId,
                PartnerName = partner?.Name ?? "",
                AssignmentId = assignmentId,
                ExamTitle = assignment.Title,
                BatchName = batch.Name,
                AssignedAt = assignment.AssignedAt,
                Students = rows
            };

            return View(vm);
        }

        // ════════════════════════════════════════════════════════════
        //  EXAMS — تقرير اختبار مفصّل لطالب محدد
        // ════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> StudentExamDetail(int assignmentId, int studentId, int partnerId)
        {
            var assignment = await _db.ExamAssignmentsToBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(ea => ea.Id == assignmentId);

            if (assignment == null) return NotFound();

            // Verify batch belongs to partner
            var batch = await _db.Batches
                .AsNoTracking()
                .Where(b => b.Id == assignment.BatchId)
                .Select(b => new { b.Id, b.Name, b.Branch.PartnerId })
                .FirstOrDefaultAsync();

            if (batch == null || batch.PartnerId != partnerId) return Forbid();

            // Verify student has submitted
            var status = await _db.ExamStudentStatuses
                .AsNoTracking()
                .Where(s => s.StudentId == studentId &&
                            s.ExamAssignmentId == assignmentId &&
                            s.IsSubmitted)
                .FirstOrDefaultAsync();

            if (status == null)
                return RedirectToAction("ExamReport", new { assignmentId, partnerId });

            var partner = await _db.Partners.AsNoTracking()
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(p => p.Id == partnerId);

            var studentInfo = await _db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.FullName })
                .FirstOrDefaultAsync();

            // Parse JSON snapshot for aggregate stats
            ExamFinalResultDto? snapshot = null;
            if (!string.IsNullOrWhiteSpace(status.Note))
            {
                try { snapshot = System.Text.Json.JsonSerializer.Deserialize<ExamFinalResultDto>(status.Note); }
                catch { }
            }

            // Load question attempts
            var rawAttempts = await (
                from a in _db.QuestionAttemptNew.AsNoTracking()
                join q in _db.Questions.AsNoTracking() on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.ExamAssignmentId == assignmentId
                select new
                {
                    a.QuestionId,
                    a.AttemptedAt,
                    SelectedAnswer = a.SelectedAnswer,
                    a.IsCorrect,
                    a.TimeTakenSeconds,
                    q.LessonId,
                    QuestionTitle = q.Title,
                    CorrectAnswer = q.CorrectAnswer
                }
            ).ToListAsync();

            var questions = rawAttempts
                .GroupBy(x => x.QuestionId)
                .Select(g =>
                {
                    var last = g.OrderByDescending(x => x.AttemptedAt).First();
                    return new ExamDetailQuestionVm
                    {
                        QuestionId       = g.Key,
                        QuestionTitle    = last.QuestionTitle,
                        StudentAnswer    = last.SelectedAnswer,
                        CorrectAnswer    = last.CorrectAnswer,
                        IsCorrect        = last.IsCorrect,
                        TimeTakenSeconds = last.TimeTakenSeconds
                    };
                }).ToList();

            // Prefer snapshot stats (authoritative), fall back to calculating from attempts
            int totalQ      = snapshot?.TotalQuestions > 0 ? snapshot.TotalQuestions : questions.Count;
            int correct     = snapshot?.Correct > 0       ? snapshot.Correct        : questions.Count(q => q.IsCorrect);
            int wrong       = snapshot?.Wrong > 0         ? snapshot.Wrong          : questions.Count(q => !q.IsCorrect && !string.IsNullOrEmpty(q.StudentAnswer));
            int skipped     = snapshot?.Skipped >= 0      ? snapshot.Skipped        : questions.Count(q => string.IsNullOrEmpty(q.StudentAnswer));
            double scorePercent = snapshot?.ScorePercent > 0 ? snapshot.ScorePercent : (status.Score ?? 0);
            double totalSec     = snapshot?.TotalTimeSeconds > 0 ? snapshot.TotalTimeSeconds : rawAttempts.Sum(a => (double)a.TimeTakenSeconds);
            double timeMinutes  = totalSec > 0 ? Math.Round(totalSec / 60.0, 1) : 0;

            var vm = new PartnerAdminExamDetailVM
            {
                AssignmentId     = assignmentId,
                StudentId        = studentId,
                PartnerId        = partnerId,
                PartnerName      = partner?.Name ?? "",
                StudentName      = studentInfo?.FullName ?? "الطالب",
                BatchName        = batch.Name,
                ExamTitle        = assignment.Title,
                TotalQuestions   = totalQ,
                CorrectCount     = correct,
                WrongCount       = wrong,
                SkippedCount     = skipped,
                ScorePercentage  = scorePercent,
                TimeSpentMinutes = timeMinutes,
                Questions        = questions
            };

            // Sections performance from question attempts
            var sectionRaw = await (
                from a in _db.QuestionAttemptNew.AsNoTracking()
                join q in _db.Questions.AsNoTracking() on a.QuestionId equals q.Id
                join l in _db.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _db.Sections.AsNoTracking() on l.SectionId equals s.Id
                where a.StudentId == studentId && a.ExamAssignmentId == assignmentId
                select new { a.QuestionId, a.IsCorrect, s.Id, s.Title }
            ).ToListAsync();

            vm.SectionsPerformance = sectionRaw
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g =>
                {
                    var tot  = g.Select(x => x.QuestionId).Distinct().Count();
                    var corr = g.Count(x => x.IsCorrect);
                    return new ExamDetailSectionVm
                    {
                        SectionId      = g.Key.Id,
                        SectionTitle   = g.Key.Title,
                        TotalQuestions = tot,
                        CorrectCount   = corr,
                        Accuracy       = tot > 0 ? Math.Round(corr * 100.0 / tot, 1) : 0
                    };
                }).ToList();

            // Lessons performance
            var lessonIds    = rawAttempts.Select(a => a.LessonId).Distinct().ToList();
            var lessonTitles = await _db.Lessons
                .AsNoTracking()
                .Where(l => lessonIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Title);

            vm.LessonsPerformance = rawAttempts
                .GroupBy(x => x.LessonId)
                .Select(g =>
                {
                    var tot   = g.Select(x => x.QuestionId).Distinct().Count();
                    var corr  = g.Count(x => x.IsCorrect);
                    var wrng  = g.Count(x => !x.IsCorrect && !string.IsNullOrEmpty(x.SelectedAnswer));
                    var skip  = g.Count(x => string.IsNullOrEmpty(x.SelectedAnswer));
                    var sec   = g.Sum(x => (double)x.TimeTakenSeconds);
                    lessonTitles.TryGetValue(g.Key, out var title);
                    return new ExamDetailLessonVm
                    {
                        LessonId         = g.Key,
                        LessonTitle      = title ?? "—",
                        TotalQuestions   = tot,
                        CorrectCount     = corr,
                        WrongCount       = wrng,
                        SkippedCount     = skip,
                        SuccessRate      = tot > 0 ? Math.Round(corr * 100.0 / tot, 1) : 0,
                        TimeSpentMinutes = sec > 0 ? Math.Round(sec / 60.0, 1) : 0
                    };
                }).ToList();

            // Recommendations
            vm.Recommendation = _examRecommendationService.GetRecommendation(
                scorePercent, (int)timeMinutes, assignment.DurationMinutes > 0 ? assignment.DurationMinutes : 60);

            ViewBag.AssignmentId = assignmentId;
            ViewBag.PartnerId    = partnerId;

            return View(vm);
        }

        // ════════════════════════════════════════════════════════════
        //  EXAMS — إرسال مسودة اختبار لدفعات
        // ════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AssignExam(
            int partnerId,
            int draftId,
            [FromForm] List<int> batchIds,
            DateTime startAt,
            DateTime endAt,
            int durationMinutes)
        {
            if (!batchIds.Any())
            {
                TempData["Error"] = "يجب اختيار دفعة واحدة على الأقل.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            if (endAt <= startAt)
            {
                TempData["Error"] = "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            // Validate that the draft belongs to this partner
            var draft = _db.ExamDrafts
                .AsNoTracking()
                .FirstOrDefault(ed => ed.Id == draftId && ed.PartnerId == partnerId && !ed.IsArchived);

            if (draft == null)
            {
                TempData["Error"] = "مسودة الاختبار غير موجودة أو لا تنتمي لهذا الشريك.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            // Validate batches belong to partner
            var partnerBatchIds = _db.Batches
                .Where(b => b.Branch.PartnerId == partnerId)
                .Select(b => b.Id)
                .ToList();

            var validBatchIds = batchIds.Where(id => partnerBatchIds.Contains(id)).ToList();
            if (!validBatchIds.Any())
            {
                TempData["Error"] = "الدفعات المحددة لا تنتمي لهذا الشريك.";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            var model = new SendExamDraftVM
            {
                DraftId = draftId,
                ExamTitle = draft.Title,
                CurriculumId = draft.CurriculumId,
                BatchIds = validBatchIds,
                StartAt = startAt,
                EndAt = endAt,
                DurationMinutes = durationMinutes > 0 ? durationMinutes : 30
            };

            try
            {
                _examDraftService.SendToBatches(model, partnerId);
                TempData["Success"] = "✅ تم إرسال الاختبار بنجاح للدفعات المحددة.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "فشل إرسال الاختبار: " + ex.GetBaseException().Message;
            }

            return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
        }

        // ════════════════════════════════════════════════════════════
        //  EXCEL IMPORT — رفع طلاب من Excel
        // ════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PartnerBulkImport(
            int partnerId,
            IFormFile? excelFile,
            int batchId,
            int branchId)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "⚠️ اختر ملف Excel";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            if (batchId <= 0)
            {
                TempData["Error"] = "⚠️ يجب اختيار الدفعة قبل رفع الملف";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            if (branchId <= 0)
            {
                TempData["Error"] = "⚠️ يجب اختيار الفرع قبل رفع الملف";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            // Validate branch belongs to partner
            var branch = await _db.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == branchId && b.PartnerId == partnerId);

            if (branch == null)
            {
                TempData["Error"] = "⚠️ الفرع المحدد لا ينتمي لهذا الشريك";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            // Validate batch belongs to partner's branch
            var batch = await _db.Batches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId && b.BranchId == branchId);

            if (batch == null)
            {
                TempData["Error"] = "⚠️ الدفعة المحددة لا تنتمي لهذا الفرع";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets.FirstOrDefault();

            if (sheet == null || sheet.Dimension == null)
            {
                TempData["Error"] = "⚠️ الملف فارغ";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            int lastRow = sheet.Dimension.End.Row;

            var importedRows = new List<PartnerImportRow>();
            var processedIds = new HashSet<string>();

            for (int row = 2; row <= lastRow; row++)
            {
                var fullName   = sheet.Cells[row, 1].Text?.Trim();
                var rawId      = sheet.Cells[row, 2].Text?.Trim();
                var phone      = sheet.Cells[row, 3].Text?.Trim();
                var gender     = sheet.Cells[row, 4].Text?.Trim();
                var school     = sheet.Dimension.End.Column >= 5 ? sheet.Cells[row, 5].Text?.Trim() : null;
                var level      = sheet.Dimension.End.Column >= 6 ? sheet.Cells[row, 6].Text?.Trim() : null;

                if (string.IsNullOrWhiteSpace(rawId)) continue;

                var nationalId = new string(rawId.Where(char.IsDigit).ToArray());
                if (string.IsNullOrWhiteSpace(nationalId) || nationalId.Length < 9) continue;
                if (!processedIds.Add(nationalId)) continue;

                importedRows.Add(new PartnerImportRow
                {
                    FullName   = string.IsNullOrWhiteSpace(fullName) ? "غير معروف" : fullName,
                    NationalId = nationalId,
                    Phone      = phone,
                    Gender     = NormalizeGender(gender),
                    School     = string.IsNullOrWhiteSpace(school) ? "غير محدد" : school,
                    Level      = string.IsNullOrWhiteSpace(level)  ? "غير محدد" : level
                });
            }

            if (!importedRows.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على بيانات صالحة داخل الملف";
                return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
            }

            // Load existing users
            var usersRaw = await _db.Users.AsNoTracking()
                .Select(u => new { u.Id, u.NationalID }).ToListAsync();
            var usersDict = usersRaw
                .Where(u => !string.IsNullOrWhiteSpace(u.NationalID))
                .Select(u => new { u.Id, Key = new string(u.NationalID!.Where(char.IsDigit).ToArray()) })
                .Where(u => !string.IsNullOrWhiteSpace(u.Key))
                .GroupBy(u => u.Key)
                .ToDictionary(g => g.Key, g => g.First().Id);

            // Load existing students
            var studentsRaw = await _db.Students.AsNoTracking()
                .Select(s => new { s.StudentID, s.NationalID }).ToListAsync();
            var studentsDict = studentsRaw
                .Where(s => !string.IsNullOrWhiteSpace(s.NationalID))
                .Select(s => new { s.StudentID, Key = new string(s.NationalID!.Where(char.IsDigit).ToArray()) })
                .Where(s => !string.IsNullOrWhiteSpace(s.Key))
                .GroupBy(s => s.Key)
                .ToDictionary(g => g.Key, g => g.First().StudentID);

            // Existing enrollments for this batch
            var existingEnrollments = await _db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();
            var enrolledSet = existingEnrollments.ToHashSet();

            if (!await _roleManager.RoleExistsAsync("Student"))
                await _roleManager.CreateAsync(new IdentityRole("Student"));

            var errors = new List<string>();
            int createdUsers = 0, createdStudents = 0;

            // Create missing users
            foreach (var row in importedRows)
            {
                if (usersDict.ContainsKey(row.NationalId)) continue;

                var newUser = new ApplicationUser
                {
                    UserName = row.NationalId,
                    Email = $"{row.NationalId}@qdrat.local",
                    NationalID = row.NationalId,
                    FullName = row.FullName,
                    PhoneNumber = row.Phone,
                    WhatsAppNumber = row.Phone,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(newUser, row.NationalId);
                if (!result.Succeeded)
                {
                    errors.Add($"{row.FullName} ({row.NationalId}): " +
                               string.Join(", ", result.Errors.Select(e => e.Description)));
                    continue;
                }

                await _userManager.AddToRoleAsync(newUser, "Student");
                usersDict[row.NationalId] = newUser.Id;
                createdUsers++;
            }

            // Create missing students
            foreach (var row in importedRows)
            {
                if (studentsDict.ContainsKey(row.NationalId)) continue;
                if (!usersDict.TryGetValue(row.NationalId, out var userId)) continue;

                var student = new Student
                {
                    NationalID = row.NationalId,
                    FullName = row.FullName,
                    Email = $"{row.NationalId}@qdrat.local",
                    PhoneNumber = row.Phone,
                    WhatsAppNumber = row.Phone,
                    Gender = row.Gender,
                    School = row.School,
                    Level = row.Level,
                    Age = 18,
                    BranchId = branchId,
                    UserId = userId,
                    EnrollmentStatus = "نشط",
                    RegistrationDate = DateTime.UtcNow,
                    IsRegular = true
                };

                _db.Students.Add(student);
                await _db.SaveChangesAsync();
                studentsDict[row.NationalId] = student.StudentID;
                createdStudents++;
            }

            // Enroll in batch
            var newEnrollments = new List<StudentBatchEnrollment>();
            foreach (var row in importedRows)
            {
                if (!studentsDict.TryGetValue(row.NationalId, out var studentId)) continue;
                if (!enrolledSet.Add(studentId)) continue;

                newEnrollments.Add(new StudentBatchEnrollment
                {
                    StudentID = studentId,
                    BatchId = batchId,
                    EnrolledAt = DateTime.UtcNow,
                    Status = "Active"
                });
            }

            if (newEnrollments.Any())
                await _db.BulkInsertAsync(newEnrollments);

            if (errors.Any())
                TempData["Error"] = string.Join("<br/>", errors.Take(20));

            TempData["Success"] =
                $"✅ تم إنشاء {createdUsers} مستخدم، و{createdStudents} طالب، وإضافة {newEnrollments.Count} طالب للدفعة.";

            return RedirectToAction("PartnerUserDetails", "Users", new { area = "Admin", partnerId });
        }

        // ════════════════════════════════════════════════════════════
        //  AJAX — الدفعات حسب الفرع
        // ════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetBatchesByBranch(int branchId, int partnerId)
        {
            var branch = await _db.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == branchId && b.PartnerId == partnerId);

            if (branch == null)
                return Json(new List<object>());

            var batches = await _db.Batches
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && !b.IsDeleted)
                .OrderBy(b => b.Name)
                .Select(b => new { value = b.Id, text = b.Name, isActive = b.IsActive })
                .ToListAsync();

            return Json(batches);
        }

        // ════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════

        private static string NormalizeGender(string? gender)
        {
            if (string.IsNullOrWhiteSpace(gender)) return "غير محدد";
            var v = gender.Trim();
            if (v == "ذكر" || v.Equals("Male", StringComparison.OrdinalIgnoreCase) || v == "M") return "ذكر";
            if (v == "أنثى" || v == "انثى" || v.Equals("Female", StringComparison.OrdinalIgnoreCase) || v == "F") return "أنثى";
            return v;
        }

        private sealed class PartnerImportRow
        {
            public string FullName   { get; set; } = "غير معروف";
            public string NationalId { get; set; } = "";
            public string? Phone     { get; set; }
            public string Gender     { get; set; } = "غير محدد";
            public string School     { get; set; } = "غير محدد";
            public string Level      { get; set; } = "غير محدد";
        }
    }
}
