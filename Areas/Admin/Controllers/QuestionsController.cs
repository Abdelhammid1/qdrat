using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Question;
using QdratNew.ViewModels.Shared;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class QuestionsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        public QuestionsController(IDbContextFactory<ApplicationDbContext> contextFactory, IMemoryCache cache)
        {
            _contextFactory = contextFactory;
            _cache = cache;
        }


        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> GetSectionsByCurriculum(int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var sections = await _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new { id = s.Id, title = s.Title })
                .ToListAsync();

            return Json(sections);
        }



        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> CheckSimilarQuestions(Guid id)
        {
            const int MaxResults = 30;
            const double ReliableThreshold = 0.78;

            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions
                .AsNoTracking()
                .Where(q => q.Id == id)
                .Select(q => new
                {
                    q.Id,
                    q.ReferenceNumber,
                    q.Title,
                    q.ValueA,
                    q.ValueB,
                    q.CorrectAnswer,
                    q.CurriculumId,
                    q.SectionId,
                    q.LessonId
                })
                .FirstOrDefaultAsync();

            if (question == null)
                return NotFound();

            var sourceText = BuildQuestionSimilarityText(
                question.Title,
                question.ValueA,
                question.ValueB,
                question.CorrectAnswer);

            var sourceNormalizedText = TextSimilarityHelper.NormalizeForSimilarity(sourceText);
            if (string.IsNullOrWhiteSpace(sourceNormalizedText))
                return Json(Array.Empty<object>());

            var sourceTokens = TextSimilarityHelper.GetSignificantTokens(sourceText).ToHashSet(StringComparer.Ordinal);
            if (!question.SectionId.HasValue)
                return Json(Array.Empty<object>());

            var candidates = await LoadSimilarityCandidatesAsync(_context, question.Id, question.SectionId.Value);

            foreach (var candidate in candidates)
            {
                candidate.CombinedText = BuildQuestionSimilarityText(
                    candidate.Title,
                    candidate.ValueA,
                    candidate.ValueB,
                    candidate.CorrectAnswer);
                candidate.NormalizedText = TextSimilarityHelper.NormalizeForSimilarity(candidate.CombinedText);
                candidate.Tokens = TextSimilarityHelper.GetSignificantTokens(candidate.CombinedText).ToHashSet(StringComparer.Ordinal);
            }

            var similar = candidates
                .Where(q => ShouldCompareWithSource(sourceNormalizedText, sourceTokens, q))
                .Select(q =>
                {
                    var analysis = TextSimilarityHelper.Analyze(sourceText, q.CombinedText);

                    return new
                    {
                        id = q.Id,
                        referenceNumber = q.ReferenceNumber,
                        title = CleanQuestionTitleForDisplay(q.Title),
                        curriculum = q.CurriculumTitle,
                        section = q.SectionTitle,
                        lesson = q.LessonTitle,
                        createdAt = q.CreatedAt,
                        similarity = analysis.Score,
                        matchType = analysis.MatchType,
                        tokenScore = analysis.TokenScore,
                        phraseScore = analysis.PhraseScore,
                        editScore = analysis.EditScore,
                        containmentScore = analysis.ContainmentScore,
                        isReliableMatch = analysis.IsReliableMatch,
                        scopeRank = q.ScopeRank
                    };
                })
                .Where(x => x.isReliableMatch && x.similarity >= ReliableThreshold)
                .OrderByDescending(x => x.similarity)
                .ThenBy(x => x.scopeRank)
                .ThenByDescending(x => x.tokenScore)
                .ThenByDescending(x => x.createdAt)
                .Take(MaxResults)
                .ToList();

            return Json(similar);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> LoadQuestionsData()
        {
            using var _context = _contextFactory.CreateDbContext();

            var draw = Request.Form["draw"].ToString();
            var start = int.TryParse(Request.Form["start"], out var parsedStart) && parsedStart > 0
                ? parsedStart
                : 0;
            var length = int.TryParse(Request.Form["length"], out var parsedLength) && parsedLength > 0
                ? Math.Min(parsedLength, 100)
                : 50;

            int? curriculumId = int.TryParse(Request.Form["curriculumId"], out var parsedCurriculumId)
                ? parsedCurriculumId
                : null;

            int? sectionId = int.TryParse(Request.Form["sectionId"], out var parsedSectionId)
                ? parsedSectionId
                : null;

            int? lessonId = int.TryParse(Request.Form["lessonId"], out var parsedLessonId)
                ? parsedLessonId
                : null;

            bool onlyUnanswered = Request.Form["onlyUnanswered"] == "true";

            string searchTitle = Request.Form["searchTitle"].ToString()?.Trim() ?? "";
            string dataTablesSearch = Request.Form["search[value]"].ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(searchTitle) && !string.IsNullOrWhiteSpace(dataTablesSearch))
                searchTitle = dataTablesSearch;

            var query = _context.Questions
                .AsNoTracking()
                .Where(q => q.IsComplete && q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);

            if (sectionId.HasValue)
                query = query.Where(q => q.SectionId == sectionId.Value);

            if (onlyUnanswered)
                query = query.Where(q => string.IsNullOrWhiteSpace(q.CorrectAnswer));

            if (!string.IsNullOrWhiteSpace(searchTitle))
            {
                query = query.Where(q =>
                    (q.Title != null && q.Title.Contains(searchTitle)) ||
                    (q.ReferenceNumber != null && q.ReferenceNumber.Contains(searchTitle)) ||
                    (q.InternalNote != null && q.InternalNote.Contains(searchTitle)));
            }

            var recordsFiltered = await query.CountAsync();

            var questions = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip(start)
                .Take(length)
                .Select(q => new
                {
                    q.Id,
                    q.ReferenceNumber,
                    q.Title,
                    Curriculum = q.Curriculum.Title,
                    Section = q.Section != null ? q.Section.Title : null,
                    Lesson = q.Lesson.Title,
                    q.IsAnswerConfirmed,
                    q.CreatedAt,
                    q.InternalNote
                })
                .ToListAsync();

            // ================================
            // 🟢 تنظيف العنوان من الوسوم المخفية
            // ================================
            var regex = new Regex("<span[^>]*display:none[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);

            var result = questions.Select(q =>
            {
                // 🔹 تنظيف العنوان من الوسوم المخفية
                string cleanTitle = regex.Replace(q.Title ?? "", "$1").Trim();

                // 🔹 اختصار العنوان
                string shortTitle = cleanTitle.Length > 50 ? cleanTitle.Substring(0, 50) + "..." : cleanTitle;

                return new
                {
                    referenceNumber = q.ReferenceNumber ?? "—",

                    title = shortTitle,

                    curriculum = q.Curriculum ?? "—",
                    section = q.Section ?? "—",
                    lesson = q.Lesson ?? "—",

                    isAnswerConfirmed = q.IsAnswerConfirmed,
                    createdAt = q.CreatedAt.ToString("yyyy/MM/dd"),

                    internalNote = string.IsNullOrWhiteSpace(q.InternalNote)
                        ? "—"
                        : (q.InternalNote.Length > 30 ? q.InternalNote.Substring(0, 30) + "..." : q.InternalNote),

                    id = q.Id
                };
            });

            return Json(new
            {
                draw,
                recordsTotal = recordsFiltered,
                recordsFiltered,
                data = result
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> LoadPendingReviewQuestionsData()
        {
            using var _context = _contextFactory.CreateDbContext();

            int? curriculumId = string.IsNullOrWhiteSpace(Request.Form["curriculumId"]) ? null : int.Parse(Request.Form["curriculumId"]);
            int? sectionId = string.IsNullOrWhiteSpace(Request.Form["sectionId"]) ? null : int.Parse(Request.Form["sectionId"]);
            int? lessonId = string.IsNullOrWhiteSpace(Request.Form["lessonId"]) ? null : int.Parse(Request.Form["lessonId"]);
            bool onlyUnanswered = Request.Form["onlyUnanswered"] == "true";
            string searchTitle = Request.Form["searchTitle"];

            var baseQuery = _context.Questions
                .AsNoTracking()
                .Where(q => q.IsComplete && !q.IsReviewed && !q.IsRejected)
                .Where(q => !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .Where(q => !curriculumId.HasValue || q.CurriculumId == curriculumId.Value)
                .Where(q => !sectionId.HasValue || q.SectionId == sectionId.Value)
                .Where(q => !lessonId.HasValue || q.LessonId == lessonId.Value)
                .Where(q => !onlyUnanswered || string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .Where(q => string.IsNullOrWhiteSpace(searchTitle) || (q.Title != null && q.Title.Contains(searchTitle)));

            int totalRecords = await baseQuery.CountAsync();

            var data = await baseQuery
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ReferenceNumber,
                    q.CreatedAt,
                    Curriculum = q.Curriculum.Title,
                    Section = q.Section.Title,
                    Lesson = q.Lesson.Title,
                    q.IsAnswerConfirmed
                })
                .ToListAsync();

            var result = data.Select(q => new
            {
                q.Id,
                title = q.Title.Length > 25 ? q.Title.Substring(0, 25) + "..." : q.Title,
                referenceNumber = q.ReferenceNumber ?? "—",
                curriculum = q.Curriculum ?? "—",
                section = q.Section ?? "—",
                lesson = q.Lesson ?? "—",
                createdAt = q.CreatedAt.ToString("yyyy/MM/dd"),
                isAnswerConfirmed = q.IsAnswerConfirmed
            });

            return Json(new
            {
                draw = Request.Form["draw"],
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = result
            });
        }


        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> Index(int? curriculumId, int? sectionId, int? lessonId, string? searchTitle, int page = 1, int pageSize = 50)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 تحميل القواميس من الكاش
            var curriculums = _cache.GetOrCreate("CurriculumsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Curriculums.AsNoTracking().ToDictionary(c => c.Id, c => c.Title);
            });

            var sections = _cache.GetOrCreate("SectionsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Sections.AsNoTracking().ToDictionary(s => s.Id, s => s.Title);
            });

            var lessons = _cache.GetOrCreate("LessonsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Lessons.AsNoTracking().ToDictionary(l => l.Id, l => l.Title);
            });

            var query = _context.Questions
                .AsNoTracking()
                .Where(q => q.IsComplete && q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);

            if (sectionId.HasValue)
                query = query.Where(q => q.SectionId == sectionId.Value);

            if (lessonId.HasValue)
                query = query.Where(q => q.LessonId == lessonId.Value);

            if (!string.IsNullOrWhiteSpace(searchTitle))
                query = query.Where(q => q.Title != null && q.Title.Contains(searchTitle.Trim()));

            var totalItems = await query.CountAsync();

            var summary = await _context.Questions
                .AsNoTracking()
                .GroupBy(q => 1)
                .Select(g => new
                {
                    Unanswered = g.Count(q => string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    Unreviewed = g.Count(q => !q.IsComplete || q.IsRejected),
                    PendingReview = g.Count(q =>
                        q.IsComplete &&
                        !q.IsReviewed &&
                        !q.IsRejected &&
                        !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                })
                .FirstOrDefaultAsync();

            var model = new QuestionIndexViewModel
            {
                TotalQuestionsCount = totalItems,
                UnansweredCount = summary?.Unanswered ?? 0,
                UnreviewedCount = summary?.Unreviewed ?? 0,
                PendingReviewCount = summary?.PendingReview ?? 0,
                SimilarGroupsCount = 0,
                CurriculumId = curriculumId,
                SectionId = sectionId,
                LessonId = lessonId,
                SearchTitle = searchTitle,

                Curriculums = curriculums.Select(c => new SelectListItem { Value = c.Key.ToString(), Text = c.Value }).ToList(),
                Sections = sections
                    .Select(s => new SelectListItem { Value = s.Key.ToString(), Text = s.Value })
                    .ToList(),
                Units = new List<SelectListItem>(),
                Lessons = lessons
                    .Select(l => new SelectListItem { Value = l.Key.ToString(), Text = l.Value })
                    .ToList(),

                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
            };

            return View(model);
        }



        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> Dashboard()
        {
            using var _context = _contextFactory.CreateDbContext();

            var summary = await _context.Questions
                .AsNoTracking()
                .GroupBy(q => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Approved = g.Count(q => q.IsComplete && q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    PendingReview = g.Count(q => q.IsComplete && !q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    MissingAnswers = g.Count(q => q.IsComplete && !q.IsRejected && string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    Rejected = g.Count(q => q.IsRejected),
                    Incomplete = g.Count(q => !q.IsComplete && !q.IsRejected),
                    UnconfirmedAnswers = g.Count(q => q.IsComplete && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer) && !q.IsAnswerConfirmed)
                })
                .FirstOrDefaultAsync();

            var totalQuestions = summary?.Total ?? 0;
            var totalLessons = await _context.Lessons.AsNoTracking().CountAsync(l => l.IsActive);
            var totalSections = await _context.Sections.AsNoTracking().CountAsync();

            var difficultyStatsRaw = await _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected)
                .GroupBy(q => q.Difficulty)
                .Select(g => new { Difficulty = g.Key, Count = g.Count() })
                .ToListAsync();

            var sectionMeta = await _context.Sections
                .AsNoTracking()
                .Select(s => new
                {
                    SectionId = s.Id,
                    SectionTitle = s.Title,
                    s.CurriculumId,
                    CurriculumTitle = s.Curriculum.Title,
                    LessonsCount = _context.Lessons.Count(l => l.SectionId == s.Id && l.IsActive)
                })
                .OrderBy(s => s.CurriculumTitle)
                .ThenBy(s => s.SectionTitle)
                .ToListAsync();

            var questionSectionStats = await _context.Questions
                .AsNoTracking()
                .Where(q => q.SectionId.HasValue)
                .GroupBy(q => q.SectionId!.Value)
                .Select(g => new
                {
                    SectionId = g.Key,
                    QuestionsCount = g.Count(),
                    ApprovedCount = g.Count(q => q.IsComplete && q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    PendingReviewCount = g.Count(q => q.IsComplete && !q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    MissingAnswersCount = g.Count(q => q.IsComplete && !q.IsRejected && string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    RejectedCount = g.Count(q => q.IsRejected),
                    EasyCount = g.Count(q => q.Difficulty == DifficultyLevel.Easy && !q.IsRejected),
                    MediumCount = g.Count(q => q.Difficulty == DifficultyLevel.Medium && !q.IsRejected),
                    HardCount = g.Count(q => q.Difficulty == DifficultyLevel.Hard && !q.IsRejected),
                    VeryHardCount = g.Count(q => q.Difficulty == DifficultyLevel.VeryHard && !q.IsRejected)
                })
                .ToDictionaryAsync(x => x.SectionId);

            var sectionStats = sectionMeta.Select(s =>
            {
                questionSectionStats.TryGetValue(s.SectionId, out var q);

                return new QuestionBankSectionStats
                {
                    CurriculumId = s.CurriculumId,
                    CurriculumTitle = s.CurriculumTitle,
                    SectionId = s.SectionId,
                    SectionTitle = s.SectionTitle,
                    LessonsCount = s.LessonsCount,
                    QuestionsCount = q?.QuestionsCount ?? 0,
                    ApprovedCount = q?.ApprovedCount ?? 0,
                    PendingReviewCount = q?.PendingReviewCount ?? 0,
                    MissingAnswersCount = q?.MissingAnswersCount ?? 0,
                    RejectedCount = q?.RejectedCount ?? 0,
                    EasyCount = q?.EasyCount ?? 0,
                    MediumCount = q?.MediumCount ?? 0,
                    HardCount = q?.HardCount ?? 0,
                    VeryHardCount = q?.VeryHardCount ?? 0
                };
            }).ToList();

            var stats = sectionStats
                .GroupBy(s => new { s.CurriculumId, s.CurriculumTitle })
                .Select(g => new QuestionBankCurriculumStats
                {
                    CurriculumId = g.Key.CurriculumId,
                    CurriculumTitle = g.Key.CurriculumTitle,
                    Sections = g.ToList(),
                    LessonsCount = g.Sum(s => s.LessonsCount),
                    QuestionsCount = g.Sum(s => s.QuestionsCount),
                    ApprovedCount = g.Sum(s => s.ApprovedCount),
                    PendingReviewCount = g.Sum(s => s.PendingReviewCount),
                    MissingAnswersCount = g.Sum(s => s.MissingAnswersCount),
                    RejectedCount = g.Sum(s => s.RejectedCount)
                })
                .ToList();

            var latestQuestionsForSimilarity = await _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected && !string.IsNullOrWhiteSpace(q.Title))
                .OrderByDescending(q => q.CreatedAt)
                .Take(300)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            var similarPairsCount = CountSimilarQuestionPairs(latestQuestionsForSimilarity.Select(q => (q.Id, q.Title ?? string.Empty)).ToList(), 0.78);

            var model = new QuestionBankDashboardViewModel
            {
                TotalQuestions = totalQuestions,
                TotalLessons = totalLessons,
                TotalSections = totalSections,
                AverageQuestionsPerLesson = totalLessons == 0 ? 0 : Math.Round((decimal)totalQuestions / totalLessons, 1),
                SimilarQuestionsCount = similarPairsCount,
                SimilarQuestionsSampleSize = latestQuestionsForSimilarity.Count,
                StatusCards = new List<QuestionBankStatusCard>
                {
                    new() { Title = "الأسئلة المعتمدة", Count = summary?.Approved ?? 0, Icon = "fa-circle-check", Color = "#198754", Url = Url.Action("Index", "Questions", new { area = "Admin" }) ?? "#", Description = "جاهزة للاستخدام في الاختبارات والواجبات" },
                    new() { Title = "قيد المراجعة", Count = summary?.PendingReview ?? 0, Icon = "fa-hourglass-half", Color = "#f59f00", Url = Url.Action("PendingReview", "Questions", new { area = "Admin" }) ?? "#", Description = "مكتملة وتنتظر اعتماد الإدارة" },
                    new() { Title = "بدون إجابات", Count = summary?.MissingAnswers ?? 0, Icon = "fa-triangle-exclamation", Color = "#dc3545", Url = Url.Action("MissingAnswers", "Questions", new { area = "Admin" }) ?? "#", Description = "أسئلة مكتملة لكن لا تملك إجابة صحيحة" },
                    new() { Title = "مرفوضة/غير مكتملة", Count = (summary?.Rejected ?? 0) + (summary?.Incomplete ?? 0), Icon = "fa-ban", Color = "#6c757d", Url = Url.Action("PendingApproval", "Questions", new { area = "Admin" }) ?? "#", Description = "تحتاج قرار أو استكمال قبل دخول البنك" },
                    new() { Title = "إجابة غير مؤكدة", Count = summary?.UnconfirmedAnswers ?? 0, Icon = "fa-spell-check", Color = "#0dcaf0", Url = Url.Action("Index", "Questions", new { area = "Admin", onlyUnanswered = false }) ?? "#", Description = "للمراجعة الدقيقة قبل الاختبارات الرسمية" },
                    new() { Title = "متشابهات محتملة", Count = similarPairsCount, Icon = "fa-code-compare", Color = "#7952b3", Url = Url.Action("SimilarGroups", "Questions", new { area = "Admin" }) ?? "#", Description = $"محسوبة من آخر {latestQuestionsForSimilarity.Count} سؤال" }
                },
                DifficultyStats = difficultyStatsRaw
                    .OrderBy(x => x.Difficulty)
                    .Select(x => new QuestionBankChartItem
                    {
                        Label = GetDifficultyLabel(x.Difficulty),
                        Value = x.Count,
                        Url = Url.Action("Index", "Questions", new { area = "Admin" }) ?? "#"
                    })
                    .ToList(),
                CurriculumCoverageStats = stats
                    .OrderByDescending(c => c.QuestionsCount)
                    .Select(c => new QuestionBankChartItem
                    {
                        Label = c.CurriculumTitle,
                        Value = c.QuestionsCount,
                        Url = Url.Action("Index", "Questions", new { area = "Admin", curriculumId = c.CurriculumId }) ?? "#"
                    })
                    .ToList(),
                QuestionBankStatsByCurriculum = stats
            };

            return View("Dashboard", model);
        }

        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> SimilarGroups(
            int? curriculumId,
            int? sectionId,
            int? lessonId,
            string? searchTitle,
            double threshold = 0.78,
            int? scanLimit = null,
            bool run = false)
        {
            threshold = Math.Clamp(threshold, 0.60, 0.98);

            using var _context = _contextFactory.CreateDbContext();
            var hasFilter =
                curriculumId.HasValue ||
                sectionId.HasValue ||
                lessonId.HasValue ||
                !string.IsNullOrWhiteSpace(searchTitle);

            var shouldRunScan = run || hasFilter;

            var shouldScanFullScope =
                curriculumId.HasValue ||
                sectionId.HasValue ||
                lessonId.HasValue ||
                !string.IsNullOrWhiteSpace(searchTitle);
            var effectiveScanLimit = shouldScanFullScope
                ? (int?)null
                : Math.Clamp(scanLimit ?? 1200, 200, 5000);

            var model = shouldRunScan
                ? await BuildSimilarQuestionGroupsAsync(
                    _context,
                    threshold,
                    effectiveScanLimit,
                    80,
                    curriculumId,
                    sectionId,
                    lessonId,
                    searchTitle)
                : new SimilarQuestionGroupsViewModel
                {
                    CurriculumId = curriculumId,
                    SectionId = sectionId,
                    LessonId = lessonId,
                    SearchTitle = searchTitle,
                    Threshold = threshold,
                    ScanLimit = effectiveScanLimit,
                    HasRun = false,
                    ScopeDescription = "اختر منهجًا أو محورًا أو مؤشرًا ثم ابدأ الفحص"
                };

            model.Curriculums = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title,
                    Selected = curriculumId.HasValue && c.Id == curriculumId.Value
                })
                .ToListAsync();

            model.Sections = await _context.Sections
                .AsNoTracking()
                .Where(s => curriculumId.HasValue && s.CurriculumId == curriculumId.Value)
                .OrderBy(s => s.Title)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Title,
                    Selected = sectionId.HasValue && s.Id == sectionId.Value
                })
                .ToListAsync();

            model.Lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.IsActive)
                .Where(l =>
                    (sectionId.HasValue && l.SectionId == sectionId.Value) ||
                    (!sectionId.HasValue && curriculumId.HasValue && l.Section.CurriculumId == curriculumId.Value))
                .OrderBy(l => l.Title)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Title,
                    Selected = lessonId.HasValue && l.Id == lessonId.Value
                })
                .ToListAsync();

            return View("SimilarGroups", model);
        }

        private static string BuildQuestionSimilarityText(string? title, string? valueA, string? valueB, string? correctAnswer)
        {
            return string.Join(" ",
                new[] { title, valueA, valueB, correctAnswer }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string CleanQuestionTitleForDisplay(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var decoded = WebUtility.HtmlDecode(title);
            decoded = Regex.Replace(decoded, "<span[^>]*display\\s*:\\s*none[^>]*>.*?</span>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            decoded = Regex.Replace(decoded, "<[^>]+>", " ", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, @"<?/?(?:span|sup|sub|div|p|td|tr|table|tbody|thead|strong|b|i)\b[^>]*>?", " ", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, @"\s+", " ").Trim();

            return decoded;
        }

        private static async Task<List<SimilarQuestionCandidate>> LoadSimilarityCandidatesAsync(
            ApplicationDbContext context,
            Guid sourceQuestionId,
            int sectionId)
        {
            const int SameSectionLimit = 6000;

            var results = new Dictionary<Guid, SimilarQuestionCandidate>();

            var baseQuery = context.Questions
                .AsNoTracking()
                .Where(q => q.Id != sourceQuestionId && !q.IsRejected && !string.IsNullOrWhiteSpace(q.Title));

            var sameSection = await ProjectSimilarityCandidates(baseQuery.Where(q => q.SectionId == sectionId), 1)
                .OrderByDescending(q => q.CreatedAt)
                .Take(SameSectionLimit)
                .ToListAsync();
            AddSimilarityCandidates(results, sameSection);

            return results.Values.ToList();
        }

        private static IQueryable<SimilarQuestionCandidate> ProjectSimilarityCandidates(IQueryable<Question> query, int scopeRank)
        {
            return query.Select(q => new SimilarQuestionCandidate
            {
                Id = q.Id,
                ReferenceNumber = q.ReferenceNumber,
                Title = q.Title,
                ValueA = q.ValueA,
                ValueB = q.ValueB,
                CorrectAnswer = q.CorrectAnswer,
                CurriculumId = q.CurriculumId,
                CurriculumTitle = q.Curriculum.Title,
                SectionId = q.SectionId,
                SectionTitle = q.Section != null ? q.Section.Title : null,
                LessonId = q.LessonId,
                LessonTitle = q.Lesson.Title,
                CreatedAt = q.CreatedAt,
                ScopeRank = scopeRank
            });
        }

        private static void AddSimilarityCandidates(
            Dictionary<Guid, SimilarQuestionCandidate> results,
            IEnumerable<SimilarQuestionCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (results.TryGetValue(candidate.Id, out var existing))
                {
                    if (candidate.ScopeRank < existing.ScopeRank)
                        existing.ScopeRank = candidate.ScopeRank;

                    continue;
                }

                results[candidate.Id] = candidate;
            }
        }

        private static bool ShouldCompareWithSource(
            string sourceNormalizedText,
            HashSet<string> sourceTokens,
            SimilarQuestionCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate.NormalizedText))
                return false;

            if (sourceNormalizedText == candidate.NormalizedText)
                return true;

            var lengthDifference = Math.Abs(sourceNormalizedText.Length - candidate.NormalizedText.Length);
            if (lengthDifference <= 12)
                return true;

            if (sourceTokens.Count == 0 || candidate.Tokens.Count == 0)
                return lengthDifference <= 30;

            if (sourceTokens.Overlaps(candidate.Tokens))
                return true;

            var maxLength = Math.Max(sourceNormalizedText.Length, candidate.NormalizedText.Length);
            return maxLength > 0 && ((double)lengthDifference / maxLength) <= 0.18;
        }

        private static async Task<SimilarQuestionGroupsViewModel> BuildSimilarQuestionGroupsAsync(
            ApplicationDbContext context,
            double threshold,
            int? scanLimit,
            int maxGroups,
            int? curriculumId = null,
            int? sectionId = null,
            int? lessonId = null,
            string? searchTitle = null)
        {
            var query = context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected && !string.IsNullOrWhiteSpace(q.Title));

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);

            if (sectionId.HasValue)
                query = query.Where(q => q.SectionId == sectionId.Value);

            if (lessonId.HasValue)
                query = query.Where(q => q.LessonId == lessonId.Value);

            if (!string.IsNullOrWhiteSpace(searchTitle))
                query = query.Where(q => q.Title != null && q.Title.Contains(searchTitle.Trim()));

            var orderedQuery = query.OrderByDescending(q => q.CreatedAt).AsQueryable();
            if (scanLimit.HasValue)
                orderedQuery = orderedQuery.Take(scanLimit.Value);

            var candidates = await orderedQuery
                .Select(q => new SimilarQuestionCandidate
                {
                    Id = q.Id,
                    ReferenceNumber = q.ReferenceNumber,
                    Title = q.Title,
                    ValueA = q.ValueA,
                    ValueB = q.ValueB,
                    CorrectAnswer = q.CorrectAnswer,
                    CurriculumId = q.CurriculumId,
                    CurriculumTitle = q.Curriculum.Title,
                    SectionId = q.SectionId,
                    SectionTitle = q.Section != null ? q.Section.Title : null,
                    LessonId = q.LessonId,
                    LessonTitle = q.Lesson.Title,
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();

            foreach (var candidate in candidates)
            {
                candidate.CombinedText = BuildQuestionSimilarityText(
                    candidate.Title,
                    candidate.ValueA,
                    candidate.ValueB,
                    candidate.CorrectAnswer);

                candidate.NormalizedText = TextSimilarityHelper.NormalizeForSimilarity(candidate.CombinedText);
                candidate.Tokens = TextSimilarityHelper.GetSignificantTokens(candidate.CombinedText).ToHashSet(StringComparer.Ordinal);
            }

            candidates = candidates
                .Where(q => !string.IsNullOrWhiteSpace(q.NormalizedText))
                .ToList();

            var unionFind = new SimilarQuestionUnionFind(candidates.Count);
            var edges = new Dictionary<(int Left, int Right), TextSimilarityResult>();

            for (var i = 0; i < candidates.Count; i++)
            {
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    if (!ShouldCompareForSimilarity(candidates[i], candidates[j]))
                        continue;

                    var analysis = TextSimilarityHelper.Analyze(candidates[i].CombinedText, candidates[j].CombinedText);
                    if (!analysis.IsReliableMatch || analysis.Score < threshold)
                        continue;

                    unionFind.Union(i, j);
                    edges[(i, j)] = analysis;
                }
            }

            var groups = Enumerable.Range(0, candidates.Count)
                .GroupBy(unionFind.Find)
                .Where(group => group.Count() > 1)
                .Select(group => BuildSimilarGroup(group.ToList(), candidates, edges))
                .OrderByDescending(group => group.MaxSimilarity)
                .ThenByDescending(group => group.QuestionsCount)
                .Take(maxGroups)
                .Select((group, index) =>
                {
                    group.GroupNumber = index + 1;
                    return group;
                })
                .ToList();

            return new SimilarQuestionGroupsViewModel
            {
                CurriculumId = curriculumId,
                SectionId = sectionId,
                LessonId = lessonId,
                SearchTitle = searchTitle,
                ScannedQuestionsCount = candidates.Count,
                GroupsCount = groups.Count,
                SimilarPairsCount = edges.Count,
                Threshold = threshold,
                ScanLimit = scanLimit,
                IsLimitedBySample = scanLimit.HasValue,
                HasRun = true,
                ScopeDescription = ResolveSimilarityScopeDescription(curriculumId, sectionId, lessonId, searchTitle, scanLimit),
                GeneratedAt = DateTime.Now,
                Groups = groups
            };
        }

        private static string ResolveSimilarityScopeDescription(
            int? curriculumId,
            int? sectionId,
            int? lessonId,
            string? searchTitle,
            int? scanLimit)
        {
            if (lessonId.HasValue)
                return "كل أسئلة المؤشر المحدد";

            if (sectionId.HasValue)
                return "كل أسئلة المحور المحدد";

            if (curriculumId.HasValue)
                return "كل أسئلة المنهج المحدد";

            if (!string.IsNullOrWhiteSpace(searchTitle))
                return "كل الأسئلة المطابقة للبحث";

            return scanLimit.HasValue ? $"أحدث {scanLimit.Value} سؤال في البنك" : "كل بنك الأسئلة";
        }

        private static bool ShouldCompareForSimilarity(SimilarQuestionCandidate left, SimilarQuestionCandidate right)
        {
            if (left.LessonId == right.LessonId || left.SectionId == right.SectionId)
                return true;

            if (left.CurriculumId != right.CurriculumId && Math.Abs(left.NormalizedText.Length - right.NormalizedText.Length) > 40)
                return false;

            if (left.Tokens.Count == 0 || right.Tokens.Count == 0)
                return Math.Abs(left.NormalizedText.Length - right.NormalizedText.Length) <= 12;

            return left.Tokens.Overlaps(right.Tokens);
        }

        private static SimilarQuestionGroupViewModel BuildSimilarGroup(
            List<int> indexes,
            List<SimilarQuestionCandidate> candidates,
            Dictionary<(int Left, int Right), TextSimilarityResult> edges)
        {
            var groupEdges = new List<((int Left, int Right) Pair, TextSimilarityResult Analysis)>();
            for (var i = 0; i < indexes.Count; i++)
            {
                for (var j = i + 1; j < indexes.Count; j++)
                {
                    var key = NormalizePair(indexes[i], indexes[j]);
                    if (edges.TryGetValue(key, out var analysis))
                        groupEdges.Add((key, analysis));
                }
            }

            var representativeIndex = indexes
                .OrderByDescending(index => groupEdges.Count(edge => edge.Pair.Left == index || edge.Pair.Right == index))
                .ThenByDescending(index => candidates[index].CreatedAt)
                .First();

            var representative = candidates[representativeIndex];
            var items = indexes
                .Select(index =>
                {
                    var candidate = candidates[index];
                    var isRepresentative = index == representativeIndex;
                    var analysis = isRepresentative
                        ? new TextSimilarityResult { Score = 1, MatchType = "السؤال المرجعي", IsReliableMatch = true }
                        : TextSimilarityHelper.Analyze(representative.CombinedText, candidate.CombinedText);

                    return new SimilarQuestionItemViewModel
                    {
                        Id = candidate.Id,
                        ReferenceNumber = string.IsNullOrWhiteSpace(candidate.ReferenceNumber) ? "—" : candidate.ReferenceNumber,
                        Title = candidate.Title ?? string.Empty,
                        CurriculumTitle = candidate.CurriculumTitle ?? "—",
                        SectionTitle = candidate.SectionTitle ?? "—",
                        LessonTitle = candidate.LessonTitle ?? "—",
                        CreatedAt = candidate.CreatedAt,
                        SimilarityToRepresentative = Math.Round(analysis.Score * 100, 1),
                        MatchType = analysis.MatchType,
                        IsRepresentative = isRepresentative
                    };
                })
                .OrderByDescending(item => item.IsRepresentative)
                .ThenByDescending(item => item.SimilarityToRepresentative)
                .ToList();

            var edgeScores = groupEdges.Select(edge => edge.Analysis.Score * 100).ToList();

            return new SimilarQuestionGroupViewModel
            {
                QuestionsCount = items.Count,
                PairsCount = groupEdges.Count,
                AverageSimilarity = edgeScores.Count == 0 ? 0 : Math.Round(edgeScores.Average(), 1),
                MaxSimilarity = edgeScores.Count == 0 ? 0 : Math.Round(edgeScores.Max(), 1),
                CurriculumTitle = representative.CurriculumTitle ?? "—",
                SectionTitle = representative.SectionTitle ?? "—",
                LessonTitle = representative.LessonTitle ?? "—",
                Questions = items
            };
        }

        private static (int Left, int Right) NormalizePair(int left, int right)
        {
            return left < right ? (left, right) : (right, left);
        }

        private sealed class SimilarQuestionCandidate
        {
            public Guid Id { get; set; }
            public string? ReferenceNumber { get; set; }
            public string? Title { get; set; }
            public string? ValueA { get; set; }
            public string? ValueB { get; set; }
            public string? CorrectAnswer { get; set; }
            public int CurriculumId { get; set; }
            public string? CurriculumTitle { get; set; }
            public int? SectionId { get; set; }
            public string? SectionTitle { get; set; }
            public int LessonId { get; set; }
            public string? LessonTitle { get; set; }
            public DateTime CreatedAt { get; set; }
            public int ScopeRank { get; set; }
            public string CombinedText { get; set; } = string.Empty;
            public string NormalizedText { get; set; } = string.Empty;
            public HashSet<string> Tokens { get; set; } = new(StringComparer.Ordinal);
        }

        private sealed class SimilarQuestionUnionFind
        {
            private readonly int[] _parent;
            private readonly int[] _rank;

            public SimilarQuestionUnionFind(int size)
            {
                _parent = Enumerable.Range(0, size).ToArray();
                _rank = new int[size];
            }

            public int Find(int value)
            {
                if (_parent[value] != value)
                    _parent[value] = Find(_parent[value]);

                return _parent[value];
            }

            public void Union(int left, int right)
            {
                var leftRoot = Find(left);
                var rightRoot = Find(right);

                if (leftRoot == rightRoot)
                    return;

                if (_rank[leftRoot] < _rank[rightRoot])
                {
                    _parent[leftRoot] = rightRoot;
                    return;
                }

                if (_rank[leftRoot] > _rank[rightRoot])
                {
                    _parent[rightRoot] = leftRoot;
                    return;
                }

                _parent[rightRoot] = leftRoot;
                _rank[leftRoot]++;
            }
        }

        private static int CountSimilarQuestionPairs(List<(Guid Id, string Title)> questions, double threshold)
        {
            var count = 0;

            for (var i = 0; i < questions.Count; i++)
            {
                for (var j = i + 1; j < questions.Count; j++)
                {
                    if (TextSimilarityHelper.CalculateSimilarity(questions[i].Title, questions[j].Title) >= threshold)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static string GetDifficultyLabel(DifficultyLevel difficulty)
        {
            return difficulty switch
            {
                DifficultyLevel.Easy => "سهل",
                DifficultyLevel.Medium => "متوسط",
                DifficultyLevel.Hard => "صعب",
                DifficultyLevel.VeryHard => "صعب جدًا",
                _ => difficulty.ToString()
            };
        }





        private string GetArabicLabel(QuestionUsageType type)
        {
            return type switch
            {
                QuestionUsageType.Assignment => "واجب",
                QuestionUsageType.Enhancement => "مهارة",
                QuestionUsageType.QdratExam => "اختبار قدرات",
                QuestionUsageType.OfficialMockExam => "محاكاة وزارية",
                QuestionUsageType.PlacementTest => "اختبار مقياس",
                QuestionUsageType.PromoTest => "اختبار ترويجي",
                _ => "غير مصنف"
            };
        }

        private static string GetPassageTypeIcon(PassageType type)
        {
            return type switch
            {
                PassageType.Audio => "🎧 ",
                PassageType.Video => "🎥 ",
                _ => "📄 "
            };
        }

        private static List<QuestionOptionViewModel> BuildOptionsForEdit(List<QuestionOption> dbOptions)
        {
            var result = new List<QuestionOptionViewModel>();

            if (dbOptions != null && dbOptions.Count > 0)
            {
                foreach (var option in dbOptions.OrderBy(o => o.Id))
                {
                    result.Add(new QuestionOptionViewModel
                    {
                        Text = string.IsNullOrWhiteSpace(option.Text) || option.Text.Trim() == "[صورة]"
                            ? string.Empty
                            : option.Text.Trim(),

                        ExistingImageUrl = string.IsNullOrWhiteSpace(option.ImageUrl)
                            ? null
                            : option.ImageUrl.Trim(),

                        ImageUrl = string.IsNullOrWhiteSpace(option.ImageUrl)
                            ? null
                            : option.ImageUrl.Trim(),

                        RemoveImage = false
                    });
                }
            }

            while (result.Count < 4)
            {
                result.Add(new QuestionOptionViewModel
                {
                    Text = string.Empty,
                    ExistingImageUrl = null,
                    ImageUrl = null,
                    RemoveImage = false
                });
            }

            return result.Take(4).ToList();
        }

        private static async Task<List<SelectListItem>> BuildVerbalPassageSelectListAsync(ApplicationDbContext context, int? selectedId = null)
        {
            var passages = await context.VerbalPassages
                .AsNoTracking()
                .OrderBy(v => v.Title)
                .Select(v => new
                {
                    v.Id,
                    v.Title,
                    v.Type
                })
                .ToListAsync();

            return passages.Select(v => new SelectListItem
            {
                Value = v.Id.ToString(),
                Text = GetPassageTypeIcon(v.Type) + v.Title,
                Selected = selectedId.HasValue && selectedId.Value == v.Id
            }).ToList();
        }

        private static string? ResolvePreviewImageUrl(string? imageUrl, string? existingImageUrl, bool removeImage)
        {
            if (removeImage)
                return null;

            if (!string.IsNullOrWhiteSpace(imageUrl))
                return imageUrl;

            if (!string.IsNullOrWhiteSpace(existingImageUrl))
                return existingImageUrl;

            return null;
        }


        [HttpPost]
        [AdminPermission("Questions", "Approve")]
        public IActionResult PreviewFromCreate([FromBody] QuestionCreateViewModel createModel)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ======================================================
            // 1️⃣ تحديد اتجاه المنهج + الكمي
            // ======================================================
            bool isRTL = true;
            bool isQuantitative = false;

            if (createModel.CurriculumId > 0)
            {
                var curriculum = _context.Curriculums
                    .Where(c => c.Id == createModel.CurriculumId)
                    .Select(c => new { c.IsRTL, c.IsQuantitative })
                    .FirstOrDefault();

                if (curriculum != null)
                {
                    isRTL = curriculum.IsRTL;
                    isQuantitative = curriculum.IsQuantitative;
                }
            }

            // ======================================================
            // 2️⃣ دالة تحويل الأرقام
            // ======================================================
            string ConvertIfNeeded(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return input;

                return isQuantitative
                    ? NumberHelper.ToIndicNumbersSafe(input)
                    : input;
            }

            // ======================================================
            // 3️⃣ تجهيز الاختيارات
            // ======================================================
            var options = new List<QuestionOptionDisplayViewModel>();

            if (createModel.Options != null)
            {
                options = createModel.Options
                    .Select(o =>
                    {
                        var imageUrl = ResolvePreviewImageUrl(o.ImageUrl, o.ExistingImageUrl, o.RemoveImage);
                        var text = o.Text;
                        if (string.Equals(text?.Trim(), "[صورة]", StringComparison.OrdinalIgnoreCase)
                            && string.IsNullOrWhiteSpace(imageUrl))
                        {
                            text = string.Empty;
                        }

                        return new QuestionOptionDisplayViewModel
                        {
                            Text = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(text)),
                            ImageUrl = imageUrl
                        };
                    }).ToList();
            }

            // ======================================================
            // 🔥 4️⃣ جلب القطعة اللفظية (المهم)
            // ======================================================
            string? passageContent = null;
            string? passageMediaUrl = null;
            PassageType? passageType = null;
            int? passageDuration = null;

            if (createModel.VerbalPassageId.HasValue)
            {
                var passage = _context.VerbalPassages
                    .Where(v => v.Id == createModel.VerbalPassageId.Value)
                    .Select(v => new
                    {
                        v.Content,
                        v.MediaUrl,
                        v.Type,
                        v.DurationSeconds
                    })
                    .FirstOrDefault();

                if (passage != null)
                {
                    passageContent = passage.Content;
                    passageMediaUrl = passage.MediaUrl;
                    passageType = passage.Type;
                    passageDuration = passage.DurationSeconds;
                }
            }

            // ======================================================
            // 5️⃣ إنشاء ViewModel
            // ======================================================
            var displayModel = new QuestionDisplayViewModel
            {
                Id = Guid.NewGuid(),

                Title = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.Title)),
                ComparisonValue1 = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.ValueA)),
                ComparisonValue2 = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.ValueB)),

                Explanation = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.Explanation)),

                ImageUrl = ResolvePreviewImageUrl(createModel.ImageUrl, createModel.ExistingImageUrl, createModel.RemoveImage),

                IsQuantitative = isQuantitative,
                IsRTL = isRTL,

                VideoUrl = createModel.VideoUrl,
                CorrectAnswer = createModel.CorrectAnswer,
                SelectedCorrectIndex = createModel.SelectedCorrectIndex,
                IsAnswerConfirmed = createModel.SelectedCorrectIndex.HasValue,

                Template = (QuestionTemplate)createModel.Template,
                Difficulty = (DifficultyLevel)createModel.Difficulty,

                DisplayType = GetDisplayType(createModel),

                Options = options,

                // 🔥 الجديد (مهم جدًا)
                VerbalPassageContent = passageContent,
                VerbalPassageMediaUrl = passageMediaUrl,
                VerbalPassageType = passageType,
                VerbalPassageDuration = passageDuration
            };

            return PartialView("_QuestionPreviewPartial", displayModel);
        }




        private QuestionDisplayType GetDisplayType(QuestionCreateViewModel model)
        {
            var hasImage = false;
            var hasComparison = !string.IsNullOrWhiteSpace(model.ValueA) && !string.IsNullOrWhiteSpace(model.ValueB);
            var template = (QdratNew.Enums.QuestionTemplate)model.Template; // ✅ تحويل يدوي

            return template switch
            {
                QdratNew.Enums.QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                QdratNew.Enums.QuestionTemplate.WithImage => QuestionDisplayType.WithImage,
                QdratNew.Enums.QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                _ => QuestionDisplayType.TextOnly
            };

        }



        [HttpGet]
        [AdminPermission("Questions", "Add")]
        public async Task<IActionResult> Create()
        {
            using var _context = _contextFactory.CreateDbContext();

            var model = await BuildCreateQuestionModelAsync(_context);

            return View(model);
        }

        [HttpGet]
        [AdminPermission("Questions", "Add")]
        public async Task<IActionResult> CreateOld()
        {
            using var _context = _contextFactory.CreateDbContext();

            var model = await BuildCreateQuestionModelAsync(_context);

            return View("CreateOld", model);
        }

        private async Task<QuestionCreateViewModel> BuildCreateQuestionModelAsync(ApplicationDbContext context)
        {
            return new QuestionCreateViewModel
            {
                Curriculums = await context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync(),

                Sections = new List<SelectListItem>(),
                Lessons = new List<SelectListItem>(),

                VerbalPassages = await BuildVerbalPassageSelectListAsync(context),

                Options = new List<QuestionOptionViewModel>
                {
                    new QuestionOptionViewModel(),
                    new QuestionOptionViewModel(),
                    new QuestionOptionViewModel(),
                    new QuestionOptionViewModel()
                }
            };
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions", "Add")]
        public async Task<IActionResult> Create(QuestionCreateViewModel model)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "يجب كتابة نص السؤال.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                model.Curriculums = await _context.Curriculums.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title }).ToListAsync();
                model.VerbalPassages = await BuildVerbalPassageSelectListAsync(_context, model.VerbalPassageId);
                model.Sections = await _context.Sections.Where(s => s.CurriculumId == model.CurriculumId).Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title }).ToListAsync();
                model.Lessons = await _context.Lessons.Where(l => l.Unit.SectionUnits.Any(su => su.SectionId == model.SectionId) && l.IsActive).Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Title }).ToListAsync();

                return View(model);
            }

            var isQuantitative = await _context.Curriculums
                                 .Where(c => c.Id == model.CurriculumId)
                                 .Select(c => c.IsQuantitative)
                                 .FirstOrDefaultAsync();

            int nextNumber = _context.Questions.AsEnumerable()
                .Where(q => !string.IsNullOrWhiteSpace(q.ReferenceNumber) && q.ReferenceNumber.All(char.IsDigit))
                .Select(q => int.Parse(q.ReferenceNumber))
                .DefaultIfEmpty(0)
                .Max() + 1;


            var user = await _context.Users.OfType<ApplicationUser>()
    .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            UserRoleType userRole =
     User.IsInRole("Developer") ? UserRoleType.Developer :
     User.IsInRole("Partner") ? UserRoleType.Partner :
     User.IsInRole("Admin") ? UserRoleType.Admin :
     User.IsInRole("Employee") ? UserRoleType.Employee :
     User.IsInRole("SuperAdmin") ? UserRoleType.SuperAdmin :
     User.IsInRole("Instructor") ? UserRoleType.Instructor :
     User.IsInRole("DataEntry") ? UserRoleType.DataEntry :
     User.IsInRole("Student") ? UserRoleType.Student :
     User.IsInRole("Owner") ? UserRoleType.Owner :
     UserRoleType.Unknown;


            if (model.VerbalPassageId != null && model.PassageStartSeconds == null)
            {
                model.PassageStartSeconds = 0;
            }



            var question = new Question
            {
                Id = Guid.NewGuid(),
                ReferenceNumber = nextNumber.ToString(),
                Title = model.Title,
                Template = (QuestionTemplate)model.Template,
                ValueA = model.ValueA,
                ValueB = model.ValueB,
                Explanation = model.Explanation,
                VideoUrl = model.VideoUrl,
                CurriculumId = model.CurriculumId,
                SectionId = model.SectionId,
                LessonId = model.LessonId,
                VerbalPassageId = model.VerbalPassageId,

                // =========================
                // 🔥 Media Segmentation (الإضافة المهمة)
                // =========================
                PassageStartSeconds = model.PassageStartSeconds,
                PassageEndSeconds = model.PassageEndSeconds,

                Difficulty = (DifficultyLevel)model.Difficulty,
                CreatedAt = DateTime.Now,
                ImageUrl = await SaveImageAsync(model.ImageFile),
                IsQuantitative = isQuantitative,
                IsReviewed = false,
                Hint = model.Hint,
                IsAnswerConfirmed = false,
                InternalNote = model.InternalNote,
                UsageTypes = model.SelectedUsageTypes.Aggregate(QuestionUsageType.None, (acc, t) => acc | t),
                CreatedByName = user?.FullName ?? user?.UserName ?? "SYSTEM",
                Options = new List<QuestionOption>()
            };

            for (int i = 0; i < model.Options.Count; i++)
            {
                var opt = model.Options[i];
                var imageUrl = await SaveImageAsync(opt.ImageFile);

                var optionText = !string.IsNullOrWhiteSpace(opt.Text) ? opt.Text : string.Empty;

                var newOpt = new QuestionOption
                {
                    Text = optionText,
                    ImageUrl = imageUrl
                };
                question.Options.Add(newOpt);

                if (i == model.SelectedCorrectIndex)
                {
                    // للاختيارات الصورية: نستخدم رابط الصورة كمعرف للإجابة الصحيحة
                    question.CorrectAnswer = !string.IsNullOrWhiteSpace(optionText) ? optionText : imageUrl;
                }
            }

            question.IsAnswerConfirmed = !string.IsNullOrWhiteSpace(question.CorrectAnswer);
            question.IsComplete = question.Options.Any(o => !string.IsNullOrWhiteSpace(o.Text) || !string.IsNullOrWhiteSpace(o.ImageUrl));

            _context.Questions.Add(question);

            // ✅ أول تسجيل في سجل التعديلات عند الإنشاء
            _context.QuestionAuditLogs.Add(new QuestionAuditLog
            {
                QuestionId = question.Id,
                Action = "إنشاء",
                PerformedByUserId = user?.Id ?? "SYSTEM",
                PerformedByName = user?.FullName ?? user?.UserName ?? "SYSTEM",
                PerformedByRole = userRole,
                ChangedFieldsSummary = "تم إنشاء السؤال.",
                PerformedAt = DateTime.Now
            });



            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ تم إضافة السؤال بنجاح.";
            return RedirectToAction(nameof(Create));
        }



        private string NormalizeTextDirection(string? input, bool isRTL)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty;

            input = input.Trim();

            // إزالة أي dir سابق
            input = input.Replace("dir=\"rtl\"", "")
                         .Replace("dir=\"ltr\"", "");

            // إضافة الاتجاه الصحيح
            return isRTL
                ? $"<div dir=\"rtl\">{input}</div>"
                : $"<div dir=\"ltr\">{input}</div>";
        }





        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> Display(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions
          .Include(q => q.Options)
          .Include(q => q.Curriculum) // ✅ مهم
          .Include(q => q.Lesson)
              .ThenInclude(l => l.Section)
          .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return NotFound();

            var viewModel = new QuestionDisplayViewModel
            {
                Id = question.Id,
                Title = question.Title,
                ComparisonValue1 = question.ValueA,
                ComparisonValue2 = question.ValueB,
                ImageUrl = question.ImageUrl,
                Explanation = question.Explanation,
                VideoUrl = question.VideoUrl,
                Template = question.Template,
                Difficulty = question.Difficulty,
                IsQuantitative = question.IsQuantitative,
                IsRTL = question.Curriculum != null ? question.Curriculum.IsRTL : true,
                // ✅ الآن نربط المحور والمؤشر
                LessonTitle = question.Lesson != null ? question.Lesson.Title : "",
                SectionTitle = question.Lesson != null && question.Lesson.Section != null ? question.Lesson.Section.Title : "",

                Options = question.Options.Select(o => new QuestionOptionDisplayViewModel
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList()
            };

            return View(viewModel);
        }


        private async Task<string?> SaveImageAsync(IFormFile? file)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (file == null || file.Length == 0)
                return null;

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/questions");
            Directory.CreateDirectory(uploadsFolder); // يتأكد أن المجلد موجود

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/uploads/questions/" + uniqueFileName;
        }

        private const string QuestionUploadVirtualPath = "/uploads/questions/";

        private static bool IsLocalQuestionUploadUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return false;

            return imageUrl.Replace('\\', '/')
                .StartsWith(QuestionUploadVirtualPath, StringComparison.OrdinalIgnoreCase);
        }

        private string? GetQuestionImagePhysicalPath(string? imageUrl)
        {
            if (!IsLocalQuestionUploadUrl(imageUrl))
                return null;

            var fileName = Path.GetFileName(imageUrl!.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var uploadsFolder = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "questions"));

            var filePath = Path.GetFullPath(Path.Combine(uploadsFolder, fileName));
            var allowedPrefix = uploadsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!filePath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            return filePath;
        }

        private static void AddQuestionImageForDeletion(HashSet<string> imageUrls, string? imageUrl)
        {
            if (IsLocalQuestionUploadUrl(imageUrl))
                imageUrls.Add(imageUrl!);
        }

        private void DeleteQuestionImageFiles(IEnumerable<string> imageUrls, IEnumerable<string?> retainedImageUrls)
        {
            var retained = retainedImageUrls
                .Where(IsLocalQuestionUploadUrl)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var imageUrl in imageUrls.Where(IsLocalQuestionUploadUrl).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (retained.Contains(imageUrl))
                    continue;

                var filePath = GetQuestionImagePhysicalPath(imageUrl);
                if (filePath == null)
                    continue;

                try
                {
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }
                catch
                {
                    // حذف الملف لا يجب أن يفشل حفظ السؤال بعد نجاح قاعدة البيانات.
                }
            }
        }

        [HttpGet]
        [AdminPermission("Questions", "Edit")]
        public async Task<IActionResult> Edit(Guid id)
        {
            ModelState.Clear();

            using var _context = _contextFactory.CreateDbContext();

            var model = await BuildEditQuestionModelAsync(_context, id);

            if (model == null)
                return NotFound();

            return View("Edit", model);
        }

        [HttpGet]
        [AdminPermission("Questions", "Edit")]
        public async Task<IActionResult> EditOld(Guid id)
        {
            ModelState.Clear();

            using var _context = _contextFactory.CreateDbContext();

            var model = await BuildEditQuestionModelAsync(_context, id);

            if (model == null)
                return NotFound();

            return View("EditOld", model);
        }


        private async Task<QuestionCreateViewModel?> BuildEditQuestionModelAsync(ApplicationDbContext _context, Guid id)
        {
            var question = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Curriculum)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return null;

            var optionsList = await _context.QuestionOptions
                .AsNoTracking()
                .Where(o => o.QuestionId == id)
                .OrderBy(o => o.Id)
                .ToListAsync();

            var selectedTypes = Enum.GetValues(typeof(QuestionUsageType))
                .Cast<QuestionUsageType>()
                .Where(t => t != QuestionUsageType.None && question.UsageTypes.HasFlag(t))
                .ToList();

            int? selectedCorrectIndex = null;

            if (!string.IsNullOrWhiteSpace(question.CorrectAnswer))
            {
                var correctAnswer = question.CorrectAnswer.Trim();

                selectedCorrectIndex = optionsList.FindIndex(o =>
                    (!string.IsNullOrWhiteSpace(o.Text) && o.Text.Trim() == correctAnswer) ||
                    (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl.Trim() == correctAnswer)
                );

                if (selectedCorrectIndex < 0)
                    selectedCorrectIndex = null;
            }

            var model = new QuestionCreateViewModel
            {
                Id = question.Id,
                Title = question.Title,
                Template = (QuestionTemplate)question.Template,
                ValueA = question.ValueA,
                ValueB = question.ValueB,
                CorrectAnswer = question.CorrectAnswer,
                SelectedCorrectIndex = selectedCorrectIndex,
                Explanation = question.Explanation,
                VideoUrl = question.VideoUrl,
                Hint = question.Hint,

                CurriculumId = question.CurriculumId,
                SectionId = question.SectionId ?? 0,
                LessonId = question.LessonId,
                VerbalPassageId = question.VerbalPassageId,

                PassageStartSeconds = question.PassageStartSeconds,
                PassageEndSeconds = question.PassageEndSeconds,

                Difficulty = (DifficultyLevel)question.Difficulty,
                SelectedUsageTypes = selectedTypes,
                InternalNote = question.InternalNote,
                ExistingImageUrl = question.ImageUrl,

                IsQuantitative = question.Curriculum?.IsQuantitative ?? false,
                IsRTL = question.Curriculum?.IsRTL ?? true,

                Curriculums = await _context.Curriculums
                    .AsNoTracking()
                    .OrderBy(c => c.Title)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Title,
                        Selected = c.Id == question.CurriculumId
                    })
                    .ToListAsync(),

                Sections = await _context.Sections
                    .AsNoTracking()
                    .Where(s => s.CurriculumId == question.CurriculumId)
                    .OrderBy(s => s.Title)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Title,
                        Selected = question.SectionId.HasValue && s.Id == question.SectionId.Value
                    })
                    .ToListAsync(),

                Lessons = await _context.Lessons
                    .AsNoTracking()
                    .Where(l => l.SectionId == question.SectionId && l.IsActive)
                    .OrderBy(l => l.Title)
                    .Select(l => new SelectListItem
                    {
                        Value = l.Id.ToString(),
                        Text = l.Title,
                        Selected = l.Id == question.LessonId
                    })
                    .ToListAsync(),

                VerbalPassages = await BuildVerbalPassageSelectListAsync(_context, question.VerbalPassageId),

                Options = BuildOptionsForEdit(optionsList)
            };

            ViewBag.PassageDuration = await _context.VerbalPassages
                .AsNoTracking()
                .Where(v => v.Id == question.VerbalPassageId)
                .Select(v => v.DurationSeconds)
                .FirstOrDefaultAsync();



            return model;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions", "Edit")]
        public async Task<IActionResult> Edit(Guid id, QuestionCreateViewModel model)
        {
            using var _context = _contextFactory.CreateDbContext();

            bool allowEmptyTitle = model.SectionId == 10;

            if (!allowEmptyTitle && string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "يجب كتابة نص السؤال لهذا القسم.");
            }

            if (!ModelState.IsValid)
            {
                model.Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync();
                model.VerbalPassages = await BuildVerbalPassageSelectListAsync(_context, model.VerbalPassageId);
                model.Sections = await _context.Sections
                    .Where(s => s.CurriculumId == model.CurriculumId)
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                    .ToListAsync();
                model.Lessons = await _context.Lessons
                    .Where(l => l.Unit.SectionUnits.Any(su => su.SectionId == model.SectionId) && l.IsActive)
                    .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Title })
                    .ToListAsync();
                return View("Edit", model);
            }

            var oldQuestion = await _context.Questions.Include(q => q.Options).AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id);

            var question = await _context.Questions.Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            bool wasReviewed = question.IsReviewed;
            var oldOptionImageUrls = question.Options.Select(o => o.ImageUrl).ToList();
            var imageUrlsToDeleteAfterSave = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var uploadedImageUrlsToDeleteOnFailure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 🔹 تحديث الخصائص العامة
            question.Title = model.Title;
            question.Template = (QuestionTemplate)model.Template;
            question.ValueA = model.ValueA;
            question.ValueB = model.ValueB;
            question.Explanation = model.Explanation;
            question.VideoUrl = model.VideoUrl;
            question.CurriculumId = model.CurriculumId;
            question.SectionId = model.SectionId;
            question.Hint = model.Hint; // ✅ حفظ الومضة الجديدة

            question.LessonId = model.LessonId;
            question.VerbalPassageId = model.VerbalPassageId;
            question.Difficulty = (DifficultyLevel)model.Difficulty;
            question.InternalNote = model.InternalNote;

            // 🔹 تحديث حالة الكمي
            question.IsQuantitative = await _context.Curriculums
     .Where(c => c.Id == model.CurriculumId)
     .Select(c => c.IsQuantitative)
     .FirstOrDefaultAsync();

            // 🔹 تحديث الصورة الرئيسية: حذف، استبدال، أو إبقاء الصورة الحالية
            if (model.RemoveImage)
            {
                AddQuestionImageForDeletion(imageUrlsToDeleteAfterSave, question.ImageUrl);
                question.ImageUrl = null;
            }
            else if (model.ImageFile != null)
            {
                var previousImageUrl = question.ImageUrl;
                var savedImageUrl = await SaveImageAsync(model.ImageFile);
                if (!string.IsNullOrWhiteSpace(savedImageUrl))
                {
                    question.ImageUrl = savedImageUrl;
                    uploadedImageUrlsToDeleteOnFailure.Add(savedImageUrl);
                    AddQuestionImageForDeletion(imageUrlsToDeleteAfterSave, previousImageUrl);
                }
            }
            else if (!string.IsNullOrWhiteSpace(model.ExistingImageUrl))
            {
                question.ImageUrl = model.ExistingImageUrl;
            }

            // 🔹 تحديث أنواع الاستخدام
            question.UsageTypes = model.SelectedUsageTypes
                .Aggregate(QuestionUsageType.None, (acc, t) => acc | t);

            // 🔹 حذف الخيارات القديمة قبل إعادة إنشائها
            _context.QuestionOptions.RemoveRange(question.Options);
            question.Options = new List<QuestionOption>();

            // 🟢 بناء الخيارات الجديدة وتحديث الإجابة الصحيحة
            for (int i = 0; i < model.Options.Count; i++)
            {
                var opt = model.Options[i];
                var previousOptionImageUrl = !string.IsNullOrWhiteSpace(opt.ExistingImageUrl)
                    ? opt.ExistingImageUrl
                    : oldOptionImageUrls.ElementAtOrDefault(i);

                string? imageUrl = previousOptionImageUrl;
                if (opt.RemoveImage)
                {
                    AddQuestionImageForDeletion(imageUrlsToDeleteAfterSave, previousOptionImageUrl);
                    imageUrl = null;
                }
                else if (opt.ImageFile != null)
                {
                    var savedOptionImageUrl = await SaveImageAsync(opt.ImageFile);
                    if (!string.IsNullOrWhiteSpace(savedOptionImageUrl))
                    {
                        imageUrl = savedOptionImageUrl;
                        uploadedImageUrlsToDeleteOnFailure.Add(savedOptionImageUrl);
                        AddQuestionImageForDeletion(imageUrlsToDeleteAfterSave, previousOptionImageUrl);
                    }
                }

                // نحتفظ بالنص كما هو — إذا كان فارغاً والاختيار صورة فالنص يبقى فارغاً
                var optionText = string.IsNullOrWhiteSpace(opt.Text) ? string.Empty : opt.Text;

                var newOpt = new QuestionOption
                {
                    Text = optionText,
                    ImageUrl = imageUrl
                };
                question.Options.Add(newOpt);
            }

            // ✅ تحديث الإجابة الصحيحة بشكل مضمون بعد بناء الخيارات
            question.CorrectAnswer = null;

            if (model.SelectedCorrectIndex.HasValue)
            {
                var correctOption = question.Options.ElementAtOrDefault(model.SelectedCorrectIndex.Value);
                if (correctOption != null)
                {
                    question.CorrectAnswer = !string.IsNullOrWhiteSpace(correctOption.Text)
                        ? correctOption.Text
                        : correctOption.ImageUrl;
                }
            }


            // 🔹 ضبط حالات الاعتماد والمراجعة
            question.IsAnswerConfirmed = !string.IsNullOrWhiteSpace(question.CorrectAnswer);
            question.IsComplete = question.Options.Any(o =>
                !string.IsNullOrWhiteSpace(o.Text) || !string.IsNullOrWhiteSpace(o.ImageUrl));

            if (!wasReviewed)
            {
                question.IsReviewed = false;
                question.IsRejected = false;
            }

            // 🟢 تتبع التغييرات وتسجيلها في QuestionAuditLog
            var changeSummary = QuestionChangeTracker.GetChangesSummary(oldQuestion, question);
            var user = await _context.Users.OfType<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            _context.QuestionAuditLogs.Add(new QuestionAuditLog
            {
                QuestionId = question.Id,
                Action = "تعديل",
                PerformedByUserId = user?.Id ?? "SYSTEM",
                PerformedByName = user?.FullName ?? "SYSTEM",
                PerformedByRole = User.IsInRole("Instructor") ? UserRoleType.Instructor : UserRoleType.Unknown,
                ChangedFieldsSummary = changeSummary,
                PerformedAt = DateTime.Now
            });

            var retainedImageUrls = new[] { question.ImageUrl }
                .Concat(question.Options.Select(o => o.ImageUrl))
                .ToList();

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                DeleteQuestionImageFiles(uploadedImageUrlsToDeleteOnFailure, Enumerable.Empty<string?>());
                throw;
            }

            DeleteQuestionImageFiles(imageUrlsToDeleteAfterSave, retainedImageUrls);

            TempData["Message"] = wasReviewed
                ? "✅ تم تحديث السؤال بنجاح دون المساس بحالة الاعتماد."
                : "✅ تم تحديث السؤال، وسيحتاج إلى مراجعة لاعتماده لاحقاً.";

            return RedirectToAction("Index");
        }



        [HttpGet]
        [AdminPermission("Questions", "Read")]

        public async Task<JsonResult> GetUnitsBySection(int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var units = await _context.SectionUnits
                .Where(su => su.SectionId == sectionId)
                .Select(su => new { su.Unit.Id, su.Unit.Title })
                .Distinct()
                .ToListAsync();

            return Json(units);
        }



        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> GetLessonsBySection(int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();
            // ✅ حماية ضد القيم الخاطئة
            if (sectionId <= 0)
                return Json(new List<object>());

            // ✅ جلب المؤشرات (الدروس) التابعة للمحور المحدد
            var lessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title
                })
                .OrderBy(l => l.title)
                .ToListAsync();

            return Json(lessons);
        }

        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> GetActiveLessonsForSimilarity(int? curriculumId, int? sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var query = _context.Lessons
                .AsNoTracking()
                .Where(l => l.IsActive);

            if (sectionId.HasValue && sectionId.Value > 0)
            {
                query = query.Where(l => l.SectionId == sectionId.Value);
            }
            else if (curriculumId.HasValue && curriculumId.Value > 0)
            {
                query = query.Where(l => l.Section.CurriculumId == curriculumId.Value);
            }
            else
            {
                return Json(new List<object>());
            }

            var lessons = await query
                .OrderBy(l => l.Title)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title
                })
                .ToListAsync();

            return Json(lessons);
        }




        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> Details(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            var model = new QuestionDetailsViewModel
            {
                Id = question.Id,
                Title = question.Title,
                Template = (Enums.QuestionTemplate)question.Template,      // ✅ بدون cast
                Difficulty = (Enums.DifficultyLevel)question.Difficulty,
                ValueA = question.ValueA,
                ValueB = question.ValueB,
                CorrectAnswer = question.CorrectAnswer,
                Explanation = question.Explanation,
                VideoUrl = question.VideoUrl,
                CurriculumTitle = await _context.Curriculums
                    .Where(c => c.Id == question.CurriculumId)
                    .Select(c => c.Title)
                    .FirstOrDefaultAsync(),
                SectionTitle = await _context.Sections
                    .Where(s => s.Id == question.SectionId)
                    .Select(s => s.Title)
                    .FirstOrDefaultAsync(),
                LessonTitle = await _context.Lessons
                    .Where(l => l.Id == question.LessonId)
                    .Select(l => l.Title)
                    .FirstOrDefaultAsync(),
                ImageUrl = question.ImageUrl,
                Options = question.Options.Select(o => new QuestionOptionViewModel
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList(),
                IsQuantitative = question.IsQuantitative, // ✅ لضمان عمل معاينة السؤال
                CreatedAt = question.CreatedAt            // ✅ لعرض التاريخ
            };

            return View(model);
        }


        [HttpGet]
        [AdminPermission("Questions", "Delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return NotFound();

            var model = new ConfirmDeleteViewModel
            {
                ItemId = id,
                ModalId = "", // غير مستخدم هنا
                Title = "تأكيد الحذف",
                Message = "هل أنت متأكد من حذف السؤال التالي؟",
                ConfirmButtonText = "نعم، احذف",
                CancelButtonText = "إلغاء"
            };

            return View("ConfirmDelete", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions","Delete")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions.FindAsync(id);
            if (question == null)
            {
                TempData["Error"] = "❌ السؤال غير موجود.";
                return RedirectToAction("Index");
            }

            try
            {
                // 1️⃣ ProfessionalModelQuestions
                var pmLinks = await _context.ProfessionalModelQuestions
                    .Where(x => x.QuestionId == id)
                    .ToListAsync();
                if (pmLinks.Any())
                    _context.ProfessionalModelQuestions.RemoveRange(pmLinks);

                // 2️⃣ ExamQuestions
                var examLinks = await _context.ExamQuestions
                    .Where(x => x.QuestionId == id)
                    .ToListAsync();
                if (examLinks.Any())
                    _context.ExamQuestions.RemoveRange(examLinks);

                // 3️⃣ QuestionAttemptNew
                var attempts = await _context.QuestionAttemptNew
                    .Where(x => x.QuestionId == id)
                    .ToListAsync();
                if (attempts.Any())
                    _context.QuestionAttemptNew.RemoveRange(attempts);

                // 4️⃣ StudentActivityLog
                var activities = await _context.StudentActivityLogs
                    .Where(x => x.QuestionId == id)
                    .ToListAsync();
                if (activities.Any())
                    _context.StudentActivityLogs.RemoveRange(activities);

                // 5️⃣ Homework (السجلات المرتبطة بالسؤال)
                var relatedHomeworks = await _context.Homeworks
                    .Where(x => x.QuestionId == id)
                    .ToListAsync();
                if (relatedHomeworks.Any())
                    _context.Homeworks.RemoveRange(relatedHomeworks);

                // 💾 احفظ بعد حذف الروابط
                await _context.SaveChangesAsync();

                // 🔵 احذف السؤال نفسه
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();

                TempData["Message"] = "✅ تم حذف السؤال وجميع الواجبات والروابط التابعة له بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"حدث خطأ أثناء الحذف: {ex.InnerException?.Message ?? ex.Message}";
            }

            return RedirectToAction("Index");
        }


        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> Preview(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions
          .Include(q => q.Options)
          .Include(q => q.VerbalPassage)
          .Include(q => q.Curriculum) // ✅ مهم جدًا
          .FirstOrDefaultAsync(q => q.Id == id);


            if (question == null)
                return NotFound();

            // 🔹 تحويل إلى ViewModel للعرض
            var model = question.ToDisplayModel();
            model.VerbalPassageContent = question.VerbalPassage?.Content;
            // 🔥 الجديد
            model.VerbalPassageMediaUrl = question.VerbalPassage?.MediaUrl;
            model.VerbalPassageType = question.VerbalPassage?.Type;
            model.VerbalPassageDuration = question.VerbalPassage?.DurationSeconds;

            // ✅ تمرير الاتجاه
            model.IsRTL = question.Curriculum != null ? question.Curriculum.IsRTL : true;

            Response.Headers["X-Is-RTL"] = model.IsRTL ? "true" : "false";

            // 🔹 تأمين القائمة قبل تمريرها للبارشال (حتى لو ToDisplayModel أرجع null)
            if (model.Options == null)
                model.Options = new List<QuestionOptionDisplayViewModel>();

            // 🔹 ضمان وجود 4 عناصر لتفادي IndexOutOfRangeException
            while (model.Options.Count < 4)
            {
                model.Options.Add(new QuestionOptionDisplayViewModel
                {
                    Text = string.Empty,
                    ImageUrl = string.Empty
                });
            }

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }



        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> PreviewByInt(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 👇 جلب السؤال من جدول الأسئلة عبر الـ int Id الحقيقي
            var question = await _context.Questions
        .Include(q => q.Options)
        .Include(q => q.VerbalPassage)
        .Include(q => q.Curriculum) // ✅ مهم
        .FirstOrDefaultAsync(q => q.Id == id);

            var displayModel = question.ToDisplayModel();

            if (question == null)
                return NotFound("❌ لم يتم العثور على السؤال.");

            // 👇 تحويله إلى ViewModel العرض نفسه الذي تستخدمه في Preview الأصلي

            // ✅ تمرير الاتجاه
            displayModel.IsRTL = question.Curriculum != null ? question.Curriculum.IsRTL : true;
            // 👇 إعادة نفس البارشال المعتمد لديك
            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", displayModel);
        }



        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> Labels(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions.FindAsync(id);
            if (question == null)
                return NotFound();

            return PartialView("_QuestionLabelsSummaryModal", question);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions", "Reject")]
        public async Task<IActionResult> RejectQuestion(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions.FindAsync(id);
            if (question == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على السؤال.";
                return RedirectToAction("PendingReview");
            }

            question.IsRejected = true;
            await _context.SaveChangesAsync();

            TempData["Message"] = "⚠️ تم رفض السؤال بنجاح.";
            return RedirectToAction("PendingReview");
        }


        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> PendingApproval()
        {
            using var _context = _contextFactory.CreateDbContext();

            var questions = await _context.Questions
                .Include(q => q.Curriculum)
                .Include(q => q.Section)
                .Include(q => q.Lesson)
                .Include(q => q.Options)
                .Where(q => !q.IsComplete || q.IsRejected)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            var audits = await _context.QuestionAuditLogs
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.PerformedAt).FirstOrDefault())
                .ToListAsync();

            var model = new QuestionIndexViewModel
            {
                Questions = questions.Select(q => new QuestionListViewModel
                {
                    Id = q.Id,
                    ReferenceNumber = q.ReferenceNumber ?? "—",
                    Title = q.Title,
                    TitlePreview = q.Title.Length > 15 ? q.Title.Substring(0, 15) + "..." : q.Title,
                    IsQuantitative = q.IsQuantitative,
                    CurriculumTitle = q.Curriculum?.Title ?? "—",
                    SectionTitle = q.Section?.Title ?? "—",
                    LessonTitle = q.Lesson?.Title ?? "—",
                    CreatedAt = q.CreatedAt,
                    IsReviewed = q.IsReviewed,
                    Explanation = q.Explanation,
                    LatestAudit = audits.FirstOrDefault(a => a.QuestionId == q.Id),
                    SelectedLabels = Enum.GetValues(typeof(QuestionUsageType))
                        .Cast<QuestionUsageType>()
                        .Where(t => t != QuestionUsageType.None && q.UsageTypes.HasFlag(t))
                        .Select(t => GetArabicLabel(t))
                        .ToList()
                }).ToList(),

                UnreviewedCount = questions.Count,
                UnansweredCount = await _context.Questions.CountAsync(q => string.IsNullOrWhiteSpace(q.CorrectAnswer))
            };

            return View("Index", model);
        }



        // Controllers/Admin/QuestionsController.cs

        [HttpGet]
        [AdminPermission("Questions", "Approve")]
        public async Task<IActionResult> ApproveReadyQuestions(int? curriculumId, int? sectionId, int? lessonId, int page = 1, int pageSize = 50)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 تحميل القواميس من الكاش أو إنشاؤها مؤقتًا لمدة ساعة
            var curriculums = _cache.GetOrCreate("CurriculumsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Curriculums.AsNoTracking().ToDictionary(c => c.Id, c => c.Title);
            });

            var sections = _cache.GetOrCreate("SectionsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Sections.AsNoTracking().ToDictionary(s => s.Id, s => s.Title);
            });

            var lessons = _cache.GetOrCreate("LessonsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Lessons.AsNoTracking().ToDictionary(l => l.Id, l => l.Title);
            });

            // 🟢 استعلام الأسئلة الجاهزة للاعتماد
            var query = _context.Questions.AsNoTracking()
                .Where(q =>
                    q.IsComplete &&
                    !q.IsReviewed &&
                    !q.IsRejected &&
                    !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);

            if (sectionId.HasValue)
                query = query.Where(q => q.SectionId == sectionId.Value);

            if (lessonId.HasValue)
                query = query.Where(q => q.LessonId == lessonId.Value);

            int totalItems = await query.CountAsync();

            // 🟢 جلب البيانات مع الصفحات
            var questionsList = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ReferenceNumber,
                    q.CreatedAt,
                    q.IsQuantitative,
                    q.IsReviewed,
                    q.CurriculumId,
                    q.SectionId,
                    q.LessonId,
                    q.UsageTypes,
                    q.CorrectAnswer
                })
                .ToListAsync();

            // 🟢 جلب آخر عملية تعديل أو مراجعة لكل سؤال
            var questionIds = questionsList.Select(q => q.Id).ToList();
            var auditLogs = await (
                from a in _context.QuestionAuditLogs
                where questionIds.Contains(a.QuestionId)
                group a by a.QuestionId into g
                select g.OrderByDescending(x => x.PerformedAt).FirstOrDefault()
            ).ToListAsync();

            // 🟢 بناء ViewModel للنتائج
            var result = new ApproveReadyQuestionsViewModel
            {
                CurriculumId = curriculumId,
                SectionId = sectionId,
                LessonId = lessonId,
                Curriculums = curriculums.Select(c => new SelectListItem { Value = c.Key.ToString(), Text = c.Value }).ToList(),
                Sections = sections.Select(s => new SelectListItem { Value = s.Key.ToString(), Text = s.Value }).ToList(),
                Lessons = lessons.Select(l => new SelectListItem { Value = l.Key.ToString(), Text = l.Value }).ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                Questions = questionsList.Select(q => new QuestionListViewModel
                {
                    Id = q.Id,
                    Title = q.Title,
                    TitlePreview = q.Title?.Length > 100 ? q.Title.Substring(0, 100) + "..." : q.Title,
                    ReferenceNumber = q.ReferenceNumber ?? "—",
                    CurriculumTitle = curriculums.ContainsKey(q.CurriculumId) ? curriculums[q.CurriculumId] : "—",
                    SectionTitle = q.SectionId.HasValue && sections.ContainsKey(q.SectionId.Value) ? sections[q.SectionId.Value] : "—",
                    LessonTitle = lessons.ContainsKey(q.LessonId) ? lessons[q.LessonId] : "—",
                    CreatedAt = q.CreatedAt,
                    IsQuantitative = q.IsQuantitative,
                    IsReviewed = q.IsReviewed,
                    CorrectAnswer = q.CorrectAnswer,
                    SelectedLabels = Enum.GetValues(typeof(QuestionUsageType))
                        .Cast<QuestionUsageType>()
                        .Where(t => t != QuestionUsageType.None && q.UsageTypes.HasFlag(t))
                        .Select(t => GetArabicLabel(t))
                        .ToList(),
                    LatestAudit = auditLogs.FirstOrDefault(a => a.QuestionId == q.Id)
                }).ToList()
            };

            return View("ApproveReadyQuestions", result);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions", "Approve")]
        public async Task<IActionResult> ApproveReadyQuestionsConfirm(List<Guid> selectedIds)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم تحديد أي سؤال.";
                return RedirectToAction("ApproveReadyQuestions");
            }

            // 🟢 جلب الأسئلة المحددة فقط
            var allPendingQuestions = await _context.Questions
         .Include(q => q.Options)
         .Where(q => !q.IsReviewed && !q.IsRejected)
         .ToListAsync();

            var selectedIdSet = selectedIds
                .Where(id => id != Guid.Empty)
                .ToList();

            var questions = allPendingQuestions
                .Where(q => selectedIdSet.Any(id => id == q.Id))
                .ToList();

            // 🟢 تحديد الأسئلة الصالحة للاعتماد
            var validQuestions = questions
                .Where(q =>
                    q.IsComplete &&
                    !q.IsReviewed &&
                    !q.IsRejected &&
                    !string.IsNullOrWhiteSpace(q.CorrectAnswer) &&
                    q.Options.Any(o =>
                        (!string.IsNullOrWhiteSpace(o.Text) && o.Text == q.CorrectAnswer) ||
                        (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl == q.CorrectAnswer)
                    )
                )
                .ToList();

            if (!validQuestions.Any())
            {
                TempData["Error"] = "⚠️ لا توجد أسئلة صالحة للاعتماد.";
                return RedirectToAction("ApproveReadyQuestions");
            }

            // 🟢 تحميل المستخدم الحالي لمرة واحدة فقط
            var user = await _context.Users
                .OfType<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            string userId = user?.Id ?? "SYSTEM";
            string userName = user?.FullName ?? user?.UserName ?? "SYSTEM";
            UserRoleType userRole = User.IsInRole("Admin") ? UserRoleType.Admin :
                                    User.IsInRole("SuperAdmin") ? UserRoleType.SuperAdmin :
                                    User.IsInRole("Owner") ? UserRoleType.Owner :
                                    UserRoleType.Unknown;

            DateTime now = DateTime.Now;

            // 🟢 تنفيذ الاعتماد بشكل مجمع
            foreach (var q in validQuestions)
            {
                q.IsReviewed = true;
                q.ReviewedByUserId = userId;
                q.ReviewedAt = now;

                _context.QuestionAuditLogs.Add(new QuestionAuditLog
                {
                    QuestionId = q.Id,
                    Action = "اعتماد",
                    PerformedByUserId = userId,
                    PerformedByName = userName,
                    PerformedByRole = userRole,
                    PerformedAt = now,
                    ChangedFieldsSummary = "تم اعتماد السؤال من شاشة اعتماد الأسئلة الجاهزة (ApproveReadyQuestions)."
                });
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = $"✅ تم اعتماد {validQuestions.Count} سؤال وتسجيلها في السجل.";
            return RedirectToAction("ApproveReadyQuestions");
        }




        [HttpGet]
        [AdminPermission("Questions", "Read")]

        public async Task<IActionResult> MissingOptions(int page = 1, int pageSize = 50)
        {
            using var _context = _contextFactory.CreateDbContext();

            var query = _context.Questions
                .Where(q =>
                    q.IsComplete &&
                    !q.IsRejected &&
                    !_context.QuestionOptions
                        .Where(o => o.QuestionId == q.Id)
                        .Any(o => !string.IsNullOrWhiteSpace(o.Text) || !string.IsNullOrWhiteSpace(o.ImageUrl))
                );

            int totalItems = await query.CountAsync();

            var questionsList = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ReferenceNumber,
                    q.CreatedAt,
                    q.IsReviewed,
                    q.IsAnswerConfirmed,
                    q.CurriculumId,
                    q.SectionId,
                    q.LessonId
                })
                .ToListAsync();

            var curriculumTitles = await _context.Curriculums.ToDictionaryAsync(c => c.Id, c => c.Title);
            var sectionTitles = await _context.Sections.ToDictionaryAsync(s => s.Id, s => s.Title);
            var lessonTitles = await _context.Lessons.ToDictionaryAsync(l => l.Id, l => l.Title);

            var model = new QuestionIndexViewModel
            {
                Questions = questionsList.Select(q => new QuestionListViewModel
                {
                    Id = q.Id,
                    ReferenceNumber = q.ReferenceNumber ?? "—",
                    Title = q.Title,
                    CurriculumTitle = curriculumTitles.ContainsKey(q.CurriculumId) ? curriculumTitles[q.CurriculumId] : "—",
                    SectionTitle = q.SectionId.HasValue && sectionTitles.ContainsKey(q.SectionId.Value) ? sectionTitles[q.SectionId.Value] : "—",
                    LessonTitle = lessonTitles.ContainsKey(q.LessonId)
                                ? lessonTitles[q.LessonId]
                                : "—",
                    CreatedAt = q.CreatedAt,
                    IsReviewed = q.IsReviewed,
                    IsAnswerConfirmed = q.IsAnswerConfirmed
                }).ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
            };

            return View("MissingAnswers", model);
        }




        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> MissingAnswers(int page = 1, int pageSize = 50)
        {
            using var _context = _contextFactory.CreateDbContext();

            var query = _context.Questions
                .Where(q =>
                    (string.IsNullOrWhiteSpace(q.CorrectAnswer) || q.CorrectAnswer.Trim() == "")
                    && q.IsComplete && !q.IsRejected);

            int totalItems = await query.CountAsync();

            var questionsList = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ReferenceNumber,
                    q.CreatedAt,
                    q.IsReviewed,
                    q.IsAnswerConfirmed,
                    q.CurriculumId,
                    q.SectionId,
                    q.LessonId
                })
                .ToListAsync();

            var curriculumTitles = await _context.Curriculums.ToDictionaryAsync(c => c.Id, c => c.Title);
            var sectionTitles = await _context.Sections.ToDictionaryAsync(s => s.Id, s => s.Title);
            var lessonTitles = await _context.Lessons.ToDictionaryAsync(l => l.Id, l => l.Title);

            var model = new QuestionIndexViewModel
            {
                Questions = questionsList.Select(q => new QuestionListViewModel
                {
                    Id = q.Id,
                    ReferenceNumber = q.ReferenceNumber ?? "—",
                    Title = q.Title,
                    CurriculumTitle = curriculumTitles.ContainsKey(q.CurriculumId) ? curriculumTitles[q.CurriculumId] : "—",
                    SectionTitle = q.SectionId.HasValue && sectionTitles.ContainsKey(q.SectionId.Value) ? sectionTitles[q.SectionId.Value] : "—",
                    LessonTitle = lessonTitles.ContainsKey(q.LessonId)
                                ? lessonTitles[q.LessonId]
                                : "—",
                    CreatedAt = q.CreatedAt,
                    IsReviewed = q.IsReviewed,
                    IsAnswerConfirmed = q.IsAnswerConfirmed
                }).ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
            };

            return View("MissingAnswers", model);
        }


        [HttpGet]
        [AdminPermission("Questions", "Read")]

        public async Task<IActionResult> PendingReview(int? curriculumId, int? sectionId, int? lessonId, string? searchTitle, int page = 1, int pageSize = 50)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 تحميل بيانات المناهج والمحاور والدروس من الكاش
            var curriculums = _cache.GetOrCreate("CurriculumsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Curriculums.AsNoTracking().ToDictionary(c => c.Id, c => c.Title);
            }) ?? new Dictionary<int, string>();

            var sections = _cache.GetOrCreate("SectionsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Sections.AsNoTracking().ToDictionary(s => s.Id, s => s.Title);
            }) ?? new Dictionary<int, string>();

            var lessons = _cache.GetOrCreate("LessonsCache", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                using var ctx = _contextFactory.CreateDbContext();
                return ctx.Lessons.AsNoTracking().ToDictionary(l => l.Id, l => l.Title);
            }) ?? new Dictionary<int, string>();

            // 🟢 بناء استعلام الأسئلة
            var baseQuery = _context.Questions
                .AsNoTracking()
                .Where(q => q.IsComplete && !q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            var curriculumStats = await baseQuery
                .GroupBy(q => new { q.CurriculumId, q.Curriculum.Title })
                .Select(g => new PendingReviewCurriculumStatViewModel
                {
                    CurriculumId = g.Key.CurriculumId,
                    CurriculumTitle = g.Key.Title,
                    PendingCount = g.Count(),
                    ConfirmedAnswersCount = g.Count(q => q.IsAnswerConfirmed),
                    UnconfirmedAnswersCount = g.Count(q => !q.IsAnswerConfirmed),
                    QuantitativeCount = g.Count(q => q.IsQuantitative),
                    VerbalCount = g.Count(q => !q.IsQuantitative),
                    SectionsCount = g.Select(q => q.SectionId).Distinct().Count(),
                    OldestCreatedAt = g.Min(q => (DateTime?)q.CreatedAt)
                })
                .OrderByDescending(x => x.PendingCount)
                .ToListAsync();

            if (curriculumId.HasValue)
                baseQuery = baseQuery.Where(q => q.CurriculumId == curriculumId.Value);
            if (sectionId.HasValue)
                baseQuery = baseQuery.Where(q => q.SectionId == sectionId.Value);
            if (lessonId.HasValue)
                baseQuery = baseQuery.Where(q => q.LessonId == lessonId.Value);
            if (!string.IsNullOrWhiteSpace(searchTitle))
                baseQuery = baseQuery.Where(q => q.Title != null && q.Title.Contains(searchTitle.Trim()));

            int totalItems = await baseQuery.CountAsync();
            var filteredSummary = await baseQuery
                .GroupBy(q => 1)
                .Select(g => new
                {
                    ConfirmedAnswers = g.Count(q => q.IsAnswerConfirmed),
                    UnconfirmedAnswers = g.Count(q => !q.IsAnswerConfirmed),
                    Quantitative = g.Count(q => q.IsQuantitative),
                    Verbal = g.Count(q => !q.IsQuantitative)
                })
                .FirstOrDefaultAsync();

            // 🟢 جلب الصفحة المطلوبة فقط
            var questionsList = await baseQuery
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ReferenceNumber,
                    q.CurriculumId,
                    q.SectionId,
                    q.LessonId,
                    q.CreatedAt,
                    q.IsReviewed,
                    q.IsQuantitative,
                    q.UsageTypes,
                    LatestAudit = _context.QuestionAuditLogs
                        .Where(a => a.QuestionId == q.Id)
                        .OrderByDescending(a => a.PerformedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // 🟢 دمج البيانات داخل ViewModel العرض
            var questions = questionsList.Select(q => new QuestionListViewModel
            {
                Id = q.Id,
                Title = q.Title,
                TitlePreview = q.Title?.Length > 80 ? q.Title.Substring(0, 80) + "..." : q.Title,
                ReferenceNumber = q.ReferenceNumber ?? "—",
                CurriculumTitle = curriculums.TryGetValue(q.CurriculumId, out var c) ? c : "—",
                SectionTitle = q.SectionId.HasValue && sections.TryGetValue(q.SectionId.Value, out var s) ? s : "—",
                LessonTitle = lessons.TryGetValue(q.LessonId, out var l) ? l : "—",
                IsReviewed = q.IsReviewed,
                IsQuantitative = q.IsQuantitative,
                CreatedAt = q.CreatedAt,
                SelectedLabels = Enum.GetValues(typeof(QuestionUsageType))
                    .Cast<QuestionUsageType>()
                    .Where(t => t != QuestionUsageType.None && q.UsageTypes.HasFlag(t))
                    .Select(t => GetArabicLabel(t))
                    .ToList(),
                LatestAudit = q.LatestAudit
            }).ToList();

            // 🟢 بناء ViewModel النهائي للعرض
            var viewModel = new QuestionIndexViewModel
            {
                Questions = questions,
                Curriculums = curriculums
                    .Select(c => new SelectListItem { Value = c.Key.ToString(), Text = c.Value })
                    .ToList(),
                Sections = sections
                    .Select(s => new SelectListItem { Value = s.Key.ToString(), Text = s.Value })
                    .ToList(),
                Lessons = lessons
                    .Select(l => new SelectListItem { Value = l.Key.ToString(), Text = l.Value })
                    .ToList(),
                CurriculumId = curriculumId,
                SectionId = sectionId,
                LessonId = lessonId,
                SearchTitle = searchTitle,
                PendingReviewCurriculumStats = curriculumStats,
                PendingReviewFilteredCount = totalItems,
                PendingReviewConfirmedAnswersCount = filteredSummary?.ConfirmedAnswers ?? 0,
                PendingReviewUnconfirmedAnswersCount = filteredSummary?.UnconfirmedAnswers ?? 0,
                PendingReviewQuantitativeCount = filteredSummary?.Quantitative ?? 0,
                PendingReviewVerbalCount = filteredSummary?.Verbal ?? 0,
                PendingReviewCount = curriculumStats.Sum(x => x.PendingCount),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
            };

            return View(viewModel);
        }



        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> IncompleteQuestions(int page = 1, int pageSize = 50)
        {
            using var _context = _contextFactory.CreateDbContext();

            var query = _context.Questions
                .Where(q => !q.IsComplete && !q.IsRejected);

            int totalItems = await query.CountAsync();

            var questionsList = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ReferenceNumber,
                    q.CreatedAt,
                    q.IsReviewed,
                    q.IsAnswerConfirmed,
                    q.CurriculumId,
                    q.SectionId,
                    q.LessonId
                })
                .ToListAsync();

            var curriculumTitles = await _context.Curriculums.ToDictionaryAsync(c => c.Id, c => c.Title);
            var sectionTitles = await _context.Sections.ToDictionaryAsync(s => s.Id, s => s.Title);
            var lessonTitles = await _context.Lessons.ToDictionaryAsync(l => l.Id, l => l.Title);

            var model = new QuestionIndexViewModel
            {
                Questions = questionsList.Select(q => new QuestionListViewModel
                {
                    Id = q.Id,
                    ReferenceNumber = q.ReferenceNumber ?? "—",
                    Title = q.Title,
                    CurriculumTitle = curriculumTitles.ContainsKey(q.CurriculumId) ? curriculumTitles[q.CurriculumId] : "—",
                    SectionTitle = q.SectionId.HasValue && sectionTitles.ContainsKey(q.SectionId.Value) ? sectionTitles[q.SectionId.Value] : "—",
                    LessonTitle = lessonTitles.ContainsKey(q.LessonId)
                                ? lessonTitles[q.LessonId]
                                : "—",
                    CreatedAt = q.CreatedAt,
                    IsReviewed = q.IsReviewed,
                    IsAnswerConfirmed = q.IsAnswerConfirmed
                }).ToList(),
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
            };

            return View("MissingAnswers", model); // أو View خاص بـ Incomplete
        }






        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions", "Approve")]
        public async Task<IActionResult> ApproveQuestions(List<Guid> selectedIds)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم تحديد أي سؤال.";
                return RedirectToAction("PendingReview");
            }

            var allPendingQuestions = await _context.Questions
       .Include(q => q.Options)
       .Where(q => !q.IsReviewed && !q.IsRejected)
       .ToListAsync();

            var selectedIdSet = selectedIds
                .Where(id => id != Guid.Empty)
                .ToList();

            var questions = allPendingQuestions
                .Where(q => selectedIdSet.Any(id => id == q.Id))
                .ToList();
            var validQuestions = questions
                .Where(q =>
                    q.IsComplete &&
                    !q.IsReviewed &&
                    !q.IsRejected &&
                    !string.IsNullOrWhiteSpace(q.CorrectAnswer) &&
                    q.Options.Any(o =>
                        (!string.IsNullOrWhiteSpace(o.Text) && o.Text == q.CorrectAnswer) ||
                        (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl == q.CorrectAnswer)
                    )
                )
                .ToList();

            if (!validQuestions.Any())
            {
                TempData["Error"] = "⚠️ لا توجد أسئلة صالحة للاعتماد.";
                return RedirectToAction("PendingReview");
            }

            var user = await _context.Users.OfType<ApplicationUser>()
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            foreach (var q in validQuestions)
            {
                q.IsReviewed = true;
                q.ReviewedByUserId = user?.Id ?? "SYSTEM";
                q.ReviewedAt = DateTime.Now;

                // ✅ سجل الاعتماد في QuestionAuditLog
                _context.QuestionAuditLogs.Add(new QuestionAuditLog
                {
                    QuestionId = q.Id,
                    Action = "اعتماد",
                    PerformedByUserId = user?.Id ?? "SYSTEM",
                    PerformedByName = user?.FullName ?? user?.UserName ?? "SYSTEM",
                    PerformedByRole = User.IsInRole("Admin") ? UserRoleType.Admin : UserRoleType.Unknown,
                    PerformedAt = DateTime.Now,
                    ChangedFieldsSummary = "تم اعتماد السؤال ضمن عملية اعتماد متعددة من PendingReview."
                });
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = $"✅ تم اعتماد {validQuestions.Count} سؤال وتسجيلها في السجل.";
            return RedirectToAction("PendingReview");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions", "Approve")]
        public async Task<IActionResult> ApprovePendingReviewFiltered(int? curriculumId, int? sectionId, int? lessonId, string? searchTitle)
        {
            using var _context = _contextFactory.CreateDbContext();

            var query = _context.Questions
                .Include(q => q.Options)
                .Where(q => q.IsComplete && !q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);
            if (sectionId.HasValue)
                query = query.Where(q => q.SectionId == sectionId.Value);
            if (lessonId.HasValue)
                query = query.Where(q => q.LessonId == lessonId.Value);
            if (!string.IsNullOrWhiteSpace(searchTitle))
                query = query.Where(q => q.Title != null && q.Title.Contains(searchTitle.Trim()));

            var questions = await query.ToListAsync();

            var validQuestions = questions
                .Where(q =>
                    q.Options.Any(o =>
                        (!string.IsNullOrWhiteSpace(o.Text) && o.Text == q.CorrectAnswer) ||
                        (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl == q.CorrectAnswer)
                    )
                )
                .ToList();

            if (!validQuestions.Any())
            {
                TempData["Error"] = "⚠️ لا توجد أسئلة صالحة للاعتماد ضمن الفلاتر الحالية.";
                return RedirectToAction("PendingReview", new { curriculumId, sectionId, lessonId });
            }

            var user = await _context.Users
                .OfType<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            string userId = user?.Id ?? "SYSTEM";
            string userName = user?.FullName ?? user?.UserName ?? "SYSTEM";
            UserRoleType userRole =
                User.IsInRole("Admin") ? UserRoleType.Admin :
                User.IsInRole("SuperAdmin") ? UserRoleType.SuperAdmin :
                User.IsInRole("Owner") ? UserRoleType.Owner :
                UserRoleType.Unknown;

            DateTime now = DateTime.Now;

            foreach (var q in validQuestions)
            {
                q.IsReviewed = true;
                q.ReviewedByUserId = userId;
                q.ReviewedAt = now;

                _context.QuestionAuditLogs.Add(new QuestionAuditLog
                {
                    QuestionId = q.Id,
                    Action = "اعتماد",
                    PerformedByUserId = userId,
                    PerformedByName = userName,
                    PerformedByRole = userRole,
                    PerformedAt = now,
                    ChangedFieldsSummary = "تم اعتماد السؤال ضمن اعتماد كل النتائج المطابقة للفلاتر من صفحة PendingReview."
                });
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = $"✅ تم اعتماد {validQuestions.Count} سؤال من النتائج المطابقة للفلاتر.";
            return RedirectToAction("PendingReview", new { curriculumId, sectionId, lessonId });
        }




        [HttpGet]
        [AdminPermission("Questions", "ViewAudit")]

        public async Task<IActionResult> Audit(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var latestAudit = await _context.QuestionAuditLogs
                .Where(a => a.QuestionId == id)
                .OrderByDescending(a => a.PerformedAt)
                .FirstOrDefaultAsync();

            if (latestAudit == null)
                return Content("<p class='text-danger'>لا يوجد سجل تعديلات لهذا السؤال</p>");

            var vm = new QuestionAuditSummaryViewModel
            {
                QuestionId = latestAudit.QuestionId,
                LatestAudit = latestAudit
            };

            return PartialView("_QuestionAuditSummaryModal", vm);
        }

        [HttpGet]
        [AdminPermission("Questions", "ViewAudit")]

        public async Task<IActionResult> FullAudit(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 هات كل التعديلات المرتبطة بالسؤال
            var logs = await _context.QuestionAuditLogs
                .Where(a => a.QuestionId == id)
                .OrderByDescending(a => a.PerformedAt)
                .ToListAsync();

            // 🟢 تمرير الـ QuestionId عشان زر الرجوع للتفاصيل
            ViewBag.QuestionId = id;

            return View("FullAudit", logs);
        }



        [HttpGet]
        [AdminPermission("Questions", "ViewAudit")]

        public async Task<IActionResult> AuditLog(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var logs = await _context.QuestionAuditLogs
                .Where(a => a.QuestionId == id)
                .OrderByDescending(a => a.PerformedAt)
                .ToListAsync();

            ViewBag.QuestionId = id;

            return View("AuditLog", logs); // صفحة كاملة
        }




        [HttpGet]
        [IgnoreAntiforgeryToken]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> GetAllQuestions()
        {
            using var _context = _contextFactory.CreateDbContext();

            var questions = await _context.Questions
                .Where(q => q.IsComplete && q.IsReviewed && !q.IsRejected && !string.IsNullOrEmpty(q.CorrectAnswer))
                .Include(q => q.Curriculum)
                .Include(q => q.Section)
                .Include(q => q.Lesson)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new {
                    id = q.Id,
                    referenceNumber = q.ReferenceNumber ?? "—",
                    title = q.Title.Length > 25 ? q.Title.Substring(0, 25) + "..." : q.Title,
                    curriculum = q.Curriculum.Title,
                    section = q.Section.Title,
                    lesson = q.Lesson.Title,
                    createdAt = q.CreatedAt.ToString("yyyy/MM/dd"),
                    isAnswerConfirmed = q.IsAnswerConfirmed
                })
                .ToListAsync();

            return Json(new { data = questions });
        }


        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> ListBySection(int curriculumId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var section = await _context.Sections.FirstOrDefaultAsync(s => s.Id == sectionId);
            if (section == null)
                return NotFound();

            var regex = new Regex("<span[^>]*display:none[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);

            var questions = await _context.Questions
                .Where(q => q.SectionId == sectionId &&
                            q.IsComplete &&
                            !q.IsRejected &&
                            !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .Select(q => new QuestionSimpleViewModel
                {
                    Id = q.Id,
                    Title = q.Title,
                    ReferenceNumber = q.ReferenceNumber,
                    IsAnswerConfirmed = q.IsAnswerConfirmed,
                    CreatedAt = q.CreatedAt,
                    LessonTitle = q.Lesson.Title,
                    InternalNote = q.InternalNote
                })
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            // 🟢 تنظيف العنوان من span المخفي
            foreach (var q in questions)
            {
                q.CleanTitle = regex.Replace(q.Title ?? "", "$1").Trim();
            }

            var pendingCount = await _context.Questions.CountAsync(q =>
                q.SectionId == sectionId &&
                q.IsComplete &&
                !q.IsReviewed &&
                !q.IsRejected &&
                !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            var viewModel = new QuestionsListBySectionViewModel
            {
                SectionId = sectionId,
                SectionTitle = section.Title,
                PendingReviewCount = pendingCount,
                Questions = questions
            };

            return View(viewModel);
        }


        [AdminPermission("Questions", "Read")]

        public async Task<IActionResult> ShowBySection(int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var section = await _context.Sections.FindAsync(sectionId);
            if (section == null)
                return NotFound();

            var questions = await _context.Questions
                .Where(q => q.SectionId == sectionId && q.IsComplete && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuestionSimpleViewModel
                {
                    Id = q.Id,
                    Title = q.Title,
                    ReferenceNumber = q.ReferenceNumber,
                    IsAnswerConfirmed = q.IsAnswerConfirmed,
                    CreatedAt = q.CreatedAt,
                    LessonTitle = _context.Lessons.FirstOrDefault(l => l.Id == q.LessonId).Title
                })
                .ToListAsync();

            var pendingCount = await _context.Questions
                .CountAsync(q => q.SectionId == sectionId && q.IsComplete && !q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            var viewModel = new QuestionsBySectionViewModel
            {
                SectionId = sectionId,
                SectionTitle = section.Title,
                PendingReviewCount = pendingCount,
                Questions = questions
            };

            return View(viewModel);
        }

        [HttpGet]
        [AdminPermission("Questions", "Read")]
        public async Task<IActionResult> GetCurriculumType(int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var data = await _context.Curriculums
                .Where(c => c.Id == curriculumId)
                .Select(c => new
                {
                    isRTL = c.IsRTL,
                    isQuantitative = c.IsQuantitative
                })
                .FirstOrDefaultAsync();

            if (data == null)
                return Json(new { isRTL = true, isQuantitative = false });

            return Json(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Questions", "Approve")]
        public async Task<IActionResult> ApproveSingleQuestion(Guid selectedId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 جلب السؤال مع خياراته مرة واحدة فقط
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == selectedId);

            if (question == null)
                return Json(new { success = false, message = "❌ السؤال غير موجود." });

            // 🟢 تحقق من صلاحية السؤال للاعتماد
            if (!question.IsComplete || question.IsRejected || string.IsNullOrWhiteSpace(question.CorrectAnswer))
                return Json(new { success = false, message = "⚠️ لا يمكن اعتماد هذا السؤال لأنه غير مكتمل أو مرفوض أو بدون إجابة صحيحة." });

            bool hasValidOption = question.Options.Any(o =>
                (!string.IsNullOrWhiteSpace(o.Text) && o.Text == question.CorrectAnswer) ||
                (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl == question.CorrectAnswer)
            );

            if (!hasValidOption)
                return Json(new { success = false, message = "⚠️ الإجابة المحددة غير مرتبطة بأي خيار فعلي." });

            // 🟢 تحميل بيانات المستخدم مرة واحدة فقط (NoTracking لتحسين الأداء)
            var user = await _context.Users
                .OfType<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            string userId = user?.Id ?? "SYSTEM";
            string userName = user?.FullName ?? user?.UserName ?? "SYSTEM";
            UserRoleType userRole =
                User.IsInRole("Admin") ? UserRoleType.Admin :
                User.IsInRole("SuperAdmin") ? UserRoleType.SuperAdmin :
                User.IsInRole("Owner") ? UserRoleType.Owner :
                User.IsInRole("Instructor") ? UserRoleType.Instructor :
                User.IsInRole("Developer") ? UserRoleType.Developer :
                UserRoleType.Unknown;

            DateTime now = DateTime.Now;

            // 🟢 تنفيذ الاعتماد
            question.IsReviewed = true;
            question.ReviewedByUserId = userId;
            question.ReviewedAt = now;

            // 🟢 تسجيل العملية في سجل المراجعة (Audit)
            _context.QuestionAuditLogs.Add(new QuestionAuditLog
            {
                QuestionId = question.Id,
                Action = "اعتماد",
                PerformedByUserId = userId,
                PerformedByName = userName,
                PerformedByRole = userRole,
                PerformedAt = now,
                ChangedFieldsSummary = "تم اعتماد السؤال يدويًا من صفحة PendingReview."
            });

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "✅ تم اعتماد السؤال بنجاح وتسجيل العملية في السجل." });
        }



    }
}
