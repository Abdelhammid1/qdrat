using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Partner.Exam;
using QdratNew.ViewModels.Reports;
using System.Text.Json;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class PartnerExamController : PartnerBaseController
    {
        private readonly IPartnerExamGenerationService _examService;
        private readonly IExamResultEngine _examResultEngine;
        private readonly IExamRecommendationService _recommendationService;
        private readonly IAdvancedNotificationService _notificationService;

        public PartnerExamController(
            ApplicationDbContext context,
            IPartnerSubscriptionService subscriptionService,
            IExamResultEngine examResultEngine,
            IExamRecommendationService recommendationService,
            IPartnerExamGenerationService examService,
            IAdvancedNotificationService notificationService
        )
            : base(subscriptionService, context)
        {
            _examService = examService;
            _examResultEngine = examResultEngine;
            _recommendationService = recommendationService;
            _notificationService = notificationService;
        }



        // =====================================================
        // STEP 1: شاشة إنشاء المسودة (Generate)
        // =====================================================
        [HttpGet]
        public IActionResult Generate()
        {
            return View(new GenerateExamRequestVM());
        }


        [HttpGet]
        public IActionResult SentExams()
        {
            // 🔹 تأكيد وجود شريك
            if (ActivePartnerId <= 0)
                return View(new List<SentExamListVM>());

            // 🔹 جلب BranchIds الخاصة بالشريك
            var partnerBranchIds = _context.Branches
                .Where(b => b.PartnerId == ActivePartnerId)
                .Select(b => b.Id)
                .ToList();

            if (!partnerBranchIds.Any())
                return View(new List<SentExamListVM>());

            // 🔹 جلب Assignments الأساسية فقط (بدون أي Navigation)
            var assignmentsRaw = _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x =>
                    x.IsSentToStudents &&
                    x.ExamId.HasValue &&          // ExamId nullable ✔
                    x.BatchId > 0                 // BatchId int ✔
                )
                .Select(x => new
                {
                    x.Id,
                    ExamId = x.ExamId.Value,
                    x.BatchId,
                    x.Title,
                    x.AssignedAt
                })
                .ToList();

            if (!assignmentsRaw.Any())
                return View(new List<SentExamListVM>());

            // 🔹 جلب الدُفعات مرة واحدة
            var allBatches = _context.Batches
         .AsNoTracking()
         .Select(b => new
         {
             b.Id,
             b.Name,
             b.BranchId
         })
         .ToList();

            var batches = allBatches
                .Where(b => partnerBranchIds.Any(pid => pid == b.BranchId))
                .ToList();


            // 🔹 جلب حالات الطلاب مرة واحدة
            var allStatuses = _context.ExamStudentStatuses
                .AsNoTracking()
                .ToList();

            var result = new List<SentExamListVM>();

            foreach (var a in assignmentsRaw)
            {
                var batch = batches.FirstOrDefault(b => b.Id == a.BatchId);
                if (batch == null)
                    continue;

                var totalStudents = _context.StudentBatchEnrollments
                    .Count(s => s.BatchId == a.BatchId);

                var attempted = 0;

                foreach (var s in allStatuses)
                {
                    if (s.ExamAssignmentId == a.Id &&
                        (s.IsSubmitted || s.Status == ExamStatus.Completed))
                    {
                        attempted++;
                    }
                }

                result.Add(new SentExamListVM
                {
                    ExamId = a.ExamId,
                    ExamAssignmentId = a.Id,
                    BatchId = a.BatchId,
                    ExamTitle = a.Title,
                    BatchName = batch.Name,
                    StudentsCount = totalStudents,
                    AttemptedCount = attempted,
                    NotAttemptedCount = Math.Max(0, totalStudents - attempted),
                    SentAt = a.AssignedAt
                });
            }

            return View(result);
        }



        // =====================================================
        // AJAX: جلب المؤشرات داخل محور
        // =====================================================
        // =====================================================
        // AJAX: جلب المؤشرات الفعالة فقط داخل محور
        // =====================================================
        [HttpGet]
        public IActionResult GetLessonsBySection(int sectionId)
        {
            if (sectionId <= 0)
                return Json(new List<object>());

            // 1️⃣ جلب الدروس النشطة فقط
            var activeLessons = _context.Lessons
                .AsNoTracking()
                .Where(l =>
                    l.SectionId == sectionId &&
                    l.IsActive)
                .Select(l => new
                {
                    l.Id,
                    l.Title
                })
                .ToList();

            if (!activeLessons.Any())
                return Json(new List<object>());

            // 2️⃣ جلب LessonIds
            var lessonIds = activeLessons
                .Select(x => x.Id)
                .ToList();

            // 3️⃣ جلب الأسئلة الجاهزة فقط (SQL 2014 SAFE)
            var questionLessonIds = _context.Questions
                .AsNoTracking()
                .Where(q =>
                    q.IsComplete &&
                    q.IsAnswerConfirmed)
                .Select(q => q.LessonId)
                .ToList();

            // 4️⃣ تصفية في الذاكرة (بدون Contains في SQL)
            var validLessons = activeLessons
                .Where(l =>
                    questionLessonIds.Any(qId => qId == l.Id))
                .ToList();

            return Json(validLessons);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendExamToStudent(int examAssignmentId, int studentId)
        {
            // ============================
            // 1️⃣ إعادة ضبط حالة الاختبار
            // ============================
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(x =>
                    x.ExamAssignmentId == examAssignmentId &&
                    x.StudentId == studentId);

            if (status != null)
            {
                status.IsSubmitted = false;
                status.Status = ExamStatus.Pending;
                status.SubmittedAt = null;
                status.StartedAt = null;
                status.EndAt = null;
                status.Note = null;

                // ✅ عدّاد إعادة الإرسال
                status.ResendCount += 1;
            }


            // حذف المحاولات السابقة
            var attempts = await _context.QuestionAttemptNew
                .Where(x =>
                    x.ExamAssignmentId == examAssignmentId &&
                    x.StudentId == studentId)
                .ToListAsync();

            _context.QuestionAttemptNew.RemoveRange(attempts);

            await _context.SaveChangesAsync();

            // ============================
            // 2️⃣ بيانات الطالب والاختبار
            // ============================
            var student = await _context.Students
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName,
                    s.UserId
                })
                .FirstOrDefaultAsync();

            var examInfo = await _context.ExamAssignmentsToBatches
                .Where(x => x.Id == examAssignmentId)
                .Select(x => new
                {
                    x.Title,
                    x.Batch.Name
                })
                .FirstOrDefaultAsync();

            // ============================
            // 3️⃣ إرسال الإشعار
            // ============================
            if (student != null && !string.IsNullOrEmpty(student.UserId))
            {
                await _notificationService.SendToUserAsync(
                    student.UserId,
                    $"📢 تم إعادة إتاحة اختبار \"{examInfo?.Title}\" للدفعة {examInfo?.Name}. يمكنك الدخول وحل الاختبار من جديد.",
                    NotificationCategory.Exam,
                    $"/Students/Exams/Start/{examAssignmentId}"
                );
            }


            return RedirectToAction(nameof(Students), new { examAssignmentId });
        }



        [HttpGet]
        public async Task<IActionResult> Attempts(int examAssignmentId, int studentId)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Where(x =>
                    x.Id == examAssignmentId &&
                    x.Batch.Branch.PartnerId == ActivePartnerId
                )
                .Select(x => new
                {
                    x.Id,
                    x.Title
                })
                .FirstOrDefaultAsync();

            if (assignment == null)
                return NotFound();

            var student = await _context.Students
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName
                })
                .FirstOrDefaultAsync();

            if (student == null)
                return NotFound();

            var attempts = await _context.QuestionAttemptNew
                .Where(x =>
                    x.ExamAssignmentId == examAssignmentId &&
                    x.StudentId == studentId
                )
                .OrderBy(x => x.AttemptedAt)
                .Select(x => new PartnerExamAttemptRowVM
                {
                    AttemptNumber = x.AttemptNumber ?? 1,
                    AttemptedAt = x.AttemptedAt,
                    IsCorrect = x.IsCorrect,
                    TimeTakenSeconds = x.TimeTakenSeconds
                })
                .ToListAsync();

            var resendCount = await _context.ExamStudentStatuses
                .Where(x =>
                    x.ExamAssignmentId == examAssignmentId &&
                    x.StudentId == studentId)
                .Select(x => x.ResendCount)
                .FirstOrDefaultAsync();

            var vm = new PartnerExamAttemptsVM
            {
                ExamAssignmentId = examAssignmentId,
                StudentId = studentId,
                StudentName = student.FullName,
                ExamTitle = assignment.Title,
                ResendCount = resendCount,
                Attempts = attempts
            };

            return View(vm);
        }



        [HttpGet]
        public IActionResult Students(int examAssignmentId)
        {
            var assignment = _context.ExamAssignmentsToBatches
                .Where(x =>
                    x.Id == examAssignmentId &&
                    x.Batch.Branch.PartnerId == ActivePartnerId
                )
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.BatchId,
                    BatchName = x.Batch.Name
                })
                .FirstOrDefault();

            if (assignment == null)
                return NotFound();

            var students = (
                from sb in _context.StudentBatchEnrollments
                join s in _context.Students on sb.StudentID equals s.StudentID
                where sb.BatchId == assignment.BatchId
                select new
                {
                    s.StudentID,
                    s.FullName
                }
            ).ToList();

            var statuses = _context.ExamStudentStatuses
                .Where(s => s.ExamAssignmentId == examAssignmentId)
                .ToList();

            var vm = new PartnerExamStudentsVM
            {
                ExamAssignmentId = assignment.Id,
                ExamTitle = assignment.Title,
                BatchName = assignment.BatchName,
                Students = students.Select(st =>
                {
                    var status = statuses.FirstOrDefault(x => x.StudentId == st.StudentID);

                    return new PartnerExamStudentRowVM
                    {
                        StudentId = st.StudentID,
                        StudentName = st.FullName,
                        Status = status == null
                            ? "لم يبدأ"
                            : status.IsSubmitted
                                ? "تم التسليم"
                                : "قيد الحل",
                        IsSubmitted = status?.IsSubmitted ?? false
                    };
                }).ToList()
            };

            return View(vm);
        }



        [HttpGet]
        public async Task<IActionResult> StudentExamReport(int examAssignmentId, int studentId)
        {
            // =========================================
            // 1️⃣ التحقق من أن الاختبار تابع للشريك
            // =========================================
            var assignment = await _context.ExamAssignmentsToBatches
                .Where(x =>
                    x.Id == examAssignmentId &&
                    x.Batch.Branch.PartnerId == ActivePartnerId
                )
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.DurationMinutes,
                    ExamDate = x.ScheduledDate ?? x.AssignedAt
                })
                .FirstOrDefaultAsync();

            if (assignment == null)
                return NotFound();

            // =========================================
            // 2️⃣ حالة الطالب (لا تقرير بدون تسليم)
            // =========================================
            var status = await _context.ExamStudentStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentId == examAssignmentId &&
                    s.IsSubmitted);

            if (status == null)
            {
                TempData["ErrorMessage"] = "لا يوجد تقرير لهذا الطالب.";
                return RedirectToAction(nameof(Students), new { examAssignmentId });
            }

            // =========================================
            // 3️⃣ Snapshot (توليد إن لم يوجد)
            // =========================================
            if (string.IsNullOrWhiteSpace(status.Note))
            {
                await _examResultEngine.GenerateSnapshotIfMissingAsync(
                    examAssignmentId,
                    studentId
                );

                status = await _context.ExamStudentStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.ExamAssignmentId == examAssignmentId);
            }

            if (status == null || string.IsNullOrWhiteSpace(status.Note))
            {
                TempData["ErrorMessage"] = "تعذر إنشاء تقرير لهذا الطالب.";
                return RedirectToAction(nameof(Students), new { examAssignmentId });
            }

            var snapshot =
                JsonSerializer.Deserialize<ExamFinalResultDto>(status.Note);

            if (snapshot == null)
                return NotFound();

            // =========================================
            // 4️⃣ بيانات الطالب
            // =========================================
            var student = await _context.Students
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName,
                    s.Level
                })
                .FirstOrDefaultAsync();

            if (student == null)
                return NotFound();

            // =========================================
            // 5️⃣ الزمن
            // =========================================
            double solveMinutes =
                snapshot.TotalTimeSeconds > 0
                    ? Math.Round(snapshot.TotalTimeSeconds / 60.0, 1)
                    : 0;

            double percentTime =
                assignment.DurationMinutes > 0
                    ? Math.Round((solveMinutes / assignment.DurationMinutes) * 100, 1)
                    : 0;

            var rec = _recommendationService.GetRecommendation(
                snapshot.ScorePercent,
                (int)solveMinutes,
                assignment.DurationMinutes
            );

            // =========================================
            // 6️⃣ المحاور
            // =========================================
            var sectionTitles = await _context.Sections
                .Where(s => snapshot.Sections.Keys.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(x => x.Id, x => x.Title);

            var sectionStats = snapshot.Sections.Select(sec =>
            {
                int total = sec.Value.Correct + sec.Value.Wrong + sec.Value.Skipped;

                return new ExamSectionPerformancesVm
                {
                    SectionId = sec.Key,
                    SectionName = sectionTitles.GetValueOrDefault(sec.Key, "غير معروف"),
                    TotalQuestions = total,
                    CorrectAnswers = sec.Value.Correct,
                    WrongAnswers = sec.Value.Wrong,
                    Skipped = sec.Value.Skipped,
                    AccuracyPercent =
                        total == 0 ? 0 :
                        Math.Round(sec.Value.Correct * 100.0 / total, 1)
                };
            }).ToList();





            // =========================================
            // 7️⃣ ViewModel
            // =========================================
            var vm = new ExamDetailedReportViewModel
            {
                StudentId = student.StudentID,
                StudentName = student.FullName,
                Level = student.Level,

                ExamAssignmentId = examAssignmentId,
                ExamTitle = assignment.Title,
                ExamDate = assignment.ExamDate,
                IsIndividual = false,

                TotalQuestions = snapshot.TotalQuestions,
                TotalCorrect = snapshot.Correct,
                TotalWrong = snapshot.Wrong,
                TotalSkipped = snapshot.Skipped,

                TotalScore = snapshot.Correct,
                MaxScore = snapshot.TotalQuestions,

                SolveMinutes = solveMinutes,
                TotalMinutes = assignment.DurationMinutes,
                PercentTime = percentTime,
                OverallPercent = snapshot.ScorePercent,

                SectionPerformances = sectionStats,

                TrackCard1 = rec.TrackCard1,
                TrackCard1Desc = rec.TrackCard1Desc,
                TrackCard2 = rec.TrackCard2,
                TrackCard2Desc = rec.TrackCard2Desc,
                SpeedLabel = rec.SpeedLabel,
                SpeedNote = rec.SpeedNote,
                IndividualTips = rec.IndividualTips
            };


            // =========================================
            // 8️⃣ تجهيز بيانات التشارت (بدون Contains)
            // SQL Server 2014 SAFE
            // =========================================

            // 1️⃣ جلب كل المحاور مع نوع المنهج (مرة واحدة)
            var allSections = await _context.Sections
                .Include(s => s.Curriculum)
                .AsNoTracking()
                .ToListAsync();

            // 2️⃣ بناء Dictionary في الذاكرة
            var sectionTypeMap = allSections
                .Where(s => sectionStats.Any(x => x.SectionId == s.Id))
                .ToDictionary(
                    s => s.Id,
                    s => s.Curriculum != null && s.Curriculum.IsQuantitative
                );

            // =====================
            // 📐 كمي
            // =====================
            var quantSections = sectionStats
                .Where(s =>
                    sectionTypeMap.ContainsKey(s.SectionId) &&
                    sectionTypeMap[s.SectionId])
                .ToList();

            // =====================
            // 🗣️ لفظي
            // =====================
            var verbalSections = sectionStats
                .Where(s =>
                    sectionTypeMap.ContainsKey(s.SectionId) &&
                    !sectionTypeMap[s.SectionId])
                .ToList();

            vm.HasQuantChart = quantSections.Any();
            vm.HasVerbalChart = verbalSections.Any();

            if (vm.HasQuantChart)
            {
                vm.QuantLabels = quantSections.Select(s => s.SectionName).ToList();
                vm.QuantCorrectCounts = quantSections.Select(s => s.CorrectAnswers).ToList();
                vm.QuantWrongCounts = quantSections.Select(s => s.WrongAnswers).ToList();
            }

            if (vm.HasVerbalChart)
            {
                vm.VerbalLabels = verbalSections.Select(s => s.SectionName).ToList();
                vm.VerbalCorrectCounts = verbalSections.Select(s => s.CorrectAnswers).ToList();
                vm.VerbalWrongCounts = verbalSections.Select(s => s.WrongAnswers).ToList();
            }



            return View(
                "~/Areas/Partner/Views/PartnerExam/ExamReport.cshtml",
                vm
            );
        }




        [HttpGet]
        public IActionResult Details(int examId, int batchId)
        {
            // ===============================
            // 1️⃣ جلب Assignment + التحقق من الشريك
            // ===============================
            var assignment = _context.ExamAssignmentsToBatches
                .Where(x =>
                    x.ExamId == examId &&
                    x.BatchId == batchId &&
                    x.IsSentToStudents &&
                    x.Batch.Branch.PartnerId == ActivePartnerId
                )
                .Select(x => new
                {
                    x.Id, // ExamAssignmentId
                    x.Title,
                    BatchName = x.Batch.Name,
                    x.AssignedAt,
                    x.ScheduledDate,
                    x.EndAt,
                    StudentsCount = _context.StudentBatchEnrollments
                        .Count(s => s.BatchId == x.BatchId)
                })
                .FirstOrDefault();

            if (assignment == null)
                return NotFound();

            // ===============================
            // 2️⃣ جلب أسئلة الاختبار (بدون Contains)
            // ===============================
            var questions = _context.ExamQuestions
                .Where(eq => eq.ExamAssignmentId == assignment.Id)
                .OrderBy(eq => eq.Order)
                .Select(eq => new ExamDetailsQuestionVM
                {
                    QuestionId = eq.Question.Id,
                    Title = eq.Question.Title
                })
                .ToList();

            // ===============================
            // 3️⃣ بناء ViewModel
            // ===============================
            var model = new SentExamDetailsVM
            {
                ExamId = examId,
                BatchId = batchId,
                Title = assignment.Title,
                BatchName = assignment.BatchName,
                StudentsCount = assignment.StudentsCount,
                SentAt = assignment.AssignedAt,
                StartAt = assignment.ScheduledDate,
                EndAt = assignment.EndAt,
                Questions = questions
            };

            return View(model);
        }


        // =====================================================
        // AJAX: جلب الدورات
        // =====================================================
        [HttpGet]
        public IActionResult GetCourses()
        {
            var courses = _context.PartnerSubscriptionCourses
                .Where(x => x.PartnerSubscription.PartnerId == ActivePartnerId)
                .Select(x => new
                {
                    x.Course.Id,
                    x.Course.Name
                })
                .Distinct()
                .OrderBy(x => x.Name)
                .ToList();

            return Json(courses);
        }

        // =====================================================
        // AJAX: جلب المناهج داخل دورة
        // =====================================================
        [HttpGet]
        public IActionResult GetCurriculumsByCourse(int courseId)
        {
            var curriculums = _context.CourseCurriculums
                .Where(cc => cc.CourseId == courseId)
                .Select(cc => new
                {
                    cc.Curriculum.Id,
                    cc.Curriculum.Title
                })
                .OrderBy(x => x.Title)
                .ToList();

            return Json(curriculums);
        }

        // =====================================================
        // AJAX: جلب المحاور داخل منهج
        // =====================================================
        [HttpGet]
        public IActionResult GetSectionsByCurriculum(int curriculumId)
        {
            var sections = _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new
                {
                    s.Id,
                    s.Title
                })
                .OrderBy(s => s.Title)
                .ToList();

            return Json(sections);
        }

        // =====================================================
        // STEP 2: توليد مؤقت + معاينة (Preview)
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Preview(GenerateExamRequestVM request)
        {
            if (!ModelState.IsValid)
                return View("Generate", request);

            GeneratedExamResult generated;
            try
            {
                generated = _examService.GeneratePreview(request);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Generate", request);
            }

            TempData["GeneratedExam"] =
                System.Text.Json.JsonSerializer.Serialize(generated);

            return View("PreviewGenerated", generated);
        }

        // =====================================================
        // STEP 3: حفظ المسودة فعليًا
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveDraft()
        {
            if (!TempData.ContainsKey("GeneratedExam"))
                return RedirectToAction(nameof(Generate));

            var generatedJson = TempData["GeneratedExam"]!.ToString()!;
            var generated = System.Text.Json.JsonSerializer
                .Deserialize<GeneratedExamResult>(generatedJson)!;

            var draftId = _examService.SaveDraft(generated, ActivePartnerId);

            return RedirectToAction(nameof(PreviewDraft), new { draftId });
        }

        // =====================================================
        // STEP 4: عرض المسودات
        // =====================================================
        [HttpGet]
        public IActionResult Drafts()
        {
            var drafts = _context.ExamDrafts
                .Where(d => d.PartnerId == ActivePartnerId && !d.IsArchived)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new ExamDraftListVM
                {
                    DraftId = d.Id,
                    Title = d.Title,
                    CreatedAt = d.CreatedAt,
                    QuestionsCount = d.DraftQuestions.Count
                })
                .ToList();

            return View(drafts);
        }

        // =====================================================
        // STEP 5: معاينة مسودة محفوظة
        // =====================================================
        [HttpGet]
        public IActionResult PreviewDraft(int draftId)
        {
            var model = _examService.GetDraftForPreview(draftId);
            return View(model);
        }



        [HttpGet]
        public IActionResult AddQuestion(int draftId, int lessonId)
        {
            var model = _examService.GetAddQuestionCandidates(draftId, lessonId);
            return View(model);
        }


        // =====================================================
        // STEP 6: تجهيز الإرسال
        // =====================================================
        [HttpGet]
        public IActionResult SendDraft(int draftId)
        {
            var vm = _examService.PrepareSendDraftVM(draftId, ActivePartnerId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendDraft(SendExamDraftVM model)
        {
            _examService.SendDraftToBatch(model, ActivePartnerId);
            return RedirectToAction(nameof(Drafts));
        }

        // =====================================================
        // STEP 8: استبدال سؤال داخل المسودة
        // =====================================================
        [HttpGet]
        public IActionResult ReplaceQuestion(
          int draftId,
          Guid oldQuestionId,
          int lessonId)
        {
            var model = _examService.GetReplaceCandidates(
                draftId,
                oldQuestionId,
                lessonId);

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmReplaceQuestion(
      int draftId,
      Guid oldQuestionId,
      Guid newQuestionId)
        {
            _examService.ReplaceQuestion(
                draftId,
                oldQuestionId,
                newQuestionId);

            return RedirectToAction(
                nameof(PreviewDraft),
                new { draftId });
        }

        // =====================================================
        // 🗑 حذف اختبار مُرسل للدفعة
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSentExam(int examAssignmentId, bool forceDelete = false)
        {
            if (examAssignmentId <= 0)
                return BadRequest();

            // =====================================================
            // 1️⃣ التحقق من ملكية الشريك
            // =====================================================
            var assignment = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x =>
                    x.Id == examAssignmentId &&
                    x.Batch.Branch.PartnerId == ActivePartnerId)
                .Select(x => new
                {
                    x.Id
                })
                .FirstOrDefaultAsync();

            if (assignment == null)
                return Forbid();

            // =====================================================
            // 2️⃣ حساب التفاعل
            // =====================================================
            var studentsStartedCount = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.ExamAssignmentId == examAssignmentId)
                .Select(a => a.StudentId)
                .Distinct()
                .CountAsync();

            var studentsSubmittedCount = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Where(s =>
                    s.ExamAssignmentId == examAssignmentId &&
                    s.IsSubmitted)
                .CountAsync();

            // =====================================================
            // 3️⃣ عرض التحذير إن وجد نشاط
            // =====================================================
            if (!forceDelete && (studentsStartedCount > 0 || studentsSubmittedCount > 0))
            {
                var vm = new DeleteExamWarningVm
                {
                    ExamAssignmentId = examAssignmentId,
                    StudentsStarted = studentsStartedCount,
                    StudentsSubmitted = studentsSubmittedCount
                };

                return View("DeleteExamWarning", vm);
            }

            // =====================================================
            // 4️⃣ حذف فعلي داخل ExecutionStrategy (Azure Safe)
            // =====================================================
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    // حذف محاولات الأسئلة
                    await _context.QuestionAttemptNew
                        .Where(a => a.ExamAssignmentId == examAssignmentId)
                        .ExecuteDeleteAsync();

                    // حذف حالات الطلاب
                    await _context.ExamStudentStatuses
                        .Where(s => s.ExamAssignmentId == examAssignmentId)
                        .ExecuteDeleteAsync();

                    // حذف أسئلة الاختبار المرتبطة
                    await _context.ExamQuestions
                        .Where(q => q.ExamAssignmentId == examAssignmentId)
                        .ExecuteDeleteAsync();

                    // حذف Assignment نفسه
                    var deleted = await _context.ExamAssignmentsToBatches
                        .Where(x => x.Id == examAssignmentId)
                        .ExecuteDeleteAsync();

                    if (deleted == 0)
                        throw new Exception("ExamAssignment not found.");

                    await transaction.CommitAsync();
                });

                TempData["Success"] = "تم حذف الاختبار وجميع بيانات الطلاب المرتبطة به.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "فشل الحذف: " + ex.GetBaseException().Message;
            }

            return RedirectToAction(nameof(SentExams));
        }



        // =====================================================
        // 🗑 حذف مسودة اختبار
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDraft(int draftId)
        {
            if (draftId <= 0)
                return BadRequest();

            // =====================================================
            // 1️⃣ التحقق من أن المسودة تخص الشريك
            // =====================================================
            var draft = await _context.ExamDrafts
                .AsNoTracking()
                .Where(d =>
                    d.Id == draftId &&
                    d.PartnerId == ActivePartnerId)
                .Select(d => new
                {
                    d.Id,
                    d.IsArchived
                })
                .FirstOrDefaultAsync();

            if (draft == null)
                return Forbid();

            // =====================================================
            // 2️⃣ منع حذف مسودة مؤرشفة
            // =====================================================
            if (draft.IsArchived)
            {
                TempData["Error"] = "لا يمكن حذف مسودة مؤرشفة.";
                return RedirectToAction(nameof(Drafts));
            }

            // =====================================================
            // 3️⃣ حذف آمن (Azure Safe)
            // =====================================================
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync();

                    // حذف أسئلة المسودة
                    await _context.ExamDraftQuestions
                        .Where(q => q.ExamDraftId == draftId)
                        .ExecuteDeleteAsync();

                    // حذف المسودة نفسها
                    await _context.ExamDrafts
                        .Where(d => d.Id == draftId)
                        .ExecuteDeleteAsync();

                    await transaction.CommitAsync();
                });

                TempData["Success"] = "تم حذف مسودة الاختبار بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "فشل الحذف: " + ex.GetBaseException().Message;
            }

            return RedirectToAction(nameof(Drafts));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmAddQuestion(int draftId, Guid questionId)
        {
            _examService.AddQuestionToDraft(draftId, questionId);
            return RedirectToAction(nameof(PreviewDraft), new { draftId });
        }



    }
}
