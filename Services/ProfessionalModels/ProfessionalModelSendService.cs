using System.Data;
using EFCore.BulkExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Helpers;
using QdratNew.Services.Interfaces;
using QdratNew.Services.PartnerDispatch;
using HomeworkEntity = QdratNew.Entities.Homework;

namespace QdratNew.Services.ProfessionalModels
{
    public class ProfessionalModelSendService : IProfessionalModelSendService
    {
        private const string ActiveEnrollmentStatus = "Active";
        private const int MaxBatchesPerRequest = 200;
        private const int MaxTitleLength = 200;
        private const int MaxReplacementCandidates = 50;
        private const int LockTimeoutMilliseconds = 15000;

        // نفس حدود مدة اختبار تحديد المستوى من نموذج
        private const int MinExamDurationMinutes = 5;
        private const int MaxExamDurationMinutes = 300;

        // نقرتان متتاليتان أو إعادة إرسال الطلب لا تنشئان واجبًا ثانيًا حتى مع تأكيد التحذيرات
        private static readonly TimeSpan DuplicateSendWindow = TimeSpan.FromSeconds(60);

        private readonly ApplicationDbContext _context;
        private readonly IProfessionalModelAvailabilityService _availabilityService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IAdvancedNotificationService _notificationService;
        private readonly IPartnerDispatchLogService _dispatchLog;
        private readonly ILogger<ProfessionalModelSendService> _logger;

        public ProfessionalModelSendService(
            ApplicationDbContext context,
            IProfessionalModelAvailabilityService availabilityService,
            ITimeZoneService timeZoneService,
            IAdvancedNotificationService notificationService,
            IPartnerDispatchLogService dispatchLog,
            ILogger<ProfessionalModelSendService> logger)
        {
            _context = context;
            _availabilityService = availabilityService;
            _timeZoneService = timeZoneService;
            _notificationService = notificationService;
            _dispatchLog = dispatchLog;
            _logger = logger;
        }

        // خطأ عمل متوقع رسالته آمنة للعرض (يُرمى داخل ExecutionStrategy لإلغاء الـ Transaction)
        private sealed class ModelSendRuleException : Exception
        {
            public ModelSendRuleException(string message) : base(message) { }
        }

        private sealed record ModelInfo(int Id, string Title, int? CurriculumId, ProfessionalModelType ModelType);

        private sealed record SelectedBatch(int Id, string Name, int CourseId);

        private sealed record ModelQuestionRow(Guid QuestionId, int LessonId, string LessonTitle, string? Title, string? ImageUrl, string? ReferenceNumber, bool IsQuantitative);

        // ناتج التحقق قبل الكتابة، مشترك بين الواجب والاختبار
        private sealed record PreparedSend(ModelInfo Model, ModelSendPreview Preview, Dictionary<int, List<int>> Recipients, List<int> TargetBatchIds, string Title, DateTime? StartAt, DateTime? EndAt);

        // صف إرسال سابق من النموذج. بخصائص لا بـ Constructor حتى يترجم EF الفلترة بعد الـ Select
        private sealed class SentFromModelRow
        {
            public int BatchId { get; init; }
            public DateTime CreatedAt { get; init; }
        }

