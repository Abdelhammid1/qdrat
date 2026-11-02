using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Exams.Placement
{
    // PPL-S2: منطق Admin/PlacementExams منقول كما هو، مع فلترة النطاق.
    // نص الرسائل ومفاتيح الكاش لنطاق الأدمن مطابق لما كان في الـ Controller.
    public sealed class PlacementExamManagementService : IPlacementExamManagementService
    {
        private const string NotInContractMessage = "هذه الخدمة غير مشمولة في عقدكم الحالي أو أن العقد غير نشط.";
        private const string PartnerUnexpectedErrorMessage = "تعذر تنفيذ العملية، حاول مرة أخرى.";

        private readonly ApplicationDbContext _context;
        private readonly IPlacementExamGeneratorService _placementExamService;
        private readonly IPlacementExamScopeGuard _guard;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PlacementExamManagementService> _logger;

        public PlacementExamManagementService(
            ApplicationDbContext context,
            IPlacementExamGeneratorService placementExamService,
            IPlacementExamScopeGuard guard,
            ITimeZoneService timeZoneService,
            IMemoryCache cache,
            ILogger<PlacementExamManagementService> logger)
        {
            _context = context;
            _placementExamService = placementExamService;
            _guard = guard;
            _timeZoneService = timeZoneService;
            _cache = cache;
            _logger = logger;
        }

        // خطأ عمل متوقع رسالته آمنة للعرض (يُرمى داخل ExecutionStrategy لإلغاء الـ Transaction)
        private sealed class PlacementRuleException : Exception
        {
            public PlacementRuleException(string message) : base(message) { }
        }

        // =====================================================
        // Index / BatchExams
        // =====================================================
        public async Task<PlacementExamsIndexVM> GetIndexAsync(PlacementScope scope, CancellationToken ct = default)
        {
            var query = _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Include(x => x.Batch)
                .Include(x => x.Exam)
                .Where(x => x.Exam != null && x.Exam.Type == ExamType.LevelAssessment && x.Exam.IsActive);

            if (scope.IsPartner)
            {
                var partnerId = scope.PartnerId!.Value;
                query = query.Where(x => x.Exam.OwnerPartnerId == partnerId && x.Batch.Branch.PartnerId == partnerId);
            }
            else
            {
                query = query.Where(x => x.Exam.OwnerPartnerId == null);
            }

            // الدفعات النشطة (غير مؤرشفة): على الأقل اختبار واحد غير مؤرشف
            var allAssignments = await query.ToListAsync(ct);

            var activeBatches = allAssignments
                .Where(x => !x.IsArchived)
                .GroupBy(x => new { x.BatchId, x.Batch?.Name })
                .Select(g => new PlacementBatchExamVm
                {
                    BatchId = g.Key.BatchId,
                    BatchName = g.Key.Name ?? string.Empty,
                    ExamsCount = g.Count(),
                    LastExamDate = g.Max(x => x.CreatedAt)
                })
                .OrderByDescending(x => x.LastExamDate)
                .ToList();

            // الدفعات المؤرشفة: أي دفعة لها تعيينات مؤرشفة
            var archivedBatchIds = allAssignments
                .Where(x => x.IsArchived)
                .Select(x => x.BatchId)
                .Distinct()
                .ToHashSet();

            // قائمة الدفعات لمدير الأرشيف (الدفعات النشطة فقط)
            var allBatchesForArchive = activeBatches
                .Select(b => new SelectListItem
                {
                    Value = b.BatchId.ToString(),
                    Text = b.BatchName
                })
                .ToList();

            return new PlacementExamsIndexVM
            {
                ActiveBatches = activeBatches,
                ArchivedBatchCount = archivedBatchIds.Count,
                AllBatchesForArchive = allBatchesForArchive
            };
        }

        public async Task<List<PlacementExamListVm>?> GetBatchExamsAsync(PlacementScope scope, int batchId, CancellationToken ct = default)
        {
            if (!await _guard.CanAccessBatchAsync(_context, scope, batchId, ct))
                return null;

            var scopedExams = _guard.PlacementExams(_context, scope);
            var enrollments = EnrollmentsInScope(scope);

            // ✅ اجلب كل الاختبارات المرتبطة بهذه الدفعة فقط من ExamAssignmentToBatch
            return await (
                from eab in _context.ExamAssignmentsToBatches
                join e in scopedExams on eab.ExamId equals e.Id
                where eab.BatchId == batchId
                select new PlacementExamListVm
                {
                    ExamId = e.Id,
                    Title = e.Title,
                    TotalQuestions = e.TotalQuestions,
                    CreatedAt = e.CreatedAt,
                    BatchId = batchId,
                    StudentCount = enrollments
                        .Count(s => s.BatchId == batchId),
                    TestedCount = _context.ExamStudentStatuses
                        .Count(s => s.ExamId == e.Id
                            && s.ExamAssignment != null
                            && s.ExamAssignment.BatchId == batchId
                            && s.IsSubmitted),
                    IsActive = e.IsActive,
                    ReferenceCode = e.ReferenceCode
                }
            )
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        }

        public Task<string?> GetBatchNameAsync(PlacementScope scope, int batchId, CancellationToken ct = default)
            => _guard.Batches(_context, scope)
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct);

        // =====================================================
        // CreateAdvanced
        // =====================================================
        public async Task<PlacementExamCreateAdvancedViewModel> GetCreateAdvancedAsync(PlacementScope scope, CancellationToken ct = default)
        {
            var now = _timeZoneService.GetNowSaudi();

            var model = new PlacementExamCreateAdvancedViewModel
            {
                DurationMinutes = 60,
                TotalQuestions = 60,
                PassingScore = 60,
                StartDate = now,
                EndDate = now.AddHours(2),

                Students = await _guard.Students(_context, scope)
                    .Select(s => new SelectListItem
                    {
                        Value = s.StudentID.ToString(),
                        Text = s.FullName
                    }).ToListAsync(ct),

                Courses = await BankCoursesInScope(scope)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Name
                    }).ToListAsync(ct),

                Batches = await _guard.Batches(_context, scope)
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name
                    }).ToListAsync(ct)
            };

            return model;
        }

        public async Task ReloadCreateAdvancedLookupsAsync(PlacementScope scope, PlacementExamCreateAdvancedViewModel model, CancellationToken ct = default)
        {
            model.Students = await _guard.Students(_context, scope)
                .OrderBy(s => s.FullName)
                .Select(s => new SelectListItem
                {
                    Value = s.StudentID.ToString(),
                    Text = s.FullName
                }).ToListAsync(ct);

            model.Batches = await _guard.Batches(_context, scope)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync(ct);

            model.Courses = await BankCoursesInScope(scope)
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToListAsync(ct);
        }

        public async Task<PlacementOperationResult> CreateAdvancedAsync(PlacementScope scope, PlacementExamCreateAdvancedViewModel model, CancellationToken ct = default)
        {
            try
            {
                model.SectionSelections = model.SectionSelections?
                    .Where(x => x != null)
                    .ToList() ?? new List<PlacementExamSectionSelectionVm>();

                if (!model.SectionSelections.Any())
                    return PlacementOperationResult.Fail("⚠️ لم يتم استقبال أي محور.");

                var selectedSections = model.SectionSelections
                    .Where(x => x.QuestionCount > 0)
                    .ToList();

                if (!selectedSections.Any())
                    return PlacementOperationResult.Fail("⚠️ يجب اختيار محور واحد على الأقل.");

                int sum = selectedSections.Sum(x => x.QuestionCount);
                if (sum != model.TotalQuestions)
                    return PlacementOperationResult.Fail($"⚠️ مجموع الأسئلة ({sum}) لا يساوي المطلوب ({model.TotalQuestions}).");

                if (scope.IsPartner)
                {
                    var denied = await ValidatePartnerCreateAdvancedAsync(scope, model, selectedSections, ct);
                    if (denied != null)
                        return denied;
                }

                int examId;

                // حالة الطالب الفردي
                if (model.StudentId.HasValue && model.StudentId > 0)
                {
                    examId = await _placementExamService.GeneratePlacementTestFromSectionsAsync(
                        model.StudentId.Value,
                        model.CourseId,
                        selectedSections,
                        model.TotalQuestions,
                        model.DurationMinutes,
                        model.StartDate,
                        model.EndDate,
                        scope.PartnerId
                    );

                    await ApplyPlacementExamDeliveryModeAsync(examId, model.IsInLab, model.IsRandomized, ct);
                }
                else if (model.BatchId.HasValue && model.BatchId > 0)
                {
                    // حالة الدفعة
                    var students = await EnrollmentsInScope(scope)
                        .Where(s => s.BatchId == model.BatchId.Value)
                        .Select(s => s.StudentID)
                        .ToListAsync(ct);

                    if (!students.Any())
                        return PlacementOperationResult.Fail("⚠️ لا يوجد طلاب داخل الدفعة.");

                    // 1) إنشاء الاختبار مرة واحدة فقط
                    examId = await _placementExamService.GeneratePlacementTestFromSectionsAsync(
                        students.First(),
                        model.CourseId,
                        selectedSections,
                        model.TotalQuestions,
                        model.DurationMinutes,
                        model.StartDate,
                        model.EndDate,
                        scope.PartnerId
                    );

                    await ApplyPlacementExamDeliveryModeAsync(examId, model.IsInLab, model.IsRandomized, ct);

                    // 2) تسجيله للدفعة
                    var nowUtc = DateTime.UtcNow;

                    _context.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch
                    {
                        ExamId = examId,
                        BatchId = model.BatchId.Value,
                        CreatedAt = nowUtc,
                        TotalQuestions = model.TotalQuestions,
                        DurationMinutes = model.DurationMinutes,
                        Title = "اختبار تحديد المستوى",
                        IsSentToStudents = false, // مسودة - لم يتم الإرسال بعد
                        IsOnline = !model.IsInLab,
                        IsInLab = model.IsInLab
                    });

                    await _context.SaveChangesAsync(ct);
                }
                else
                {
                    return PlacementOperationResult.Fail("⚠️ يجب اختيار طالب أو دفعة.");
                }

                return PlacementOperationResult.Ok("تم إنشاء الاختبار كمسودة. يرجى مراجعته قبل الإرسال.", examId);
            }
            catch (Exception ex)
            {
                return UnexpectedFailure(scope, ex, $"❌ حدث خطأ: {ex.Message}");
            }
        }

        private async Task<PlacementOperationResult?> ValidatePartnerCreateAdvancedAsync(
            PlacementScope scope,
            PlacementExamCreateAdvancedViewModel model,
            List<PlacementExamSectionSelectionVm> selectedSections,
            CancellationToken ct)
        {
            if (!await HasActivePlacementContractAsync(scope, ct))
                return PlacementOperationResult.Fail(NotInContractMessage);

            if (!await BankCoursesInScope(scope).AnyAsync(c => c.Id == model.CourseId, ct))
                return PlacementOperationResult.Fail("هذه الدورة غير متاحة في عقدكم.");

            if (model.StudentId.HasValue && model.StudentId > 0)
            {
                if (!await _guard.CanAccessStudentAsync(_context, scope, model.StudentId.Value, ct))
                    return PlacementOperationResult.NotFound();
            }
            else if (model.BatchId.HasValue && model.BatchId > 0)
            {
                var batchId = model.BatchId.Value;
                if (!await _guard.Batches(_context, scope).AnyAsync(b => b.Id == batchId && b.CourseId == model.CourseId, ct))
                    return PlacementOperationResult.NotFound();
            }

            // المحاور يجب أن تتبع الدورة المختارة (فلترة داخل الذاكرة — توافق SQL 2014)
            var courseSectionIds = await (
                from cc in _context.CourseCurriculums
                join s in _context.Sections on cc.CurriculumId equals s.CurriculumId
                where cc.CourseId == model.CourseId
                select s.Id
            ).ToListAsync(ct);

            return selectedSections.All(x => courseSectionIds.Contains(x.SectionId))
                ? null
                : PlacementOperationResult.NotFound();
        }

        // =====================================================
        // CreateFromProfessionalModel
        // =====================================================
        public async Task<PlacementExamFromModelVm> GetCreateFromModelAsync(PlacementScope scope, CancellationToken ct = default)
        {
            var vm = new PlacementExamFromModelVm();
            await ReloadCreateFromModelLookupsAsync(scope, vm, ct);
            return vm;
        }

        public async Task ReloadCreateFromModelLookupsAsync(PlacementScope scope, PlacementExamFromModelVm vm, CancellationToken ct = default)
        {
            vm.AvailableModels = await ModelsInScope(scope)
                .AsNoTracking()
                .OrderBy(x => x.Title)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Title
                }).ToListAsync(ct);

            vm.AvailableBatches = await _guard.Batches(_context, scope)
                .AsNoTracking()
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync(ct);

            vm.AvailableCourses = await CoursesInScope(scope)
                .AsNoTracking()
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToListAsync(ct);
        }

        public async Task<PlacementOperationResult> CreateFromProfessionalModelAsync(PlacementScope scope, PlacementExamFromModelVm vm, CancellationToken ct = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(ct);

                    // =========================================
                    // Validation
                    // =========================================
                    if (vm.SelectedModelId <= 0)
                        throw new PlacementRuleException("⚠️ يجب اختيار نموذج.");

                    if (vm.SelectedBatchId <= 0)
                        throw new PlacementRuleException("⚠️ يجب اختيار دفعة.");

                    if (scope.IsPartner)
                    {
                        if (!await HasActivePlacementContractAsync(scope, ct))
                            throw new PlacementRuleException(NotInContractMessage);

                        // الدفعة والدورة يجب أن تكونا للشريك ومتوافقتين
                        var batchMatchesCourse = await _guard.Batches(_context, scope)
                            .AnyAsync(b => b.Id == vm.SelectedBatchId && b.CourseId == vm.SelectedCourseId, ct);

                        if (!batchMatchesCourse || !await CoursesInScope(scope).AnyAsync(c => c.Id == vm.SelectedCourseId, ct))
                            throw new PlacementRuleException("غير مسموح");
                    }

                    // =========================================
                    // تحميل النموذج
                    // =========================================
                    var model = await ModelsInScope(scope)
                        .Include(m => m.Questions)
                        .FirstOrDefaultAsync(m => m.Id == vm.SelectedModelId, ct);

                    if (model == null || !model.Questions.Any())
                        throw new PlacementRuleException("⚠️ النموذج غير موجود أو لا يحتوي على أسئلة.");

                    // =========================================
                    // الطلاب
                    // =========================================
                    var studentIds = await EnrollmentsInScope(scope)
                        .Where(x => x.BatchId == vm.SelectedBatchId)
                        .Select(x => x.StudentID)
                        .Distinct()
                        .ToListAsync(ct);

                    if (!studentIds.Any())
                        throw new PlacementRuleException("⚠️ لا يوجد طلاب في هذه الدفعة.");

                    var nowUtc = DateTime.UtcNow;

                    // =========================================
                    // إنشاء الاختبار
                    // =========================================
                    var exam = new Exam
                    {
                        Title = $"اختبار تحديد مستوى - {model.Title}",
                        Type = ExamType.LevelAssessment,
                        CourseId = vm.SelectedCourseId,
                        CreatedAt = nowUtc,
                        DurationMinutes = vm.DurationMinutes,
                        TotalQuestions = model.Questions.Count,
                        IsActive = true,
                        IsFromProfessionalModel = true,
                        OwnerPartnerId = scope.PartnerId
                    };

                    _context.Exams.Add(exam);
                    await _context.SaveChangesAsync(ct);

                    // =========================================
                    // Bulk Questions
                    // =========================================
                    var examQuestions = model.Questions
                        .OrderBy(q => q.OrderNumber)
                        .ThenBy(q => q.Id)
                        .Select((q, i) => new ExamQuestion
                        {
                            ExamId = exam.Id,
                            QuestionId = q.QuestionId ?? Guid.Empty,
                            Order = i + 1
                        }).ToList();

                    await _context.BulkInsertAsync(examQuestions);

                    // =========================================
                    // ربط الدفعة + إرسال
                    // =========================================
                    var batchAssignment = new ExamAssignmentToBatch
                    {
                        ExamId = exam.Id,
                        BatchId = vm.SelectedBatchId,
                        Title = exam.Title,
                        TotalQuestions = exam.TotalQuestions,
                        DurationMinutes = exam.DurationMinutes,
                        CreatedAt = nowUtc,
                        IsSentToStudents = true // 🔥 تم الإرسال
                    };

                    _context.ExamAssignmentsToBatches.Add(batchAssignment);
                    await _context.SaveChangesAsync(ct);

                    // =========================================
                    // Bulk Assignments (الإرسال الفعلي)
                    // =========================================
                    var assignments = new List<ExamAssignment>(studentIds.Count);
                    var statuses = new List<ExamStudentStatus>(studentIds.Count);

                    foreach (var stId in studentIds)
                    {
                        assignments.Add(new ExamAssignment
                        {
                            ExamId = exam.Id,
                            StudentId = stId,
                            AssignedAt = nowUtc,
                            DueDate = nowUtc.AddMinutes(exam.DurationMinutes)
                        });

                        statuses.Add(new ExamStudentStatus
                        {
                            StudentId = stId,
                            ExamId = exam.Id,
                            AssignedAt = nowUtc,
                            Status = ExamStatus.Pending
                        });
                    }

                    await _context.BulkInsertAsync(assignments);
                    await _context.BulkInsertAsync(statuses);

                    await transaction.CommitAsync(ct);
                });

                return PlacementOperationResult.Ok("✅ تم إنشاء وإرسال الاختبار بنجاح.");
            }
            catch (PlacementRuleException ex)
            {
                return PlacementOperationResult.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                return UnexpectedFailure(scope, ex, ex.Message);
            }
        }

        // =====================================================
        // Review / ToggleDontKnowOption
        // =====================================================
        public async Task<PlacementExamReviewVm?> GetReviewAsync(PlacementScope scope, int examId, CancellationToken ct = default)
        {
            var exam = await _guard.ExamsInScope(_context, scope)
                .Include(e => e.Questions)
                    .ThenInclude(eq => eq.Question)
                        .ThenInclude(q => q.Lesson)
                            .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(e => e.Id == examId, ct);

            if (exam == null)
                return null;

            return new PlacementExamReviewVm
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                TotalQuestions = exam.TotalQuestions,
                DurationMinutes = exam.DurationMinutes,
                CreatedAt = exam.CreatedAt,
                IsInLab = !string.IsNullOrWhiteSpace(exam.ReferenceCode),
                ReferenceCode = exam.ReferenceCode,
                AllowDontKnowOption = exam.AllowDontKnowOption,
                Sections = exam.Questions
                    .GroupBy(q => q.Question.Lesson.Section)
                    .Select(g => new PlacementExamReviewSectionVm
                    {
                        SectionId = g.Key.Id,
                        SectionTitle = g.Key.Title,
                        Questions = g.Select(x => new PlacementExamReviewQuestionVm
                        {
                            QuestionId = x.QuestionId,
                            Title = x.Question.Title,
                            ImageUrl = x.Question.ImageUrl,
                            Difficulty = x.Question.Difficulty,
                            LessonTitle = x.Question.Lesson.Title,
                            CorrectAnswer = x.Question.CorrectAnswer,
                            Order = x.Order
                        }).ToList()
                    }).ToList()
            };
        }

        public async Task<PlacementOperationResult> ToggleDontKnowOptionAsync(PlacementScope scope, int examId, bool enabled, CancellationToken ct = default)
        {
            if (!await CanModifyInScopeAsync(scope, ct))
                return PlacementOperationResult.Fail(NotInContractMessage);

            var exam = await _guard.ExamsInScope(_context, scope).FirstOrDefaultAsync(e => e.Id == examId, ct);

            if (exam == null)
                return PlacementOperationResult.Fail("⚠️ لم يتم العثور على الاختبار.");

            exam.AllowDontKnowOption = enabled;
            await _context.SaveChangesAsync(ct);

            return PlacementOperationResult.Ok(enabled
                ? "✅ تم تفعيل خيار \"لا أعرف الإجابة\" لهذا الاختبار."
                : "✅ تم إلغاء تفعيل خيار \"لا أعرف الإجابة\" لهذا الاختبار.", examId);
        }

        // =====================================================
        // ReplaceQuestion
        // =====================================================
        public async Task<ReplaceQuestionVm?> GetReplaceQuestionAsync(PlacementScope scope, int examId, Guid questionId, CancellationToken ct = default)
        {
            var exam = await _guard.ExamsInScope(_context, scope)
                .Where(e => e.Id == examId)
                .Select(e => new { e.CourseId })
                .FirstOrDefaultAsync(ct);

            if (exam == null)
                return null;

            if (scope.IsPartner && !await CanUseBankForExamCourseAsync(scope, exam.CourseId, ct))
                return null;

            // 1️⃣ السؤال الحالي داخل الاختبار
            var examQuestion = await _context.ExamQuestions
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                .FirstOrDefaultAsync(eq =>
                    eq.ExamId == examId &&
                    eq.QuestionId == questionId, ct);

            if (examQuestion == null)
                return null;

            var currentQuestion = examQuestion.Question;

            // 2️⃣ جلب الأسئلة البديلة (نفس المؤشر + نفس الصعوبة)
            var candidates = await _context.Questions
                .Where(q =>
                    q.Id != currentQuestion.Id &&
                    q.LessonId == currentQuestion.LessonId &&
                    q.Difficulty == currentQuestion.Difficulty &&
                    q.IsReviewed &&
                    q.IsComplete &&
                    !q.IsRejected)
                .OrderByDescending(q => q.CreatedAt)
                .Take(20)
                .Select(q => new ReplaceQuestionCandidateVm
                {
                    QuestionId = q.Id,
                    Title = q.Title,
                    ImageUrl = q.ImageUrl
                })
                .ToListAsync(ct);

            return new ReplaceQuestionVm
            {
                ExamId = examId,
                OldQuestionId = questionId,
                LessonTitle = currentQuestion.Lesson.Title,
                Difficulty = currentQuestion.Difficulty.ToString(),
                Candidates = candidates
            };
        }

        public async Task<PlacementOperationResult> ReplaceQuestionAsync(PlacementScope scope, int examId, Guid oldQuestionId, Guid newQuestionId, CancellationToken ct = default)
        {
            var exam = await _guard.ExamsInScope(_context, scope)
                .Where(e => e.Id == examId)
                .Select(e => new { e.CourseId })
                .FirstOrDefaultAsync(ct);

            if (exam == null)
                return PlacementOperationResult.NotFound();

            // 1️⃣ جلب السؤال القديم داخل الاختبار
            var examQuestion = await _context.ExamQuestions
                .FirstOrDefaultAsync(eq =>
                    eq.ExamId == examId &&
                    eq.QuestionId == oldQuestionId, ct);

            if (examQuestion == null)
                return PlacementOperationResult.NotFound();

            if (scope.IsPartner)
            {
                if (!await CanModifyInScopeAsync(scope, ct) || !await CanUseBankForExamCourseAsync(scope, exam.CourseId, ct))
                    return PlacementOperationResult.Fail(NotInContractMessage);

                if (await IsExamSentAsync(examId, ct))
                    return PlacementOperationResult.BadRequest("لا يمكن تعديل اختبار تم إرساله.");

                // البديل من نفس المؤشر والصعوبة، كما تعرضه صفحة الاختيار
                var isValidCandidate = await (
                    from oldQ in _context.Questions
                    from newQ in _context.Questions
                    where oldQ.Id == oldQuestionId && newQ.Id == newQuestionId &&
                          newQ.LessonId == oldQ.LessonId &&
                          newQ.Difficulty == oldQ.Difficulty &&
                          newQ.IsReviewed && newQ.IsComplete && !newQ.IsRejected
                    select newQ.Id
                ).AnyAsync(ct);

                if (!isValidCandidate)
                    return PlacementOperationResult.NotFound();
            }

            // 2️⃣ منع التكرار
            bool alreadyExists = await _context.ExamQuestions
                .AnyAsync(eq =>
                    eq.ExamId == examId &&
                    eq.QuestionId == newQuestionId, ct);

            if (alreadyExists)
                return PlacementOperationResult.Fail("⚠️ هذا السؤال موجود بالفعل داخل الاختبار.");

            // 3️⃣ الاستبدال
            examQuestion.QuestionId = newQuestionId;
            examQuestion.IsManuallySelected = true;

            await _context.SaveChangesAsync(ct);

            return PlacementOperationResult.Ok("تم استبدال السؤال بنجاح.", examId);
        }

        // =====================================================
        // AddQuestionFromBank
        // =====================================================
        public async Task<(PlacementOperationResult Result, AddQuestionFromBankVm? Model)> GetAddQuestionFromBankAsync(PlacementScope scope, int examId, int sectionId, CancellationToken ct = default)
        {
            var exam = await _guard.ExamsInScope(_context, scope)
                .Where(e => e.Id == examId)
                .Select(e => new { e.CourseId })
                .FirstOrDefaultAsync(ct);

            if (exam == null)
                return (PlacementOperationResult.NotFound(), null);

            // 🔒 تأكيد أنه Draft
            if (await IsExamSentAsync(examId, ct))
                return (PlacementOperationResult.BadRequest("لا يمكن تعديل اختبار تم إرساله."), null);

            if (scope.IsPartner &&
                (!await CanUseBankForExamCourseAsync(scope, exam.CourseId, ct) ||
                 !await IsSectionInCourseAsync(sectionId, exam.CourseId, ct)))
                return (PlacementOperationResult.NotFound(), null);

            // الأسئلة الموجودة بالفعل في الاختبار
            var existingQuestionIds = await _context.ExamQuestions
                .Where(eq => eq.ExamId == examId)
                .Select(eq => eq.QuestionId)
                .ToListAsync(ct);

            // أسئلة البنك (نفس المحور + غير مستخدمة)
            var questions = await _context.Questions
                .Where(q =>
                    q.SectionId == sectionId &&
                    q.IsReviewed &&
                    q.IsComplete &&
                    !q.IsRejected)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new AddQuestionFromBankItemVm
                {
                    QuestionId = q.Id,
                    Title = q.Title,
                    ImageUrl = q.ImageUrl,
                    IsAlreadyAdded = false
                })
                .ToListAsync(ct);

            // فلترة داخل الذاكرة (توافق SQL 2014)
            questions = questions
                .Where(q => !existingQuestionIds.Contains(q.QuestionId))
                .ToList();

            var model = new AddQuestionFromBankVm
            {
                ExamId = examId,
                SectionId = sectionId,
                SectionTitle = await _context.Sections
                    .Where(s => s.Id == sectionId)
                    .Select(s => s.Title)
                    .FirstOrDefaultAsync(ct),
                Questions = questions
            };

            return (PlacementOperationResult.Ok(string.Empty, examId), model);
        }

        public async Task<PlacementOperationResult> AddQuestionFromBankAsync(PlacementScope scope, int examId, int sectionId, Guid questionId, CancellationToken ct = default)
        {
            var exam = await _guard.ExamsInScope(_context, scope)
                .Where(e => e.Id == examId)
                .Select(e => new { e.CourseId })
                .FirstOrDefaultAsync(ct);

            if (exam == null)
                return PlacementOperationResult.NotFound();

            // 🔒 منع التعديل بعد الإرسال
            if (await IsExamSentAsync(examId, ct))
                return PlacementOperationResult.BadRequest("لا يمكن تعديل اختبار تم إرساله.");

            if (scope.IsPartner)
            {
                if (!await CanModifyInScopeAsync(scope, ct) || !await CanUseBankForExamCourseAsync(scope, exam.CourseId, ct))
                    return PlacementOperationResult.Fail(NotInContractMessage);

                var isValidQuestion = await IsSectionInCourseAsync(sectionId, exam.CourseId, ct) &&
                    await _context.Questions.AnyAsync(q =>
                        q.Id == questionId &&
                        q.SectionId == sectionId &&
                        q.IsReviewed &&
                        q.IsComplete &&
                        !q.IsRejected, ct);

                if (!isValidQuestion)
                    return PlacementOperationResult.NotFound();
            }

            // منع التكرار
            bool exists = await _context.ExamQuestions
                .AnyAsync(eq => eq.ExamId == examId && eq.QuestionId == questionId, ct);

            if (exists)
                return PlacementOperationResult.Fail("هذا السؤال مضاف بالفعل.");

            // ترتيب السؤال الجديد
            int nextOrder = await _context.ExamQuestions
                .Where(eq => eq.ExamId == examId)
                .Select(eq => (int?)eq.Order)
                .MaxAsync(ct) ?? 0;

            _context.ExamQuestions.Add(new ExamQuestion
            {
                ExamId = examId,
                QuestionId = questionId,
                Order = nextOrder + 1,
                IsManuallySelected = true
            });

            await _context.SaveChangesAsync(ct);

            return PlacementOperationResult.Ok("تمت إضافة السؤال إلى الاختبار.", examId);
        }

        // =====================================================
        // الإرسال
        // =====================================================
        public async Task<PlacementOperationResult> SendToStudentsAsync(PlacementScope scope, int examId, CancellationToken ct = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(ct);

                    if (!await CanModifyInScopeAsync(scope, ct))
                        throw new PlacementRuleException(NotInContractMessage);

                    // =========================================
                    // 1️⃣ تحميل الاختبار
                    // =========================================
                    var exam = await _guard.ExamsInScope(_context, scope)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == examId, ct);

                    if (exam == null)
                        throw new PlacementRuleException("❌ الاختبار غير موجود.");

                    // =========================================
                    // 2️⃣ تحميل ربط الدفعات
                    // =========================================
                    var batchAssignments = await _context.ExamAssignmentsToBatches
                        .Where(x => x.ExamId == examId)
                        .ToListAsync(ct);

                    if (!batchAssignments.Any())
                        throw new PlacementRuleException("⚠️ لا يوجد ربط بالدفعات.");

                    if (batchAssignments.Any(x => x.IsSentToStudents))
                        throw new PlacementRuleException("⚠️ تم الإرسال مسبقًا.");

                    // =========================================
                    // 3️⃣ استخراج الطلاب
                    // =========================================
                    var batchIds = batchAssignments
                        .Select(x => x.BatchId)
                        .Distinct()
                        .ToList();

                    var students = await EnrollmentsInScope(scope)
                        .Where(s => batchIds.Contains(s.BatchId))
                        .Select(s => s.StudentID)
                        .Distinct()
                        .ToListAsync(ct);

                    if (!students.Any())
                        throw new PlacementRuleException("⚠️ لا يوجد طلاب.");

                    var nowUtc = DateTime.UtcNow;

                    // =========================================
                    // 4️⃣ تجهيز البيانات
                    // =========================================
                    var assignments = new List<ExamAssignment>(students.Count);
                    var statuses = new List<ExamStudentStatus>(students.Count);

                    foreach (var stId in students)
                    {
                        assignments.Add(new ExamAssignment
                        {
                            ExamId = examId,
                            StudentId = stId,
                            AssignedAt = nowUtc,
                            DueDate = nowUtc.AddMinutes(exam.DurationMinutes)
                        });

                        statuses.Add(new ExamStudentStatus
                        {
                            StudentId = stId,
                            ExamId = examId,
                            AssignedAt = nowUtc,
                            Status = ExamStatus.Pending
                        });
                    }

                    // =========================================
                    // 5️⃣ Bulk Insert
                    // =========================================
                    await _context.BulkInsertAsync(assignments);
                    await _context.BulkInsertAsync(statuses);

                    // =========================================
                    // 6️⃣ تحديث الحالة
                    // =========================================
                    foreach (var a in batchAssignments)
                        a.IsSentToStudents = true;

                    await _context.SaveChangesAsync(ct);

                    // =========================================
                    // 7️⃣ تنظيف الكاش
                    // =========================================
                    foreach (var stId in students)
                        _cache.Remove($"PlacementExamDashboard_{stId}");

                    await transaction.CommitAsync(ct);
                });

                return PlacementOperationResult.Ok("✅ تم إرسال الاختبار بنجاح.", examId);
            }
            catch (PlacementRuleException ex)
            {
                return PlacementOperationResult.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                return UnexpectedFailure(scope, ex, ex.Message);
            }
        }

        // ✅ إرسال الاختبار للطلاب الذين انضموا للدفعة بعد الإرسال الأول فقط
        // (لا يُنشئ لهم SendToStudents تعيينًا لأنه يُمنع تكرار إرسال الاختبار للدفعة بالكامل)
        public async Task<PlacementOperationResult> SendToNewStudentsAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default)
        {
            var exam = await _guard.ExamsInScope(_context, scope).FirstOrDefaultAsync(e => e.Id == examId, ct);
            if (exam == null)
                return PlacementOperationResult.Fail("❌ الاختبار غير موجود.");

            var denied = await ValidatePartnerExamBatchAsync(scope, examId, batchId, ct);
            if (denied != null)
                return denied;

            var batchStudentIds = await EnrollmentsInScope(scope)
                .Where(s => s.BatchId == batchId)
                .Select(s => s.StudentID)
                .Distinct()
                .ToListAsync(ct);

            var alreadyAssignedIds = await _context.ExamAssignments
                .Where(a => a.ExamId == examId && batchStudentIds.Contains(a.StudentId))
                .Select(a => a.StudentId)
                .Distinct()
                .ToListAsync(ct);

            var newStudentIds = batchStudentIds.Except(alreadyAssignedIds).ToList();

            if (!newStudentIds.Any())
                return PlacementOperationResult.Info("لا يوجد طلاب جدد بحاجة لإرسال الاختبار.");

            var nowUtc = DateTime.UtcNow;
            var assignments = new List<ExamAssignment>(newStudentIds.Count);
            var statuses = new List<ExamStudentStatus>(newStudentIds.Count);

            foreach (var stId in newStudentIds)
            {
                assignments.Add(new ExamAssignment
                {
                    ExamId = examId,
                    StudentId = stId,
                    AssignedAt = nowUtc,
                    DueDate = nowUtc.AddMinutes(exam.DurationMinutes)
                });

                statuses.Add(new ExamStudentStatus
                {
                    StudentId = stId,
                    ExamId = examId,
                    AssignedAt = nowUtc,
                    Status = ExamStatus.Pending
                });
            }

            await _context.BulkInsertAsync(assignments);
            await _context.BulkInsertAsync(statuses);

            foreach (var stId in newStudentIds)
                _cache.Remove($"PlacementExamDashboard_{stId}");

            return PlacementOperationResult.Ok($"✅ تم إرسال الاختبار لـ {newStudentIds.Count} طالب جديد انضم للدفعة.", examId, batchId);
        }

        // ✅ إصلاح بيانات الطلاب الذين أنهوا الاختبار فعليًا لكن بلا سجل تعيين/حالة
        // (بسبب النقص القديم في تهيئة ExamAssignments/ExamStudentStatuses عند إرسال اختبار الدفعة)
        // نبني السجلات الناقصة من محاولاتهم الفعلية المحفوظة في QuestionAttemptNew حتى تظهر تقاريرهم وتُطبع بشكل صحيح
        public async Task<PlacementOperationResult> BackfillMissingAssignmentsAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default)
        {
            var exam = await _guard.ExamsInScope(_context, scope).FirstOrDefaultAsync(e => e.Id == examId, ct);
            if (exam == null)
                return PlacementOperationResult.Fail("❌ الاختبار غير موجود.");

            var denied = await ValidatePartnerExamBatchAsync(scope, examId, batchId, ct);
            if (denied != null)
                return denied;

            var batchStudentIds = await EnrollmentsInScope(scope)
                .Where(s => s.BatchId == batchId)
                .Select(s => s.StudentID)
                .Distinct()
                .ToListAsync(ct);

            // الطلاب اللي عندهم محاولات فعلية على هذا الاختبار
            var studentsWithAttempts = await (
                from a in _context.QuestionAttemptNew
                where a.ExamId == examId && batchStudentIds.Contains(a.StudentId)
                group a by a.StudentId into g
                select new
                {
                    StudentId = g.Key,
                    FirstAttemptAt = g.Min(x => x.AttemptedAt),
                    LastAttemptAt = g.Max(x => x.AttemptedAt)
                }
            ).ToListAsync(ct);

            var alreadyAssignedIds = await _context.ExamAssignments
                .Where(a => a.ExamId == examId)
                .Select(a => a.StudentId)
                .Distinct()
                .ToListAsync(ct);

            var toFix = studentsWithAttempts
                .Where(s => !alreadyAssignedIds.Contains(s.StudentId))
                .ToList();

            if (!toFix.Any())
                return PlacementOperationResult.Info("لا يوجد طلاب بحاجة لإصلاح — كل من أنهى الاختبار لديه سجل تعيين بالفعل.");

            // 1) إنشاء سجل تعيين لكل طالب ناقص
            var newAssignments = new List<ExamAssignment>();
            foreach (var s in toFix)
            {
                var assignment = new ExamAssignment
                {
                    ExamId = examId,
                    StudentId = s.StudentId,
                    AssignedAt = s.FirstAttemptAt,
                    DueDate = s.LastAttemptAt
                };
                _context.ExamAssignments.Add(assignment);
                newAssignments.Add(assignment);
            }

            await _context.SaveChangesAsync(ct);

            // 2) إنشاء/تحديث سجل الحالة، وربط المحاولات القديمة برقم التعيين الجديد
            foreach (var assignment in newAssignments)
            {
                var info = toFix.First(x => x.StudentId == assignment.StudentId);

                var status = await _context.ExamStudentStatuses
                    .FirstOrDefaultAsync(st => st.StudentId == assignment.StudentId && st.ExamId == examId, ct);

                if (status == null)
                {
                    _context.ExamStudentStatuses.Add(new ExamStudentStatus
                    {
                        StudentId = assignment.StudentId,
                        ExamId = examId,
                        AssignedAt = info.FirstAttemptAt,
                        StartedAt = info.FirstAttemptAt,
                        SubmittedAt = info.LastAttemptAt,
                        IsSubmitted = true,
                        Status = ExamStatus.Completed
                    });
                }
                else
                {
                    status.IsSubmitted = true;
                    status.Status = ExamStatus.Completed;
                    status.SubmittedAt ??= info.LastAttemptAt;
                }

                var attemptsToLink = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == assignment.StudentId &&
                                a.ExamId == examId &&
                                a.ExamAssignmentId == null)
                    .ToListAsync(ct);

                foreach (var att in attemptsToLink)
                    att.ExamAssignmentId = assignment.Id;
            }

            await _context.SaveChangesAsync(ct);

            return PlacementOperationResult.Ok($"✅ تم إصلاح بيانات {toFix.Count} طالب. أصبح بإمكانك الآن عرض وطباعة تقاريرهم.", examId, batchId);
        }

        public async Task<PlacementOperationResult> ResendToStudentAsync(PlacementScope scope, int examId, int studentId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var scopedExams = _guard.ExamsInScope(_context, scope);

            // 1️⃣ جلب الدفعة المرتبطة بالاختبار
            var examBatch = await _context.ExamAssignmentsToBatches
                .Where(x => x.ExamId == examId && scopedExams.Any(e => e.Id == x.ExamId))
                .Select(x => x.BatchId)
                .FirstOrDefaultAsync(ct);

            if (examBatch == 0)
                return PlacementOperationResult.Fail("❌ لا يمكن تحديد الدفعة المرتبطة بالاختبار.");

            if (scope.IsPartner)
            {
                if (!await CanModifyInScopeAsync(scope, ct))
                    return PlacementOperationResult.Fail(NotInContractMessage);

                var eligibleStudent = await EnrollmentsInScope(scope).AnyAsync(s =>
                    s.StudentID == studentId &&
                    _context.ExamAssignmentsToBatches.Any(x => x.ExamId == examId && x.BatchId == s.BatchId), ct);

                if (!eligibleStudent)
                    return PlacementOperationResult.NotFound();
            }

            // 2️⃣ إعادة إنشاء Assignment
            _context.ExamAssignments.Add(new ExamAssignment
            {
                ExamId = examId,
                StudentId = studentId,
                AssignedAt = now,
                DueDate = now.AddMinutes(
                    await _context.Exams
                        .Where(e => e.Id == examId)
                        .Select(e => e.DurationMinutes)
                        .FirstAsync(ct)
                )
            });

            // 3️⃣ حذف الحالة السابقة
            var oldStatus = await _context.ExamStudentStatuses
                .Where(s => s.StudentId == studentId && s.ExamId == examId)
                .ToListAsync(ct);

            if (oldStatus.Any())
                _context.ExamStudentStatuses.RemoveRange(oldStatus);

            // 4️⃣ إضافة حالة جديدة
            _context.ExamStudentStatuses.Add(new ExamStudentStatus
            {
                StudentId = studentId,
                ExamId = examId,
                AssignedAt = now,
                Status = ExamStatus.Pending
            });

            // 5️⃣ حذف محاولات الطالب السابقة
            var oldAttempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamId == examId)
                .ToListAsync(ct);

            if (oldAttempts.Any())
                _context.QuestionAttemptNew.RemoveRange(oldAttempts);

            await _context.SaveChangesAsync(ct);

            return PlacementOperationResult.Ok("✅ تم إعادة إرسال الاختبار للطالب بنجاح.", examId, examBatch);
        }

        public async Task<PlacementOperationResult> ConfirmSendAsync(PlacementScope scope, int examId, CancellationToken ct = default)
        {
            if (!await CanModifyInScopeAsync(scope, ct))
                return PlacementOperationResult.Fail(NotInContractMessage);

            using var transaction = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                var scopedExams = _guard.ExamsInScope(_context, scope);
                var assignments = await _context.ExamAssignmentsToBatches
                    .Where(x => x.ExamId == examId && scopedExams.Any(e => e.Id == x.ExamId))
                    .ToListAsync(ct);

                if (!assignments.Any())
                    return PlacementOperationResult.Fail("⚠️ لا يوجد ربط بالدفعات.");

                // 🔴 منع إعادة الإرسال
                if (assignments.Any(x => x.IsSentToStudents))
                    return PlacementOperationResult.Info("⚠️ تم إرسال الاختبار بالفعل.");

                // ✅ تفعيل الإرسال
                foreach (var a in assignments)
                    a.IsSentToStudents = true;

                await _context.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);

                return PlacementOperationResult.Ok("✅ تم اعتماد وإرسال اختبار تحديد المستوى.", examId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                return UnexpectedFailure(scope, ex, ex.Message);
            }
        }

        // =====================================================
        // Hide / Restore / Delete
        // =====================================================
        public async Task<PlacementOperationResult> HideAsync(PlacementScope scope, int examId, CancellationToken ct = default)
        {
            var userName = scope.UserName ?? "Unknown";

            var exam = await _guard.ExamsInScope(_context, scope).FirstOrDefaultAsync(e => e.Id == examId, ct);
            if (exam == null)
                return PlacementOperationResult.Fail("⚠️ لم يتم العثور على الاختبار.");

            if (!exam.IsActive)
                return PlacementOperationResult.Info("⚠️ هذا الاختبار مخفي بالفعل.");

            // 🔒 إخفاء الاختبار
            exam.IsActive = false;

            // 🧾 سجل العملية
            _context.SystemLogs.Add(new SystemLog
            {
                UserName = userName,
                Action = "Hide",
                Entity = "Exam",
                EntityId = examId,
                Description = $"قام {userName} بإخفاء اختبار ({exam.Title})"
            });

            await _context.SaveChangesAsync(ct);

            return PlacementOperationResult.Ok("🚫 تم إخفاء الاختبار ولن يظهر للطلاب بعد الآن.", examId);
        }

        public async Task<PlacementOperationResult> RestoreAsync(PlacementScope scope, int examId, CancellationToken ct = default)
        {
            var userName = scope.UserName ?? "Unknown";

            var exam = await _guard.ExamsInScope(_context, scope).FirstOrDefaultAsync(e => e.Id == examId, ct);
            if (exam == null)
                return PlacementOperationResult.Fail("⚠️ لم يتم العثور على الاختبار.");

            if (exam.IsActive)
                return PlacementOperationResult.Info("✅ هذا الاختبار مفعل بالفعل.");

            exam.IsActive = true;

            _context.SystemLogs.Add(new SystemLog
            {
                UserName = userName,
                Action = "Restore",
                Entity = "Exam",
                EntityId = examId,
                Description = $"قام {userName} باسترجاع اختبار ({exam.Title})"
            });

            await _context.SaveChangesAsync(ct);

            return PlacementOperationResult.Ok("✅ تم استرجاع الاختبار بنجاح.", examId);
        }

        // ===================== حذف نهائي لاختبار من دفعة =====================
        public async Task<PlacementOperationResult> DeletePermanentlyAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct = default)
        {
            var userName = scope.UserName ?? "Unknown";

            try
            {
                var exam = await _guard.PlacementExams(_context, scope)
                    .FirstOrDefaultAsync(e => e.Id == examId, ct);

                if (exam == null)
                    return PlacementOperationResult.Fail("⚠️ لم يتم العثور على الاختبار.");

                var linkedToBatch = await _context.ExamAssignmentsToBatches
                    .AnyAsync(ea => ea.ExamId == examId && ea.BatchId == batchId, ct);

                if (!linkedToBatch)
                    return PlacementOperationResult.Fail("⚠️ هذا الاختبار غير مرتبط بهذه الدفعة.");

                var testedCount = await _context.ExamStudentStatuses
                    .CountAsync(s => s.ExamId == examId && s.IsSubmitted, ct);

                // ✅ حذف محاولات الأسئلة
                var questionAttempts = await _context.QuestionAttemptNew
                    .Where(a => a.ExamId == examId)
                    .ToListAsync(ct);
                _context.QuestionAttemptNew.RemoveRange(questionAttempts);

                // ✅ حذف حالات الطلاب
                var statuses = await _context.ExamStudentStatuses
                    .Where(s => s.ExamId == examId)
                    .ToListAsync(ct);
                _context.ExamStudentStatuses.RemoveRange(statuses);

                // ✅ حذف الأسئلة المرتبطة
                var questions = await _context.ExamQuestions
                    .Where(q => q.ExamId == examId)
                    .ToListAsync(ct);
                _context.ExamQuestions.RemoveRange(questions);

                // ✅ حذف توزيع الأسئلة (لجميع تعيينات هذا الاختبار على الدفعات)
                var assignmentIds = await _context.ExamAssignmentsToBatches
                    .Where(ea => ea.ExamId == examId)
                    .Select(ea => ea.Id)
                    .ToListAsync(ct);
                var counts = await _context.ExamCurriculumQuestionCounts
                    .Where(c => assignmentIds.Contains(c.ExamAssignmentId))
                    .ToListAsync(ct);
                _context.ExamCurriculumQuestionCounts.RemoveRange(counts);

                // ✅ حذف التعيينات الثانوية للطلاب
                var assignments = await _context.ExamAssignments
                    .Where(a => a.ExamId == examId)
                    .ToListAsync(ct);
                _context.ExamAssignments.RemoveRange(assignments);

                // ✅ حذف الروابط مع الدفعات
                var batchAssignments = await _context.ExamAssignmentsToBatches
                    .Where(ea => ea.ExamId == examId)
                    .ToListAsync(ct);
                _context.ExamAssignmentsToBatches.RemoveRange(batchAssignments);

                await _context.SaveChangesAsync(ct);

                // ✅ حذف الاختبار نفسه
                _context.Exams.Remove(exam);
                await _context.SaveChangesAsync(ct);

                _context.SystemLogs.Add(new SystemLog
                {
                    UserName = userName,
                    Action = "PermanentDelete",
                    Entity = "Exam",
                    EntityId = examId,
                    Description = $"قام {userName} بحذف اختبار ({exam.Title}) نهائياً من الدفعة رقم {batchId}، وكان قد اختبره {testedCount} طالب."
                });
                await _context.SaveChangesAsync(ct);

                return PlacementOperationResult.Ok("🗑️ تم حذف الاختبار نهائياً من قاعدة البيانات.", examId, batchId);
            }
            catch (Exception ex)
            {
                return UnexpectedFailure(scope, ex, "⚠️ فشل في حذف الاختبار: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // =====================================================
        // GetSectionsByCourse / PlacementDetails
        // =====================================================
        public async Task<List<PlacementSectionOptionDto>> GetSectionsByCourseAsync(PlacementScope scope, int courseId, CancellationToken ct = default)
        {
            if (scope.IsPartner && !await BankCoursesInScope(scope).AnyAsync(c => c.Id == courseId, ct))
                return new List<PlacementSectionOptionDto>();

            var data = await (
                from cc in _context.CourseCurriculums
                join c in _context.Curriculums on cc.CurriculumId equals c.Id
                join s in _context.Sections on c.Id equals s.CurriculumId
                where cc.CourseId == courseId
                select new PlacementSectionOptionDto
                {
                    CurriculumId = c.Id,
                    CurriculumTitle = c.Title,
                    SectionId = s.Id,
                    SectionTitle = s.Title
                }
            ).ToListAsync(ct);

            // فلترة المحاور التي تحتوي على دروس وأسئلة
            data = data
                .Where(d =>
                    _context.Lessons.Any(l => l.SectionId == d.SectionId) &&
                    _context.Questions.Any(q => _context.Lessons.Any(l => l.Id == q.LessonId && l.SectionId == d.SectionId))
                )
                .ToList();

            return data;
        }

        public async Task<PlacementExamDetailsViewModel?> GetPlacementDetailsAsync(PlacementScope scope, int examId, CancellationToken ct = default)
        {
            var exam = await _guard.PlacementExams(_context, scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId && e.IsActive, ct);

            if (exam == null)
                return null;

            var examQuestions = await (
                from eq in _context.ExamQuestions.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on eq.QuestionId equals q.Id
                join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                where eq.ExamId == exam.Id
                select new
                {
                    eq.QuestionId,
                    eq.Order,
                    q.Title,
                    q.ImageUrl,
                    q.Difficulty,
                    q.CorrectAnswer,
                    LessonTitle = l.Title,
                    SectionId = s.Id,
                    SectionTitle = s.Title
                })
                .ToListAsync(ct);

            // الطلاب الذين بدأوا/أتمّوا الاختبار فعلاً
            var existingStatuses = await (
                from status in _context.ExamStudentStatuses.AsNoTracking()
                join student in _context.Students.AsNoTracking() on status.StudentId equals student.StudentID
                where status.ExamId == exam.Id
                select new PlacementExamStudentViewModel
                {
                    StudentId    = status.StudentId,
                    StudentName  = student.FullName,
                    HasStatus    = true,
                    Status       = status.Status,
                    AssignedAt   = status.AssignedAt,
                    SubmittedAt  = status.SubmittedAt,
                    Score        = status.Score
                })
                .ToListAsync(ct);

            // الدفعات المرتبطة بهذا الاختبار
            var batchAssignments = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Include(x => x.Batch)
                .Where(x => x.ExamId == exam.Id)
                .ToListAsync(ct);

            // جميع طلاب تلك الدفعات
            var batchIds = batchAssignments.Select(x => x.BatchId).Distinct().ToHashSet();

            var allBatchStudents = await (
                from enroll in EnrollmentsInScope(scope).AsNoTracking()
                join student in _context.Students.AsNoTracking() on enroll.StudentID equals student.StudentID
                join batch in _context.Batches.AsNoTracking() on enroll.BatchId equals batch.Id
                where batchIds.Contains(enroll.BatchId)
                select new { student.StudentID, student.FullName, BatchName = batch.Name, enroll.BatchId })
                .ToListAsync(ct);

            var existingStudentIds = existingStatuses.Select(s => s.StudentId).ToHashSet();

            // الطلاب في الدفعة الذين لم يبدأوا بعد
            var notStartedStudents = allBatchStudents
                .Where(s => !existingStudentIds.Contains(s.StudentID))
                .GroupBy(s => s.StudentID)
                .Select(g => g.First())
                .Select(s => new PlacementExamStudentViewModel
                {
                    StudentId   = s.StudentID,
                    StudentName = s.FullName,
                    BatchName   = s.BatchName,
                    HasStatus   = false,
                    Status      = ExamStatus.Pending,
                    AssignedAt  = exam.CreatedAt
                })
                .ToList();

            var allStudents = existingStatuses.Concat(notStartedStudents)
                .OrderBy(s => s.HasStatus ? 0 : 1)
                .ThenBy(s => s.StudentName)
                .ToList();

            var assignedBatches = batchAssignments
                .GroupBy(x => new { x.BatchId, BatchName = x.Batch?.Name ?? string.Empty })
                .Select(g => new PlacementExamBatchInfo
                {
                    BatchId          = g.Key.BatchId,
                    BatchName        = g.Key.BatchName,
                    IsSentToStudents = g.Any(x => x.IsSentToStudents),
                    StudentCount     = allBatchStudents.Count(s => s.BatchId == g.Key.BatchId)
                })
                .ToList();

            return new PlacementExamDetailsViewModel
            {
                ExamId          = exam.Id,
                Title           = exam.Title,
                TotalQuestions  = exam.TotalQuestions,
                DurationMinutes = exam.DurationMinutes,
                CreatedAt       = exam.CreatedAt,
                IsInLab         = !string.IsNullOrWhiteSpace(exam.ReferenceCode),
                ReferenceCode   = exam.ReferenceCode,
                AssignedBatches = assignedBatches,
                Sections        = examQuestions
                    .GroupBy(eq => new { eq.SectionId, eq.SectionTitle })
                    .Select(g => new PlacementExamReviewSectionVm
                    {
                        SectionId    = g.Key.SectionId,
                        SectionTitle = g.Key.SectionTitle,
                        Questions    = g.OrderBy(x => x.Order).Select(x => new PlacementExamReviewQuestionVm
                        {
                            QuestionId    = x.QuestionId,
                            Title         = x.Title,
                            ImageUrl      = x.ImageUrl,
                            Difficulty    = x.Difficulty,
                            LessonTitle   = x.LessonTitle,
                            CorrectAnswer = x.CorrectAnswer,
                            Order         = x.Order
                        }).ToList()
                    }).ToList(),
                StudentStatuses = allStudents
            };
        }

        // =====================================================
        // مساعدات النطاق
        // =====================================================

        // الأدمن: كل التسجيلات. الشريك: طلابه النشطون فقط.
        private IQueryable<StudentBatchEnrollment> EnrollmentsInScope(PlacementScope scope)
        {
            if (!scope.IsPartner)
                return _context.StudentBatchEnrollments;

            var partnerId = scope.PartnerId!.Value;
            return _context.StudentBatchEnrollments.Where(e =>
                e.Student.Branch.PartnerId == partnerId &&
                e.Student.IsActiveForLearning);
        }

        private IQueryable<PartnerSubscription> ActiveContracts(PlacementScope scope)
        {
            var partnerId = scope.PartnerId!.Value;
            var today = DateTime.Today;

            return _context.PartnerSubscriptions.Where(s =>
                s.PartnerId == partnerId &&
                s.StartDate <= today &&
                s.EndDate >= today &&
                s.CanUsePlacementExams);
        }

        private Task<bool> HasActivePlacementContractAsync(PlacementScope scope, CancellationToken ct)
            => ActiveContracts(scope).AnyAsync(ct);

        private Task<bool> CanModifyInScopeAsync(PlacementScope scope, CancellationToken ct)
            => scope.IsPartner ? HasActivePlacementContractAsync(scope, ct) : Task.FromResult(true);

        // الأدمن: كل الدورات. الشريك: دورات عقده النشط.
        private IQueryable<Course> CoursesInScope(PlacementScope scope)
        {
            if (!scope.IsPartner)
                return _context.Courses;

            var contracts = ActiveContracts(scope);
            return _context.Courses.Where(c =>
                contracts.Any(s => s.SubscriptionCourses.Any(sc => sc.CourseId == c.Id)));
        }

        // الأدمن: كل الدورات. الشريك: دورات عقده المسموح لها ببنك أسئلة المنصة.
        private IQueryable<Course> BankCoursesInScope(PlacementScope scope)
        {
            if (!scope.IsPartner)
                return _context.Courses;

            var contracts = ActiveContracts(scope);
            return _context.Courses.Where(c =>
                contracts.Any(s => s.SubscriptionCourses.Any(sc => sc.CourseId == c.Id && sc.CanUsePlatformQuestionBank)));
        }

        // الأدمن: كل النماذج غير المؤرشفة. الشريك: حسب العقد وإتاحة النموذج له.
        private IQueryable<ProfessionalModel> ModelsInScope(PlacementScope scope)
        {
            var models = _context.ProfessionalModels.Where(m => !m.IsArchived);

            if (!scope.IsPartner)
                return models;

            var partnerId = scope.PartnerId!.Value;
            var today = DateTime.Today;
            var contracts = ActiveContracts(scope);

            return models.Where(m =>
                contracts.Any(s => s.CanUseProfessionalModels) &&
                (m.IsGlobal
                 || _context.ProfessionalModelPartners.Any(p =>
                        p.ProfessionalModelId == m.Id &&
                        p.PartnerId == partnerId)
                 || _context.ProfessionalModelSubscriptionPeriods.Any(sp =>
                        sp.ProfessionalModelId == m.Id &&
                        sp.PartnerSubscriptionPeriod.PartnerSubscription.PartnerId == partnerId &&
                        sp.PartnerSubscriptionPeriod.StartDate <= today &&
                        sp.PartnerSubscriptionPeriod.EndDate >= today)));
        }

        private async Task<bool> CanUseBankForExamCourseAsync(PlacementScope scope, int? courseId, CancellationToken ct)
            => courseId.HasValue && await BankCoursesInScope(scope).AnyAsync(c => c.Id == courseId.Value, ct);

        private async Task<bool> IsSectionInCourseAsync(int sectionId, int? courseId, CancellationToken ct)
        {
            if (!courseId.HasValue)
                return false;

            return await (
                from cc in _context.CourseCurriculums
                join s in _context.Sections on cc.CurriculumId equals s.CurriculumId
                where cc.CourseId == courseId.Value && s.Id == sectionId
                select s.Id
            ).AnyAsync(ct);
        }

        private Task<bool> IsExamSentAsync(int examId, CancellationToken ct)
            => _context.ExamAssignmentsToBatches.AnyAsync(x => x.ExamId == examId && x.IsSentToStudents, ct);

        // الشريك: عقد نشط + دفعته + الاختبار مرتبط بها. الأدمن بلا قيد (كما كانت الـ Actions).
        private async Task<PlacementOperationResult?> ValidatePartnerExamBatchAsync(PlacementScope scope, int examId, int batchId, CancellationToken ct)
        {
            if (!scope.IsPartner)
                return null;

            if (!await HasActivePlacementContractAsync(scope, ct))
                return PlacementOperationResult.Fail(NotInContractMessage);

            if (!await _guard.CanAccessBatchAsync(_context, scope, batchId, ct))
                return PlacementOperationResult.NotFound();

            var linked = await _context.ExamAssignmentsToBatches
                .AnyAsync(x => x.ExamId == examId && x.BatchId == batchId, ct);

            return linked ? null : PlacementOperationResult.Fail("⚠️ هذا الاختبار غير مرتبط بهذه الدفعة.");
        }

        // الأدمن يرى رسالة الاستثناء كما كانت؛ الشريك يرى رسالة عامة والتفاصيل في السجل فقط.
        private PlacementOperationResult UnexpectedFailure(PlacementScope scope, Exception ex, string adminMessage)
        {
            _logger.LogError(ex, "PPL placement operation failed for scope {Kind} partner {PartnerId}", scope.Kind, scope.PartnerId);
            return PlacementOperationResult.Fail(scope.IsPartner ? PartnerUnexpectedErrorMessage : adminMessage);
        }

        private async Task ApplyPlacementExamDeliveryModeAsync(int examId, bool isInLab, bool isRandomized, CancellationToken ct)
        {
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId, ct);
            if (exam == null)
                return;

            exam.ReferenceCode = isInLab ? await GeneratePlacementReferenceCodeAsync(ct) : null;
            exam.RandomizeQuestions = isRandomized;
            await _context.SaveChangesAsync(ct);
        }

        private async Task<string> GeneratePlacementReferenceCodeAsync(CancellationToken ct)
        {
            string code;
            var random = new Random();

            do
            {
                code = random.Next(100000, 1000000).ToString();
            }
            while (await _context.Exams.AnyAsync(e => e.ReferenceCode == code, ct));

            return code;
        }
    }
}