        // =====================================================
        // القوائم
        // =====================================================
        public async Task<IReadOnlyList<ProfessionalModelListItemVm>> GetModelsAsync(ModelSendScope scope, ProfessionalModelType type, CancellationToken ct = default)
        {
            return await ModelsInScope(scope, type)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new ProfessionalModelListItemVm
                {
                    Id = m.Id,
                    Title = m.Title,
                    Description = m.Description,
                    CurriculumId = m.CurriculumId,
                    CurriculumTitle = m.Curriculum != null ? m.Curriculum.Title : null,
                    ModelType = m.ModelType,
                    QuestionCount = m.Questions.Count(q => q.QuestionId != null)
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ModelBatchOptionVm>> GetEligibleBatchesAsync(ModelSendScope scope, int modelId, CancellationToken ct = default)
        {
            var model = await LoadSendableModelAsync(scope, modelId, ct);
            if (model == null)
                return Array.Empty<ModelBatchOptionVm>();

            var enrollments = ActiveEnrollments(scope);
            var sentFromModel = SentFromModel(model);

            return await EligibleBatches(scope, model)
                .OrderBy(b => b.Name)
                .Select(b => new ModelBatchOptionVm
                {
                    BatchId = b.Id,
                    Name = b.Name,
                    ActiveStudents = enrollments.Count(e => e.BatchId == b.Id),
                    AlreadySentFromModel = sentFromModel.Any(s => s.BatchId == b.Id)
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PreviewQuestionVm>> GetReplacementCandidatesAsync(ModelSendScope scope, int modelId, Guid questionId, CancellationToken ct = default)
        {
            var model = await LoadSendableModelAsync(scope, modelId, ct);
            if (model == null)
                return Array.Empty<PreviewQuestionVm>();

            var lessonId = await _context.ProfessionalModelQuestions
                .AsNoTracking()
                .Where(mq => mq.ModelId == modelId && mq.QuestionId == questionId)
                .Select(mq => (int?)mq.Question.LessonId)
                .FirstOrDefaultAsync(ct);

            if (lessonId == null || !await CanUseBankAsync(scope, model, Array.Empty<SelectedBatch>(), ct))
                return Array.Empty<PreviewQuestionVm>();

            return await ValidBankQuestions()
                .Where(q =>
                    q.LessonId == lessonId.Value &&
                    !_context.ProfessionalModelQuestions.Any(mq => mq.ModelId == modelId && mq.QuestionId == q.Id))
                .OrderByDescending(q => q.CreatedAt)
                .Take(MaxReplacementCandidates)
                .Select(q => new PreviewQuestionVm
                {
                    QuestionId = q.Id,
                    LessonId = q.LessonId,
                    LessonTitle = q.Lesson.Title,
                    Title = q.Title,
                    ImageUrl = q.ImageUrl,
                    ReferenceNumber = q.ReferenceNumber,
                    IsQuantitative = q.IsQuantitative,
                    ReplacesQuestionId = questionId
                })
                .ToListAsync(ct);
        }

        // =====================================================
        // المعاينة
        // =====================================================
        public async Task<ModelSendPreview?> PreviewAsync(ModelSendScope scope, ProfessionalModelType type, SendFromModelRequestBase req, CancellationToken ct = default)
        {
            var model = await LoadSendableModelAsync(scope, req.ModelId, ct);
            return model == null || model.ModelType != type
                ? null
                : await BuildPreviewAsync(scope, model, req, ct);
        }

        private async Task<ModelSendPreview> BuildPreviewAsync(ModelSendScope scope, ModelInfo model, SendFromModelRequestBase req, CancellationToken ct)
        {
            var requested = (req.BatchIds ?? new List<int>()).Distinct().ToList();
            if (requested.Count > MaxBatchesPerRequest)
                return InvalidPreview(model, "⚠️ دفعات غير صالحة.");

            // عدد الدفعات محدود أعلاه، وتحت UseCompatibilityLevel(120) يترجم Contains إلى IN
            var batches = requested.Count == 0
                ? new List<SelectedBatch>()
                : await EligibleBatches(scope, model)
                    .Where(b => requested.Contains(b.Id))
                    .Select(b => new SelectedBatch(b.Id, b.Name, b.CourseId))
                    .ToListAsync(ct);

            if (batches.Count != requested.Count)
                return InvalidPreview(model, "⚠️ دفعات غير صالحة.");

            var (questions, questionError) = await ResolveQuestionsAsync(scope, model, batches, req, ct);
            if (questionError != null)
                return InvalidPreview(model, questionError);

            return new ModelSendPreview
            {
                ModelId = model.Id,
                ModelTitle = model.Title,
                ModelType = model.ModelType,
                Questions = questions,
                Batches = await BuildBatchWarningsAsync(scope, model, batches, questions, req, ct)
            };
        }

        private static ModelSendPreview InvalidPreview(ModelInfo model, string error) => new()
        {
            ModelId = model.Id,
            ModelTitle = model.Title,
            ModelType = model.ModelType,
            ValidationError = error
        };

        private async Task<(List<PreviewQuestionVm> Questions, string? Error)> ResolveQuestionsAsync(
            ModelSendScope scope,
            ModelInfo model,
            IReadOnlyCollection<SelectedBatch> batches,
            SendFromModelRequestBase req,
            CancellationToken ct)
        {
            var rows = await _context.ProfessionalModelQuestions
                .AsNoTracking()
                .Where(mq => mq.ModelId == model.Id && mq.QuestionId != null)
                .OrderBy(mq => mq.OrderNumber)
                .ThenBy(mq => mq.Id)
                .Select(mq => new ModelQuestionRow(
                    mq.QuestionId!.Value,
                    mq.Question.LessonId,
                    mq.Question.Lesson.Title,
                    mq.Question.Title,
                    mq.Question.ImageUrl,
                    mq.Question.ReferenceNumber,
                    mq.Question.IsQuantitative))
                .ToListAsync(ct);

            // بعض النماذج الحالية فيها نفس السؤال أكثر من مرة، والطالب يأخذه مرة واحدة
            var seen = new HashSet<Guid>();
            var modelQuestions = rows.Where(q => seen.Add(q.QuestionId)).ToList();

            var excluded = (req.ExcludedQuestionIds ?? new List<Guid>()).ToHashSet();
            var replacements = (req.Replacements ?? new Dictionary<Guid, Guid>())
                .Where(r => !excluded.Contains(r.Key))
                .ToDictionary(r => r.Key, r => r.Value);

            var byId = modelQuestions.ToDictionary(q => q.QuestionId);
            if (replacements.Keys.Any(id => !byId.ContainsKey(id)))
                return (new(), "⚠️ لا يمكن استبدال سؤال ليس ضمن النموذج.");

            var substitutes = new Dictionary<Guid, ModelQuestionRow>();
            if (replacements.Count > 0)
            {
                if (!await CanUseBankAsync(scope, model, batches, ct))
                    return (new(), "⚠️ عقدك لا يتيح استبدال الأسئلة من بنك المنصة لدورات الدفعات المختارة.");

                // عدد البدائل لا يتجاوز عدد أسئلة النموذج
                var substituteIds = replacements.Values.Distinct().ToList();
                substitutes = await ValidBankQuestions()
                    .Where(q => substituteIds.Contains(q.Id))
                    .Select(q => new ModelQuestionRow(q.Id, q.LessonId, q.Lesson.Title, q.Title, q.ImageUrl, q.ReferenceNumber, q.IsQuantitative))
                    .ToDictionaryAsync(q => q.QuestionId, ct);

                foreach (var (oldId, newId) in replacements)
                {
                    if (!substitutes.TryGetValue(newId, out var substitute) || substitute.LessonId != byId[oldId].LessonId)
                        return (new(), "⚠️ السؤال البديل غير صالح أو ليس من نفس الدرس.");
                }
            }

            var finalIds = new HashSet<Guid>();
            var questions = new List<PreviewQuestionVm>();

            foreach (var question in modelQuestions.Where(q => !excluded.Contains(q.QuestionId)))
            {
                var isReplaced = replacements.TryGetValue(question.QuestionId, out var newId);
                var row = isReplaced ? substitutes[newId] : question;

                if (!finalIds.Add(row.QuestionId))
                    return (new(), "⚠️ السؤال البديل موجود بالفعل في القائمة.");

                questions.Add(new PreviewQuestionVm
                {
                    QuestionId = row.QuestionId,
                    OrderNumber = questions.Count + 1,
                    LessonId = row.LessonId,
                    LessonTitle = row.LessonTitle,
                    Title = row.Title,
                    ImageUrl = row.ImageUrl,
                    ReferenceNumber = row.ReferenceNumber,
                    IsQuantitative = row.IsQuantitative,
                    ReplacesQuestionId = isReplaced ? question.QuestionId : null
                });
            }

            return questions.Count == 0
                ? (questions, "⚠️ لا توجد أسئلة للإرسال بعد الاستبعاد.")
                : (questions, null);
        }

        private async Task<List<BatchWarningVm>> BuildBatchWarningsAsync(
            ModelSendScope scope,
            ModelInfo model,
            IReadOnlyCollection<SelectedBatch> batches,
            IReadOnlyCollection<PreviewQuestionVm> questions,
            SendFromModelRequestBase req,
            CancellationToken ct)
        {
            if (batches.Count == 0)
                return new List<BatchWarningVm>();

            var batchIds = batches.Select(b => b.Id).ToList();
            var finalIds = questions.Select(q => q.QuestionId).ToHashSet();

            var students = await LoadRecipientsAsync(scope, batchIds, req, ct);
            var sentBatchIds = await SentFromModelBatchIdsAsync(model, batchIds, ct);

            // Homeworks بلا فهرس يبدأ بـ HomeworkSetId، فالربط بالطالب يستخدم (StudentId, HomeworkSetId)
            var sentQuestions = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking() on e.BatchId equals hs.BatchId
                join h in _context.Homeworks.AsNoTracking()
                    on new { HomeworkSetId = hs.Id, StudentId = e.StudentID }
                    equals new { h.HomeworkSetId, h.StudentId }
                where batchIds.Contains(e.BatchId) && !hs.IsArchived && hs.IsSent
                select new { hs.BatchId, h.QuestionId }
            ).Distinct().ToListAsync(ct);

            var repeatedPairs = sentQuestions
                .Where(x => finalIds.Contains(x.QuestionId))
                .Select(x => (x.BatchId, x.QuestionId))
                .ToHashSet();

            // الاختبار يتأثر بما رآه الطالب في واجب أو اختبار سابق
            if (model.ModelType == ProfessionalModelType.Exam)
            {
                var sentExamQuestions = await (
                    from a in _context.ExamAssignmentsToBatches.AsNoTracking()
                    join eq in _context.ExamQuestions.AsNoTracking() on (int?)a.Id equals eq.ExamAssignmentId
                    where batchIds.Contains(a.BatchId) && a.IsSentToStudents && !a.IsArchived
                    select new { a.BatchId, eq.QuestionId }
                ).Distinct().ToListAsync(ct);

                repeatedPairs.UnionWith(sentExamQuestions
                    .Where(x => finalIds.Contains(x.QuestionId))
                    .Select(x => (x.BatchId, x.QuestionId)));
            }

            var repeated = repeatedPairs
                .GroupBy(x => x.BatchId)
                .ToDictionary(g => g.Key, g => g.Count());

            return batches
                .OrderBy(b => b.Name)
                .Select(b => new BatchWarningVm
                {
                    BatchId = b.Id,
                    Name = b.Name,
                    StudentCount = students.TryGetValue(b.Id, out var list) ? list.Count : 0,
                    RepeatedQuestionCount = repeated.GetValueOrDefault(b.Id),
                    AlreadySentFromModel = sentBatchIds.Contains(b.Id)
                })
                .ToList();
        }

        // =====================================================
        // إرسال الواجب
        // =====================================================
        public async Task<ModelSendResult> SendHomeworkAsync(ModelSendScope scope, SendModelHomeworkRequest req, CancellationToken ct = default)
        {
            var (send, stop) = await PrepareSendAsync(scope, ProfessionalModelType.Homework, req, ct);
            if (send == null)
                return stop!;

            var questions = send.Preview.Questions;

            return await ExecuteSendAsync(scope, send, req.ConfirmWarnings, async now =>
            {
                var sets = send.TargetBatchIds
                    .Select(batchId => new HomeworkSet
                    {
                        BatchId = batchId,
                        Title = send.Title,
                        CompletionTitle = send.Model.Title,
                        CreatedAt = now,
                        StartAt = send.StartAt,
                        EndAt = send.EndAt,
                        IsExtra = !req.LectureId.HasValue,
                        LectureId = req.LectureId,
                        IsFromProfessionalModel = true,
                        SourceProfessionalModelId = send.Model.Id,
                        OwnerPartnerId = scope.OwnerPartnerId, // APD-S1.3: null للأدمن
                        AssignedByUserId = scope.UserId,
                        IsSent = true
                    })
                    .ToList();

                await _context.BulkInsertAsync(sets, new BulkConfig { SetOutputIdentity = true }, cancellationToken: ct);

                var setStudents = new List<HomeworkSetStudent>();
                var homeworks = new List<HomeworkEntity>();

                foreach (var set in sets)
                {
                    foreach (var studentId in send.Recipients[set.BatchId])
                    {
                        setStudents.Add(new HomeworkSetStudent
                        {
                            HomeworkSetId = set.Id,
                            StudentId = studentId,
                            AssignedAt = now,
                            LastUpdated = now,
                            NotificationSent = true
                        });

                        foreach (var question in questions)
                        {
                            homeworks.Add(new HomeworkEntity
                            {
                                StudentId = studentId,
                                QuestionId = question.QuestionId,
                                LessonId = question.LessonId,
                                LectureId = req.LectureId,
                                HomeworkSetId = set.Id,
                                AssignedAt = now,
                                CreatedAt = now,
                                Status = HomeworkStatus.Pending,
                                IsSent = true
                            });
                        }
                    }
                }

                await _context.BulkInsertAsync(setStudents, cancellationToken: ct);
                await InsertHomeworksAsync(homeworks, ct);

                return sets.ToDictionary(set => set.BatchId, set => set.Id);
            }, ct);
        }

        // نقطة فصل لاختبار إلغاء الـ Transaction عند الفشل في منتصف الكتابة
        protected virtual Task InsertHomeworksAsync(List<HomeworkEntity> homeworks, CancellationToken ct)
            => _context.BulkInsertAsync(homeworks, cancellationToken: ct);

        // =====================================================
        // إرسال الاختبار (PPM-S5.1)
        // =====================================================
        public async Task<ModelSendResult> SendExamAsync(ModelSendScope scope, SendModelExamRequest req, CancellationToken ct = default)
        {
            if (scope.IsPartner && (req.IsInLab || req.RandomizeQuestions.HasValue || req.StudentIds != null))
                return ModelSendResult.Fail("⚠️ هذه الخيارات غير متاحة.");

            // APD-S2.1: الحضوري والترتيب العشوائي مسموحان للأدمن، والإرسال للشريك يشمل كل طلابه النشطين
            if (scope.IsAdminForPartner && req.StudentIds != null)
                return ModelSendResult.Fail("⚠️ الإرسال لطلاب محددين متاح لدفعات المنصة فقط.");

            var (send, stop) = await PrepareSendAsync(scope, ProfessionalModelType.Exam, req, ct);
            if (send == null)
                return stop!;

            var questions = send.Preview.Questions;

            // الشريك: اختبار لكل دفعة حتى يبقى حذف الإرسال مستقلًا لكل دفعة، كما في مسوداته.
            // الأدمن: اختبار واحد ورقم مرجعي واحد لكل الدفعات، وحذفه من ModelAssignments يشمل دفعاته كلها.
            var ownedByPartner = TargetsPartner(scope);
            var examCount = ownedByPartner ? send.TargetBatchIds.Count : 1;
            var curriculumId = ownedByPartner ? send.Model.CurriculumId : null;

            // APD-S2.1: Students/Exams يبحث عن الاختبار الحضوري بالرقم وحده، فلكل اختبار مملوك للشريك رقمه
            var referenceCodes = !req.IsInLab
                ? null
                : ownedByPartner
                    ? await NewReferenceCodesAsync(examCount, ct)
                    : new List<string> { Random.Shared.Next(100000, 999999).ToString() };

            return await ExecuteSendAsync(scope, send, req.ConfirmWarnings, async now =>
            {
                var exams = Enumerable.Range(0, examCount)
                    .Select(examIndex => new Exam
                    {
                        Title = send.Title,
                        Type = ExamType.Manual,
                        CurriculumId = curriculumId,
                        TotalQuestions = questions.Count,
                        DurationMinutes = req.DurationMinutes,
                        CreatedAt = now,
                        IsActive = true,
                        IsFromProfessionalModel = true,
                        SourceProfessionalModelId = send.Model.Id,
                        OwnerPartnerId = scope.OwnerPartnerId,
                        ReferenceCode = referenceCodes?[examIndex],
                        RandomizeQuestions = req.RandomizeQuestions ?? true
                    })
                    .ToList();

                await _context.BulkInsertAsync(exams, new BulkConfig { SetOutputIdentity = true }, cancellationToken: ct);

                var assignments = send.TargetBatchIds
                    .Select((batchId, index) => new ExamAssignmentToBatch
                    {
                        ExamId = exams[ownedByPartner ? index : 0].Id,
                        BatchId = batchId,
                        CurriculumId = curriculumId,
                        Title = send.Title,
                        TotalQuestions = questions.Count,
                        DurationMinutes = req.DurationMinutes,
                        ScheduledDate = send.StartAt,
                        EndAt = send.EndAt,
                        CreatedAt = now,
                        AssignedAt = now,
                        IsSentToStudents = true,
                        IsOnline = !req.IsInLab,
                        IsInLab = req.IsInLab
                    })
                    .ToList();

                await _context.BulkInsertAsync(assignments, new BulkConfig { SetOutputIdentity = true }, cancellationToken: ct);

                var examQuestions = new List<ExamQuestion>();
                var examAssignments = new List<ExamAssignment>();
                var statuses = new List<ExamStudentStatus>();

                foreach (var assignment in assignments)
                {
                    examQuestions.AddRange(questions.Select((question, index) => new ExamQuestion
                    {
                        ExamId = assignment.ExamId,
                        ExamAssignmentId = assignment.Id,
                        QuestionId = question.QuestionId,
                        Order = index + 1,
                        IsManuallySelected = true
                    }));

                    foreach (var studentId in send.Recipients[assignment.BatchId])
                    {
                        // شاشة الأدمن لا تنشئ ExamAssignments، وصفوفها تُظهر الاختبار في Students/Exams/Index القديمة
                        if (ownedByPartner)
                        {
                            examAssignments.Add(new ExamAssignment
                            {
                                ExamId = assignment.ExamId!.Value,
                                StudentId = studentId,
                                AssignedAt = now,
                                DueDate = send.EndAt
                            });
                        }

                        statuses.Add(new ExamStudentStatus
                        {
                            ExamId = assignment.ExamId!.Value,
                            ExamAssignmentId = assignment.Id,
                            StudentId = studentId,
                            Status = ExamStatus.Pending,
                            IsSubmitted = false,
                            AssignedAt = now
                        });
                    }
                }

                await _context.BulkInsertAsync(examQuestions, cancellationToken: ct);
                await _context.BulkInsertAsync(examAssignments, cancellationToken: ct);
                await InsertExamStatusesAsync(statuses, ct);

                return assignments.ToDictionary(assignment => assignment.BatchId, assignment => assignment.ExamId!.Value);
            }, ct);
        }

        // نقطة فصل لاختبار إلغاء الـ Transaction عند الفشل في منتصف الكتابة
        protected virtual Task InsertExamStatusesAsync(List<ExamStudentStatus> statuses, CancellationToken ct)
            => _context.BulkInsertAsync(statuses, cancellationToken: ct);

        private async Task<List<string>> NewReferenceCodesAsync(int count, CancellationToken ct)
        {
            var codes = new HashSet<string>();

            while (codes.Count < count)
            {
                var candidates = Enumerable.Range(0, count - codes.Count)
                    .Select(_ => Random.Shared.Next(100000, 999999).ToString())
                    .Where(code => !codes.Contains(code))
                    .Distinct()
                    .ToList();

                var taken = await _context.Exams
                    .AsNoTracking()
                    .Where(e => EF.Constant(candidates).Contains(e.ReferenceCode))
                    .Select(e => e.ReferenceCode)
                    .ToListAsync(ct);

                codes.UnionWith(candidates.Except(taken));
            }

            return codes.ToList();
        }

        // =====================================================
        // خطوات الإرسال المشتركة
        // =====================================================
        private async Task<(PreparedSend? Send, ModelSendResult? Stop)> PrepareSendAsync(
            ModelSendScope scope,
            ProfessionalModelType type,
            SendFromModelRequestBase req,
            CancellationToken ct)
        {
            var model = await LoadSendableModelAsync(scope, req.ModelId, ct);
            if (model == null || model.ModelType != type)
                return (null, new ModelSendResult { Message = "⚠️ النموذج غير متاح.", IsNotFound = true });

            if (req.BatchIds == null || req.BatchIds.Count == 0)
                return (null, ModelSendResult.Fail("⚠️ اختر دفعة واحدة على الأقل."));

            var noun = type == ProfessionalModelType.Exam ? "الاختبار" : "الواجب";

            var title = req.Title?.Trim();
            if (title?.Length > MaxTitleLength)
                return (null, ModelSendResult.Fail($"⚠️ عنوان {noun} يجب ألا يتجاوز {MaxTitleLength} حرفًا."));

            var batchIds = req.BatchIds.Distinct().ToList();

            var lectureError = await ValidateLectureAsync(scope, (req as SendModelHomeworkRequest)?.LectureId, batchIds, ct);
            if (lectureError != null)
                return (null, ModelSendResult.Fail(lectureError));

            var (startAt, endAt, dateError) = ResolveDates(scope, req);
            if (dateError != null)
                return (null, ModelSendResult.Fail(dateError));

            var preview = await BuildPreviewAsync(scope, model, req, ct);
            if (preview.ValidationError != null)
                return (null, ModelSendResult.Fail(preview.ValidationError));

            var recipients = await LoadRecipientsAsync(scope, batchIds, req, ct);
            if (recipients.Count == 0)
                return (null, ModelSendResult.Fail("⚠️ لا يوجد طلاب نشطون في الدفعات المختارة."));

            if (preview.HasWarnings && !req.ConfirmWarnings)
                return (null, RequiresConfirmation(preview));

            var finalTitle = string.IsNullOrEmpty(title)
                ? $"{(type == ProfessionalModelType.Exam ? "اختبار" : "واجب")} من النموذج {model.Title}"
                : title;

            return (new PreparedSend(model, preview, recipients, batchIds.Where(recipients.ContainsKey).ToList(), finalTitle, startAt, endAt), null);
        }

        private async Task<ModelSendResult> ExecuteSendAsync(
            ModelSendScope scope,
            PreparedSend send,
            bool confirmed,
            Func<DateTime, Task<Dictionary<int, int>>> writeAsync,
            CancellationToken ct)
        {
            var model = send.Model;
            var noun = model.ModelType == ProfessionalModelType.Exam ? "الاختبار" : "الواجب";
            var studentsSent = send.Recipients.Values.Sum(s => s.Count);

            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                    await AcquireModelSendLockAsync(model.Id, ct);
                    await EnsureNotDuplicateSendAsync(model, send.TargetBatchIds, confirmed, ct);

                    var now = _timeZoneService.GetNowUtc();
                    var targets = await writeAsync(now);

                    // APD-S2.1: السجل داخل نفس الـ Transaction، فلا إرسال بلا سجل ولا سجل بلا إرسال
                    if (scope.IsAdminForPartner)
                        await WriteDispatchLogAsync(scope, send, targets, now, ct);

                    await transaction.CommitAsync(ct);
                });
            }
            catch (ModelSendRuleException ex)
            {
                _context.ChangeTracker.Clear();
                return ModelSendResult.Fail(ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // ما بقي متتبعًا من محاولة فاشلة قد يُحفظ مع إرسال الشريك التالي على نفس الـ DbContext
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "PPM {ModelType} send failed for model {ModelId}, scope {Kind}, partner {PartnerId}", model.ModelType, model.Id, scope.Kind, scope.PartnerId);
                return ModelSendResult.Fail($"⚠️ تعذر إرسال {noun}، ولم يُحفظ أي شيء. حاول مرة أخرى.");
            }

            // الإرسال نجح فعليًا: تعثر السجل أو الإشعار لا يقلب النتيجة إلى فشل
            await WriteSendLogAsync(scope, model, send.TargetBatchIds.Count, studentsSent, send.Preview.Questions.Count);
            await NotifyAsync(model, send.Preview, send.Recipients, send.EndAt);

            var skipped = send.Preview.Batches.Where(b => !send.Recipients.ContainsKey(b.BatchId)).Select(b => b.Name).ToList();
            var message = skipped.Count == 0
                ? $"✅ تم إرسال {noun} للدفعات المختارة بنجاح."
                : $"✅ تم إرسال {noun}. تم تخطي دفعات بلا طلاب نشطين: {string.Join("، ", skipped)}.";

            return new ModelSendResult
            {
                Succeeded = true,
                Message = message,
                BatchesSent = send.TargetBatchIds.Count,
                StudentsSent = studentsSent
            };
        }

        private async Task WriteDispatchLogAsync(ModelSendScope scope, PreparedSend send, Dictionary<int, int> targets, DateTime now, CancellationToken ct)
        {
            // إعادة المحاولة عبر ExecutionStrategy تبدأ بلا صفوف سجل معلّقة من المحاولة السابقة
            _context.ChangeTracker.Clear();

            var kind = send.Model.ModelType == ProfessionalModelType.Exam ? PartnerDispatchKind.Exam : PartnerDispatchKind.Homework;

            await _dispatchLog.WriteAsync(
                send.TargetBatchIds
                    .Select(batchId => new PartnerDispatchLog
                    {
                        DispatchGroupId = scope.DispatchGroupId!.Value,
                        Kind = kind,
                        Title = send.Title,
                        SourceProfessionalModelId = send.Model.Id,
                        PartnerId = scope.PartnerId!.Value,
                        BatchId = batchId,
                        TargetEntityId = targets[batchId],
                        StudentsCount = send.Recipients[batchId].Count,
                        ContractOverridden = scope.ContractOverridden,
                        OverrideReason = scope.OverrideReason,
                        SentByUserId = scope.UserId!,
                        SentAtUtc = now
                    })
                    .ToList(),
                ct);
        }

        private static ModelSendResult RequiresConfirmation(ModelSendPreview preview) => new()
        {
            Succeeded = false,
            RequiresConfirmation = true,
            Preview = preview,
            Message = "⚠️ سبق إرسال هذا النموذج أو بعض أسئلته لدفعة مختارة. راجع التحذيرات وأكد الإرسال."
        };

        private async Task<string?> ValidateLectureAsync(ModelSendScope scope, int? lectureId, IReadOnlyList<int> batchIds, CancellationToken ct)
        {
            if (!lectureId.HasValue)
                return null;

            if (TargetsPartner(scope))
                return "⚠️ ربط الواجب بمحاضرة غير متاح.";

            if (batchIds.Count != 1)
                return "⚠️ ربط الواجب بمحاضرة يتطلب اختيار دفعة واحدة فقط.";

            var batchId = batchIds[0];
            var belongs = await _context.Lecture
                .AsNoTracking()
                .AnyAsync(l => l.Id == lectureId.Value && l.BatchId == batchId, ct);

            return belongs ? null : "⚠️ المحاضرة المختارة لا تنتمي للدفعة المحددة.";
        }

        public string? ValidateSchedule(ModelSendScope scope, SendFromModelRequestBase req)
            => ResolveDates(scope, req).Error;

        private (DateTime? StartAt, DateTime? EndAt, string? Error) ResolveDates(ModelSendScope scope, SendFromModelRequestBase req)
        {
            if (req is SendModelExamRequest exam &&
                (exam.DurationMinutes < MinExamDurationMinutes || exam.DurationMinutes > MaxExamDurationMinutes))
                return (null, null, $"⚠️ مدة الاختبار يجب أن تكون بين {MinExamDurationMinutes} و{MaxExamDurationMinutes} دقيقة.");

            if (TargetsPartner(scope) && (!req.StartAtLocal.HasValue || !req.EndAtLocal.HasValue))
                return (null, null, req is SendModelExamRequest
                    ? "⚠️ حدد موعد بداية ونهاية الاختبار."
                    : "⚠️ حدد موعد بداية ونهاية الواجب.");

            var startAt = req.StartAtLocal.HasValue ? _timeZoneService.ConvertToUtc(req.StartAtLocal.Value) : (DateTime?)null;
            var endAt = req.EndAtLocal.HasValue ? _timeZoneService.ConvertToUtc(req.EndAtLocal.Value) : (DateTime?)null;

            // موعد اختبار الأدمن اختياري: يبدأ الآن وينتهي بعد مدته
            if (!TargetsPartner(scope) && req is SendModelExamRequest adminExam)
            {
                startAt ??= _timeZoneService.GetNowUtc();
                endAt ??= startAt.Value.AddMinutes(adminExam.DurationMinutes);
            }

            if (startAt.HasValue && endAt.HasValue && endAt <= startAt)
                return (null, null, "⚠️ موعد النهاية يجب أن يكون بعد موعد البداية.");

            if (TargetsPartner(scope) && endAt <= _timeZoneService.GetNowUtc())
                return (null, null, "⚠️ موعد النهاية يجب أن يكون في المستقبل.");

            return (startAt, endAt, null);
        }

        // يقفل على مستوى الـ Transaction حتى لا يتجاوز طلبان متزامنان فحص التكرار معًا
        private async Task AcquireModelSendLockAsync(int modelId, CancellationToken ct)
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"PPM_ModelSend_{modelId}" };
            var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = LockTimeoutMilliseconds };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout",
                new object[] { result, resource, timeout },
                ct);

            if (result.Value is not int code || code < 0)
                throw new ModelSendRuleException("⚠️ يجري إرسال هذا النموذج الآن، حاول بعد قليل.");
        }

        private async Task EnsureNotDuplicateSendAsync(ModelInfo model, List<int> batchIds, bool confirmed, CancellationToken ct)
        {
            var since = _timeZoneService.GetNowUtc() - DuplicateSendWindow;

            var sent = await SentFromModel(model)
                .Where(s => batchIds.Contains(s.BatchId))
                .Select(s => s.CreatedAt)
                .ToListAsync(ct);

            if (sent.Any(createdAt => createdAt >= since))
                throw new ModelSendRuleException(model.ModelType == ProfessionalModelType.Exam
                    ? "⚠️ تم إرسال هذا النموذج لدفعة مختارة قبل لحظات. تحقق من الاختبارات المرسلة قبل إعادة الإرسال."
                    : "⚠️ تم إرسال هذا النموذج لدفعة مختارة قبل لحظات. تحقق من الواجبات المرسلة قبل إعادة الإرسال.");

            // طلب غير مؤكد تجاوز فحص التحذيرات قبل أن يكتمل إرسال متزامن
            if (sent.Count > 0 && !confirmed)
                throw new ModelSendRuleException("⚠️ سبق إرسال هذا النموذج لدفعة مختارة. راجع التحذيرات وأكد الإرسال.");
        }

        private async Task WriteSendLogAsync(ModelSendScope scope, ModelInfo model, int batches, int students, int questions)
        {
            try
            {
                var who = scope.UserName ?? scope.UserId ?? "Unknown";
                var owner = scope.Kind switch
                {
                    ModelSendScopeKind.Partner => $"الشريك {scope.PartnerId}",
                    ModelSendScopeKind.AdminForPartner => $"المعهد باسم الشريك {scope.PartnerId}",
                    _ => "المعهد"
                };
                var isExam = model.ModelType == ProfessionalModelType.Exam;

                _context.SystemLogs.Add(new SystemLog
                {
                    UserName = who,
                    Action = isExam ? "SendExamFromModel" : "SendHomeworkFromModel",
                    Entity = "ProfessionalModel",
                    EntityId = model.Id,
                    Description = $"أرسل {who} ({owner}) {(isExam ? "اختبارًا" : "واجبًا")} من النموذج ({model.Title}) إلى {batches} دفعة و{students} طالب بعدد {questions} سؤال"
                });

                await _context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "PPM send log failed for model {ModelId}", model.Id);
            }
        }

        private async Task NotifyAsync(ModelInfo model, ModelSendPreview preview, Dictionary<int, List<int>> recipients, DateTime? endAt)
        {
            var isExam = model.ModelType == ProfessionalModelType.Exam;
            var noun = isExam ? "اختبار" : "واجب";
            var category = isExam ? NotificationCategory.Exam : NotificationCategory.Homework;

            try
            {
                var deadline = endAt.HasValue
                    ? $" آخر موعد: {_timeZoneService.ConvertToSaudi(endAt.Value):yyyy-MM-dd HH:mm}"
                    : string.Empty;

                await _notificationService.SendToStudentsAsync(
                    recipients.Values.SelectMany(s => s).Distinct().ToList(),
                    $"📘 تم إرسال {noun} جديد من النموذج {model.Title}.{deadline}",
                    category,
                    isExam ? NotificationLinkBuilder.ExamDashboard() : NotificationLinkBuilder.HomeworkDashboard());

                var batchNames = preview.Batches
                    .Where(b => recipients.ContainsKey(b.BatchId))
                    .ToDictionary(b => b.BatchId, b => b.Name);
                var batchIds = batchNames.Keys.ToList();

                var instructors = await _context.InstructorCurriculumBatches
                    .AsNoTracking()
                    .Where(x => batchIds.Contains(x.BatchId))
                    .Select(x => new { x.InstructorId, x.BatchId })
                    .Distinct()
                    .ToListAsync();

                // إشعار واحد لكل مدرب مهما تعددت دفعاته. عددهم صغير والخدمة لا توفر إرسالًا مجمعًا للمدربين
                foreach (var instructor in instructors.GroupBy(x => x.InstructorId))
                {
                    var names = string.Join("، ", instructor.Select(x => batchNames[x.BatchId]));
                    await _notificationService.SendToInstructorAsync(
                        instructor.Key,
                        $"📢 تم إرسال {noun} من النموذج {model.Title} للدفعات: {names}.",
                        category,
                        isExam ? "/Instructors/ExamDashboard" : "/Instructors/HomeworkDashboard");
                }
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "PPM notification failed for model {ModelId}", model.Id);
            }
        }

        // =====================================================
        // مساعدات النطاق
        // =====================================================

        // APD-S2.1: الدفعات والطلاب والمواعيد وشكل الاختبار كنطاق الشريك سواء أرسله هو أو الأدمن باسمه.
        // أما النماذج والعقد والبنك فشرطها IsPartner: إرسال الأدمن فُحصت أهليته في IDispatchAudienceService.
        private static bool TargetsPartner(ModelSendScope scope) => scope.OwnerPartnerId != null;

        // الأدمن: كل النماذج غير المؤرشفة من النوع. الشريك: خدمة الإتاحة + عقد يسمح بإنشاء هذا النوع.
        private IQueryable<ProfessionalModel> ModelsInScope(ModelSendScope scope, ProfessionalModelType type)
        {
            if (!scope.IsPartner)
                return _context.ProfessionalModels.AsNoTracking().Where(m => !m.IsArchived && m.ModelType == type);

            var contracts = SendContracts(scope.PartnerId!.Value, type);

            return _availabilityService
                .AvailableForPartner(scope.PartnerId.Value, type)
                .Where(m => contracts.Any());
        }

        // نوع النموذج يحدد شرط العقد، والنموذج بلا نوع (ModelType = 0) لا يُرسل
        private async Task<ModelInfo?> LoadSendableModelAsync(ModelSendScope scope, int modelId, CancellationToken ct)
        {
            var type = await _context.ProfessionalModels
                .AsNoTracking()
                .Where(m => m.Id == modelId)
                .Select(m => m.ModelType)
                .FirstOrDefaultAsync(ct);

            if (type != ProfessionalModelType.Homework && type != ProfessionalModelType.Exam)
                return null;

            return await ModelsInScope(scope, type)
                .Where(m => m.Id == modelId)
                .Select(m => new ModelInfo(m.Id, m.Title, m.CurriculumId, m.ModelType))
                .FirstOrDefaultAsync(ct);
        }

        private IQueryable<PartnerSubscription> SendContracts(int partnerId, ProfessionalModelType type)
        {
            var today = DateTime.Today;
            var contracts = _context.PartnerSubscriptions.Where(s =>
                s.PartnerId == partnerId &&
                s.StartDate <= today &&
                s.EndDate >= today &&
                s.CanUseProfessionalModels);

            return type == ProfessionalModelType.Exam
                ? contracts.Where(s => s.CanCreateExams)
                : contracts.Where(s => s.CanCreateHomework);
        }

        // الأدمن: دفعات المنصة غير المحذوفة وغير المؤرشفة (APD-S2.2).
        // الأدمن باسم شريك: دفعات فروع الشريك المتوافقة مع منهج النموذج، كما في IDispatchAudienceService، بلا شرط العقد.
        // الشريك: دفعات فروعه، دورتها في عقده وتحتوي منهج النموذج. النموذج بلا منهج لا يُرسل للشريك.
        private IQueryable<Batch> EligibleBatches(ModelSendScope scope, ModelInfo model)
        {
            var batches = _context.Batches.AsNoTracking().Where(b => !b.IsDeleted && !b.IsArchived);

            if (scope.Kind == ModelSendScopeKind.Admin)
                return batches.Where(b => b.Branch.PartnerId == null);

            if (scope.IsAdminForPartner)
            {
                var targetPartnerId = scope.PartnerId!.Value;
                batches = batches.Where(b => b.Branch.PartnerId == targetPartnerId);

                if (!model.CurriculumId.HasValue)
                    return batches;

                var modelCurriculumId = model.CurriculumId.Value;
                return batches.Where(b => _context.CourseCurriculums.Any(cc => cc.CourseId == b.CourseId && cc.CurriculumId == modelCurriculumId));
            }

            if (!model.CurriculumId.HasValue)
                return batches.Where(b => false);

            var partnerId = scope.PartnerId!.Value;
            var curriculumId = model.CurriculumId.Value;
            var contracts = SendContracts(partnerId, model.ModelType);

            return batches.Where(b =>
                b.Branch.PartnerId == partnerId &&
                contracts.Any(s => s.SubscriptionCourses.Any(sc => sc.CourseId == b.CourseId)) &&
                _context.CourseCurriculums.Any(cc => cc.CourseId == b.CourseId && cc.CurriculumId == curriculumId));
        }

        // الأدمن: التسجيل النشط. الشريك: طلابه النشطون فقط.
        private IQueryable<StudentBatchEnrollment> ActiveEnrollments(ModelSendScope scope)
        {
            var enrollments = _context.StudentBatchEnrollments.AsNoTracking().Where(e => e.Status == ActiveEnrollmentStatus);

            if (!TargetsPartner(scope))
                return enrollments;

            var partnerId = scope.PartnerId!.Value;
            return enrollments.Where(e => e.Student.Branch.PartnerId == partnerId && e.Student.IsActiveForLearning);
        }

        // لا تحتوي إلا الدفعات التي فيها طلاب
        private async Task<Dictionary<int, List<int>>> LoadRecipientsAsync(ModelSendScope scope, List<int> batchIds, SendFromModelRequestBase req, CancellationToken ct)
        {
            var rows = await ActiveEnrollments(scope)
                .Where(e => batchIds.Contains(e.BatchId))
                .Select(e => new { e.BatchId, e.StudentID })
                .Distinct()
                .ToListAsync(ct);

            var selected = (req as SendModelExamRequest)?.StudentIds?.ToHashSet();

            return rows
                .Where(x => selected == null || selected.Contains(x.StudentID))
                .GroupBy(x => x.BatchId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.StudentID).ToList());
        }

        // ما أُرسل فعليًا من هذا النموذج وغير مؤرشف. حذف الإرسال أو أرشفته يرفع التحذير
        private IQueryable<SentFromModelRow> SentFromModel(ModelInfo model)
        {
            var modelId = model.Id;

            return model.ModelType == ProfessionalModelType.Exam
                ? _context.ExamAssignmentsToBatches
                    .AsNoTracking()
                    .Where(a => a.Exam.SourceProfessionalModelId == modelId && a.IsSentToStudents && !a.IsArchived)
                    .Select(a => new SentFromModelRow { BatchId = a.BatchId, CreatedAt = a.CreatedAt })
                : _context.HomeworkSets
                    .AsNoTracking()
                    .Where(h => h.SourceProfessionalModelId == modelId && !h.IsArchived)
                    .Select(h => new SentFromModelRow { BatchId = h.BatchId, CreatedAt = h.CreatedAt });
        }

        private async Task<HashSet<int>> SentFromModelBatchIdsAsync(ModelInfo model, List<int> batchIds, CancellationToken ct)
        {
            var ids = await SentFromModel(model)
                .Where(s => batchIds.Contains(s.BatchId))
                .Select(s => s.BatchId)
                .Distinct()
                .ToListAsync(ct);

            return ids.ToHashSet();
        }

        // نفس فلتر توليد الواجبات، ومن بنك المنصة فقط
        private IQueryable<Question> ValidBankQuestions()
            => _context.Questions.AsNoTracking().Where(q =>
                q.IsReviewed &&
                q.IsComplete &&
                !q.IsRejected &&
                q.PartnerId == null &&
                !string.IsNullOrWhiteSpace(q.CorrectAnswer));

        // بلا دفعات مختارة: يكفي دورة مؤهلة واحدة، والإرسال يعيد الفحص لكل دفعة
        private async Task<bool> CanUseBankAsync(ModelSendScope scope, ModelInfo model, IReadOnlyCollection<SelectedBatch> batches, CancellationToken ct)
        {
            if (!scope.IsPartner)
                return true;

            var bankCourseIds = await SendContracts(scope.PartnerId!.Value, model.ModelType)
                .SelectMany(s => s.SubscriptionCourses)
                .Where(sc => sc.CanUsePlatformQuestionBank)
                .Select(sc => sc.CourseId)
                .Distinct()
                .ToListAsync(ct);

            if (batches.Count > 0)
                return batches.All(b => bankCourseIds.Contains(b.CourseId));

            var eligibleCourseIds = await EligibleBatches(scope, model)
                .Select(b => b.CourseId)
                .Distinct()
                .ToListAsync(ct);

            return eligibleCourseIds.Any(bankCourseIds.Contains);
        }
    }
}
