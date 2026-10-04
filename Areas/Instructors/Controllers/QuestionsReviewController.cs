using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Services;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.QuestionReviewTasks;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Instructors.Controllers
{
    /// <summary>
    /// مراجعة المدرب لأسئلة البنك: اعتماد مباشر أو تعديل ثم اعتماد،
    /// وكل عملية تُسجَّل في QuestionAuditLogs (سجل البنك).
    /// </summary>
    public class QuestionsReviewController : BaseInstructorController
    {
        private const string ActionApproveDirect = "اعتماد مباشر";
        private const string ActionEdit = "تعديل";
        private const string ActionEditAndApprove = "تعديل واعتماد";
        private const string QuestionUploadVirtualPath = "/uploads/questions/";

        private readonly ApplicationDbContext _context;
        private readonly IQuestionReviewLockService _locks;
        private readonly IQuestionReviewTaskService _reviewTasks;
        private readonly IQuestionReviewTaskQueryService _taskQuery;

        public QuestionsReviewController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService,
            IQuestionReviewLockService locks,
            IQuestionReviewTaskService reviewTasks,
            IQuestionReviewTaskQueryService taskQuery)
            : base(userManager, scopeService)
        {
            _context = context;
            _locks = locks;
            _reviewTasks = reviewTasks;
            _taskQuery = taskQuery;
        }

        // ─── QRT-S4.2: حماية المسار العام من الأسئلة المحتجزة بمهام المراجعة ───
        // سؤال محجوز (Pending) أو مُرجَع للإدارة (Returned) لا يُعتمد ولا يُعدَّل إلا من داخل مهمته.
        private async Task<string?> HoldMessageAsync(Guid questionId)
        {
            var hold = await _locks.GetHoldAsync(questionId);
            if (hold is null) return null;

            return hold.ItemStatus == QuestionReviewTaskItemStatus.Returned
                ? $"🔒 هذا السؤال أُرجع للإدارة ضمن المهمة {hold.TaskCode} وبانتظار قرارها، ولا يمكن اعتماده أو تعديله من هنا."
                : $"🔒 هذا السؤال محجوز ضمن مهمة المراجعة {hold.TaskCode}؛ افتحه من «مهام المراجعة» لاعتماده أو تعديله.";
        }

        // QRT-S4.1: التحقق من أن العنصر معلّق ويخص مهمة هذا المدرب ويطابق السؤال (لا IDOR)
        private async Task<(EditableTaskItem? Item, IActionResult? Reject)> ResolveTaskEditAsync(Guid questionId, long taskItemId)
        {
            var instructorId = await RequireInstructorAsync();
            var item = instructorId == 0 ? null : await _taskQuery.GetEditableItemAsync(instructorId, taskItemId);
            if (item is null || item.QuestionId != questionId)
            {
                TempData["Message"] = "⚠️ هذا السؤال لم يعد متاحًا للتعديل ضمن مهمة المراجعة.";
                return (null, RedirectToAction("Index", "QuestionReviewTasks", new { area = "Instructors" }));
            }

            return (item, null);
        }

        private bool IsPrivileged =>
            User.IsInRole("SuperAdmin") || User.IsInRole("Owner") || User.IsInRole("Developer");

        private async Task<bool> CanAccessCurriculumAsync(int curriculumId)
        {
            var ids = await GetAccessibleCurriculumIdsAsync();
            return ids.Contains(curriculumId);
        }

        // المناهج المسندة للمدرب مباشرة فقط (InstructorCurriculumBatches).
        // الأدمن/المالك/المطوّر بدون سجل مدرب يرون كل المناهج.
        private async Task<List<int>> GetAccessibleCurriculumIdsAsync()
        {
            if (_accessibleCurriculumIds != null) return _accessibleCurriculumIds;

            var instructorId = await RequireInstructorAsync();

            if (instructorId != 0)
                _accessibleCurriculumIds = await GetInstructorDirectCurriculumIdsAsync(instructorId);
            else if (IsPrivileged)
                _accessibleCurriculumIds = await _context.Curriculums.AsNoTracking().Select(c => c.Id).ToListAsync();
            else
                _accessibleCurriculumIds = new List<int>();

            return _accessibleCurriculumIds;
        }

        private List<int>? _accessibleCurriculumIds;

        private UserRoleType CurrentRole() =>
            User.IsInRole("Developer") ? UserRoleType.Developer :
            User.IsInRole("SuperAdmin") ? UserRoleType.SuperAdmin :
            User.IsInRole("Owner") ? UserRoleType.Owner :
            User.IsInRole("Instructor") || User.IsInRole("PartnerInstructor") ? UserRoleType.Instructor :
            UserRoleType.Unknown;

        private async Task<(string Id, string Name)> CurrentUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return (user?.Id ?? "SYSTEM", user?.FullName ?? user?.UserName ?? "SYSTEM");
        }

        private QuestionAuditLog NewAudit(Guid questionId, string action, string? summary, (string Id, string Name) user) =>
            new()
            {
                QuestionId = questionId,
                Action = action,
                PerformedByUserId = user.Id,
                PerformedByName = user.Name,
                PerformedByRole = CurrentRole(),
                PerformedAt = DateTime.Now,
                ChangedFieldsSummary = summary
            };

        // ─── صفحة الأسئلة قيد المراجعة ───────────────────────────
        [HttpGet]
        public async Task<IActionResult> PendingReview(int? curriculumId)
        {
            var allowedIds = await GetAccessibleCurriculumIdsAsync();

            var curriculums = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            var allowed = new HashSet<int>(allowedIds);
            var items = curriculums
                .Where(c => allowed.Contains(c.Id))
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToList();

            int? selected = curriculumId.HasValue && allowed.Contains(curriculumId.Value)
                ? curriculumId
                : (items.Count > 0 ? int.Parse(items[0].Value) : (int?)null);

            return View(new InstructorPendingReviewViewModel
            {
                CurriculumId = selected,
                Curriculums = items
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoadPendingReviewQuestionsData()
        {
            int? curriculumId = int.TryParse(Request.Form["curriculumId"], out var c) ? c : null;
            int? sectionId = int.TryParse(Request.Form["sectionId"], out var s) ? s : null;
            int? lessonId = int.TryParse(Request.Form["lessonId"], out var l) ? l : null;
            string searchTitle = Request.Form["searchTitle"];

            if (!curriculumId.HasValue || !await CanAccessCurriculumAsync(curriculumId.Value))
            {
                return Json(new
                {
                    draw = Request.Form["draw"].ToString(),
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = Array.Empty<object>()
                });
            }

            var query = _context.Questions
                .AsNoTracking()
                .Where(q => q.CurriculumId == curriculumId.Value)
                .Where(q => q.IsComplete && !q.IsReviewed && !q.IsRejected)
                .Where(q => !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .Where(q => !_locks.HeldQuestionIds(_context).Contains(q.Id)) // QRT-S4.2: المحجوز/المرتجع خارج المسار العام
                .Where(q => !sectionId.HasValue || q.SectionId == sectionId.Value)
                .Where(q => !lessonId.HasValue || q.LessonId == lessonId.Value)
                .Where(q => string.IsNullOrWhiteSpace(searchTitle) || (q.Title != null && q.Title.Contains(searchTitle)));

            int total = await query.CountAsync();

            int start = int.TryParse(Request.Form["start"], out var st) ? Math.Max(st, 0) : 0;
            int length = int.TryParse(Request.Form["length"], out var ln) && ln > 0 ? Math.Min(ln, 100) : 25;

            var rows = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip(start)
                .Take(length)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ReferenceNumber,
                    q.CreatedAt,
                    Curriculum = q.Curriculum.Title,
                    Section = q.Section.Title
                })
                .ToListAsync();

            var data = rows.Select(q => new
            {
                id = q.Id,
                referenceNumber = q.ReferenceNumber ?? "—",
                title = q.Title ?? string.Empty,
                curriculum = q.Curriculum ?? "—",
                section = q.Section ?? "—",
                createdAt = q.CreatedAt.ToString("yyyy/MM/dd")
            });

            return Json(new
            {
                draw = Request.Form["draw"].ToString(),
                recordsTotal = total,
                recordsFiltered = total,
                data
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetSectionsByCurriculum(int curriculumId)
        {
            if (!await CanAccessCurriculumAsync(curriculumId))
                return Json(new List<object>());

            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new { id = s.Id, title = s.Title })
                .ToListAsync();

            return Json(sections);
        }

        [HttpGet]
        public async Task<IActionResult> GetLessonsBySection(int sectionId)
        {
            if (sectionId <= 0)
                return Json(new List<object>());

            var curriculumId = await _context.Sections
                .AsNoTracking()
                .Where(s => s.Id == sectionId)
                .Select(s => s.CurriculumId)
                .FirstOrDefaultAsync();

            if (!await CanAccessCurriculumAsync(curriculumId))
                return Json(new List<object>());

            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .OrderBy(l => l.Title)
                .Select(l => new { id = l.Id, title = l.Title })
                .ToListAsync();

            return Json(lessons);
        }

        [HttpGet]
        public async Task<IActionResult> GetCurriculumType(int curriculumId)
        {
            if (!await CanAccessCurriculumAsync(curriculumId))
                return Json(new { isRTL = true, isQuantitative = false });

            var data = await _context.Curriculums
                .AsNoTracking()
                .Where(c => c.Id == curriculumId)
                .Select(c => new { isRTL = c.IsRTL, isQuantitative = c.IsQuantitative })
                .FirstOrDefaultAsync();

            return Json(data ?? new { isRTL = true, isQuantitative = false });
        }

        // ─── معاينة سؤال ──────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Preview(Guid id)
        {
            var question = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Curriculum)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null || !await CanAccessCurriculumAsync(question.CurriculumId))
                return NotFound();

            var model = question.ToDisplayModel();
            model.VerbalPassageContent = question.VerbalPassage?.Content;
            model.VerbalPassageMediaUrl = question.VerbalPassage?.MediaUrl;
            model.VerbalPassageType = question.VerbalPassage?.Type;
            model.VerbalPassageDuration = question.VerbalPassage?.DurationSeconds;
            model.IsRTL = question.Curriculum?.IsRTL ?? true;

            model.Options ??= new List<QuestionOptionDisplayViewModel>();
            while (model.Options.Count < 4)
                model.Options.Add(new QuestionOptionDisplayViewModel { Text = string.Empty, ImageUrl = string.Empty });

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> PreviewFromCreate([FromBody] QuestionCreateViewModel createModel)
        {
            if (createModel == null)
                return BadRequest();

            bool isRTL = true;
            bool isQuantitative = false;

            if (createModel.CurriculumId > 0)
            {
                if (!await CanAccessCurriculumAsync(createModel.CurriculumId))
                    return Forbid();

                var curriculum = await _context.Curriculums
                    .AsNoTracking()
                    .Where(c => c.Id == createModel.CurriculumId)
                    .Select(c => new { c.IsRTL, c.IsQuantitative })
                    .FirstOrDefaultAsync();

                if (curriculum != null)
                {
                    isRTL = curriculum.IsRTL;
                    isQuantitative = curriculum.IsQuantitative;
                }
            }

            string ConvertIfNeeded(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return input;
                return isQuantitative ? NumberHelper.ToIndicNumbersSafe(input) : input;
            }

            static string? ResolveImage(string? imageUrl, string? existingImageUrl, bool removeImage)
            {
                if (removeImage) return null;
                if (!string.IsNullOrWhiteSpace(imageUrl)) return imageUrl;
                return string.IsNullOrWhiteSpace(existingImageUrl) ? null : existingImageUrl;
            }

            var options = (createModel.Options ?? new List<QuestionOptionViewModel>())
                .Select(o =>
                {
                    var imageUrl = ResolveImage(o.ImageUrl, o.ExistingImageUrl, o.RemoveImage);
                    var text = o.Text;
                    if (string.Equals(text?.Trim(), "[صورة]", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(imageUrl))
                        text = string.Empty;

                    return new QuestionOptionDisplayViewModel
                    {
                        Text = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(text)),
                        ImageUrl = imageUrl
                    };
                }).ToList();

            string? passageContent = null, passageMediaUrl = null;
            PassageType? passageType = null;
            int? passageDuration = null;

            if (createModel.VerbalPassageId.HasValue)
            {
                var passage = await _context.VerbalPassages
                    .AsNoTracking()
                    .Where(v => v.Id == createModel.VerbalPassageId.Value)
                    .Select(v => new { v.Content, v.MediaUrl, v.Type, v.DurationSeconds })
                    .FirstOrDefaultAsync();

                if (passage != null)
                {
                    passageContent = passage.Content;
                    passageMediaUrl = passage.MediaUrl;
                    passageType = passage.Type;
                    passageDuration = passage.DurationSeconds;
                }
            }

            var template = (QuestionTemplate)createModel.Template;
            var displayType = template switch
            {
                QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                QuestionTemplate.WithImage => QuestionDisplayType.WithImage,
                QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                _ => QuestionDisplayType.TextOnly
            };

            var displayModel = new QuestionDisplayViewModel
            {
                Id = Guid.NewGuid(),
                Title = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.Title)),
                ComparisonValue1 = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.ValueA)),
                ComparisonValue2 = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.ValueB)),
                Explanation = ConvertIfNeeded(System.Net.WebUtility.HtmlDecode(createModel.Explanation)),
                ImageUrl = ResolveImage(createModel.ImageUrl, createModel.ExistingImageUrl, createModel.RemoveImage),
                IsQuantitative = isQuantitative,
                IsRTL = isRTL,
                VideoUrl = createModel.VideoUrl,
                CorrectAnswer = createModel.CorrectAnswer,
                SelectedCorrectIndex = createModel.SelectedCorrectIndex,
                IsAnswerConfirmed = createModel.SelectedCorrectIndex.HasValue,
                Template = template,
                Difficulty = (DifficultyLevel)createModel.Difficulty,
                DisplayType = displayType,
                Options = options,
                VerbalPassageContent = passageContent,
                VerbalPassageMediaUrl = passageMediaUrl,
                VerbalPassageType = passageType,
                VerbalPassageDuration = passageDuration
            };

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", displayModel);
        }

        // ─── اعتماد مباشر (بدون فتح التعديل) ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSingleQuestion(Guid selectedId)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == selectedId);

            if (question == null)
                return Json(new { success = false, message = "❌ السؤال غير موجود." });

            if (!await CanAccessCurriculumAsync(question.CurriculumId))
                return Json(new { success = false, message = "🚫 لا تملك صلاحية مراجعة أسئلة هذا المنهج." });

            if (question.IsReviewed)
                return Json(new { success = false, message = "ℹ️ السؤال معتمد مسبقًا." });

            var holdMessage = await HoldMessageAsync(question.Id);
            if (holdMessage is not null)
                return Json(new { success = false, message = holdMessage });

            if (!question.IsComplete || question.IsRejected || string.IsNullOrWhiteSpace(question.CorrectAnswer))
                return Json(new { success = false, message = "⚠️ لا يمكن اعتماد هذا السؤال لأنه غير مكتمل أو مرفوض أو بدون إجابة صحيحة." });

            bool hasValidOption = question.Options.Any(o =>
                (!string.IsNullOrWhiteSpace(o.Text) && o.Text == question.CorrectAnswer) ||
                (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl == question.CorrectAnswer));

            if (!hasValidOption)
                return Json(new { success = false, message = "⚠️ الإجابة المحددة غير مرتبطة بأي خيار فعلي." });

            var user = await CurrentUserAsync();
            var now = DateTime.Now;

            question.IsReviewed = true;
            question.ReviewedByUserId = user.Id;
            question.ReviewedAt = now;

            _context.QuestionAuditLogs.Add(NewAudit(
                question.Id, ActionApproveDirect,
                "تم اعتماد السؤال مباشرة من مراجعة المدرب دون فتح التعديل.", user));

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "✅ تم اعتماد السؤال وتسجيل العملية في سجل البنك." });
        }

        // ─── سجل السؤال (من عدّل / من راجع / نوع التمرير) ────────
        [HttpGet]
        public async Task<IActionResult> History(Guid id)
        {
            var curriculumId = await _context.Questions
                .AsNoTracking()
                .Where(q => q.Id == id)
                .Select(q => (int?)q.CurriculumId)
                .FirstOrDefaultAsync();

            if (!curriculumId.HasValue || !await CanAccessCurriculumAsync(curriculumId.Value))
                return Json(new List<object>());

            var logs = await _context.QuestionAuditLogs
                .AsNoTracking()
                .Where(a => a.QuestionId == id)
                .OrderByDescending(a => a.PerformedAt)
                .Select(a => new
                {
                    action = a.Action,
                    by = a.PerformedByName,
                    at = a.PerformedAt,
                    summary = a.ChangedFieldsSummary
                })
                .ToListAsync();

            return Json(logs.Select(a => new
            {
                a.action,
                a.by,
                at = a.at.ToString("yyyy/MM/dd HH:mm"),
                a.summary
            }));
        }

        // ─── تعديل سؤال ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id, long? taskItemId = null)
        {
            ModelState.Clear();

            EditableTaskItem? taskItem = null;
            if (taskItemId.HasValue)
            {
                var (item, reject) = await ResolveTaskEditAsync(id, taskItemId.Value);
                if (reject is not null) return reject;
                taskItem = item;
            }
            else
            {
                var holdMessage = await HoldMessageAsync(id);
                if (holdMessage is not null)
                {
                    TempData["Message"] = holdMessage;
                    return RedirectToAction("PendingReview");
                }
            }

            var model = await BuildEditModelAsync(id);
            if (model == null) return NotFound();

            if (!await CanAccessCurriculumAsync(model.CurriculumId))
                return Forbid();

            model.ReviewTaskItemId = taskItem?.ItemId;
            model.ReviewTaskId = taskItem?.TaskId;
            return View("Edit", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, QuestionCreateViewModel model, bool approveAfterEdit = false, long? taskItemId = null)
        {
            // QRT-S4.1/S4.2: داخل مهمة ← التحقق من العنصر، وخارجها ← رفض الأسئلة المحتجزة
            EditableTaskItem? taskItem = null;
            if (taskItemId.HasValue)
            {
                var (item, reject) = await ResolveTaskEditAsync(id, taskItemId.Value);
                if (reject is not null) return reject;
                taskItem = item;
            }
            else
            {
                var holdMessage = await HoldMessageAsync(id);
                if (holdMessage is not null)
                {
                    TempData["Message"] = holdMessage;
                    return RedirectToAction("PendingReview");
                }
            }

            model.ReviewTaskItemId = taskItem?.ItemId;
            model.ReviewTaskId = taskItem?.TaskId;

            bool allowEmptyTitle = model.SectionId == 10;
            if (!allowEmptyTitle && string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError("Title", "يجب كتابة نص السؤال لهذا القسم.");

            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            // التحقق من المنهج الحالي للسؤال ومن المنهج الجديد المرسل
            if (!await CanAccessCurriculumAsync(question.CurriculumId) ||
                !await CanAccessCurriculumAsync(model.CurriculumId))
                return Forbid();

            if (!ModelState.IsValid)
            {
                await FillEditListsAsync(model);
                return View("Edit", model);
            }

            var oldQuestion = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Options)
                .FirstAsync(q => q.Id == id);

            bool wasReviewed = question.IsReviewed;
            var oldOptionImageUrls = question.Options.Select(o => o.ImageUrl).ToList();
            var imageUrlsToDeleteAfterSave = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var uploadedImageUrlsToDeleteOnFailure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            question.Title = model.Title;
            question.Template = (QuestionTemplate)model.Template;
            question.ValueA = model.ValueA;
            question.ValueB = model.ValueB;
            question.Explanation = model.Explanation;
            question.VideoUrl = model.VideoUrl;
            question.CurriculumId = model.CurriculumId;
            question.SectionId = model.SectionId;
            question.Hint = model.Hint;
            question.LessonId = model.LessonId;
            question.VerbalPassageId = model.VerbalPassageId;
            question.Difficulty = (DifficultyLevel)model.Difficulty;
            question.InternalNote = model.InternalNote;

            question.IsQuantitative = await _context.Curriculums
                .Where(c => c.Id == model.CurriculumId)
                .Select(c => c.IsQuantitative)
                .FirstOrDefaultAsync();

            if (model.RemoveImage)
            {
                AddImageForDeletion(imageUrlsToDeleteAfterSave, question.ImageUrl);
                question.ImageUrl = null;
            }
            else if (model.ImageFile != null)
            {
                var previous = question.ImageUrl;
                var saved = await SaveImageAsync(model.ImageFile);
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    question.ImageUrl = saved;
                    uploadedImageUrlsToDeleteOnFailure.Add(saved);
                    AddImageForDeletion(imageUrlsToDeleteAfterSave, previous);
                }
            }
            else if (!string.IsNullOrWhiteSpace(model.ExistingImageUrl))
            {
                question.ImageUrl = model.ExistingImageUrl;
            }

            question.UsageTypes = model.SelectedUsageTypes
                .Aggregate(QuestionUsageType.None, (acc, t) => acc | t);

            _context.QuestionOptions.RemoveRange(question.Options);
            question.Options = new List<QuestionOption>();

            for (int i = 0; i < model.Options.Count; i++)
            {
                var opt = model.Options[i];
                var previousOptionImageUrl = !string.IsNullOrWhiteSpace(opt.ExistingImageUrl)
                    ? opt.ExistingImageUrl
                    : oldOptionImageUrls.ElementAtOrDefault(i);

                string? imageUrl = previousOptionImageUrl;
                if (opt.RemoveImage)
                {
                    AddImageForDeletion(imageUrlsToDeleteAfterSave, previousOptionImageUrl);
                    imageUrl = null;
                }
                else if (opt.ImageFile != null)
                {
                    var savedOption = await SaveImageAsync(opt.ImageFile);
                    if (!string.IsNullOrWhiteSpace(savedOption))
                    {
                        imageUrl = savedOption;
                        uploadedImageUrlsToDeleteOnFailure.Add(savedOption);
                        AddImageForDeletion(imageUrlsToDeleteAfterSave, previousOptionImageUrl);
                    }
                }

                question.Options.Add(new QuestionOption
                {
                    Text = string.IsNullOrWhiteSpace(opt.Text) ? string.Empty : opt.Text,
                    ImageUrl = imageUrl
                });
            }

            question.CorrectAnswer = null;
            if (model.SelectedCorrectIndex.HasValue)
            {
                var correct = question.Options.ElementAtOrDefault(model.SelectedCorrectIndex.Value);
                if (correct != null)
                    question.CorrectAnswer = !string.IsNullOrWhiteSpace(correct.Text) ? correct.Text : correct.ImageUrl;
            }

            question.IsAnswerConfirmed = !string.IsNullOrWhiteSpace(question.CorrectAnswer);
            question.IsComplete = question.Options.Any(o =>
                !string.IsNullOrWhiteSpace(o.Text) || !string.IsNullOrWhiteSpace(o.ImageUrl));

            if (!wasReviewed)
            {
                question.IsReviewed = false;
                question.IsRejected = false;
            }

            var user = await CurrentUserAsync();
            var changeSummary = QuestionChangeTracker.GetChangesSummary(oldQuestion, question);

            _context.QuestionAuditLogs.Add(NewAudit(question.Id, ActionEdit, changeSummary, user));

            bool approved = false;
            if (approveAfterEdit && !wasReviewed)
            {
                if (question.IsComplete && !question.IsRejected && question.IsAnswerConfirmed)
                {
                    question.IsReviewed = true;
                    question.ReviewedByUserId = user.Id;
                    question.ReviewedAt = DateTime.Now;
                    approved = true;

                    _context.QuestionAuditLogs.Add(NewAudit(
                        question.Id, ActionEditAndApprove,
                        "تم اعتماد السؤال بعد فتح التعديل وإجراء التعديل من مراجعة المدرب.", user));
                }
            }

            var retained = new[] { question.ImageUrl }
                .Concat(question.Options.Select(o => o.ImageUrl))
                .ToList();

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                DeleteImageFiles(uploadedImageUrlsToDeleteOnFailure, Enumerable.Empty<string?>());
                throw;
            }

            DeleteImageFiles(imageUrlsToDeleteAfterSave, retained);

            if (taskItem is not null)
                return await CompleteTaskEditAsync(taskItem, user, approved, approveAfterEdit && !wasReviewed);

            TempData["Message"] = approved
                ? "✅ تم حفظ التعديل واعتماد السؤال، وسُجلت العملية في سجل البنك."
                : approveAfterEdit && !wasReviewed
                    ? "⚠️ تم حفظ التعديل لكن لم يُعتمد السؤال لأنه غير مكتمل أو بدون إجابة صحيحة."
                    : "✅ تم حفظ التعديل وسُجلت العملية في سجل البنك.";

            return RedirectToAction("PendingReview", new { curriculumId = question.CurriculumId });
        }

        // QRT-S4.1: ختام التعديل داخل مهمة — تسجيل «عُدِّل واعتُمد» على العنصر والعودة لصفحة المهمة
        private async Task<IActionResult> CompleteTaskEditAsync(
            EditableTaskItem taskItem, (string Id, string Name) user, bool approved, bool approvalRequested)
        {
            string message;
            if (approved)
            {
                var instructorId = await RequireInstructorAsync();
                var marked = await _reviewTasks.MarkEditedAndApprovedAsync(
                    instructorId, taskItem.ItemId, new ReviewActor(user.Id, user.Name, CurrentRole()));
                _taskQuery.InvalidatePendingCount(user.Id);

                message = marked.Success
                    ? $"✅ تم حفظ التعديل واعتماد السؤال ضمن المهمة {taskItem.TaskCode}."
                    : $"✅ حُفظ التعديل واعتُمد السؤال، لكن تعذّر تحديث المهمة: {marked.Message}";
            }
            else if (approvalRequested)
            {
                message = "⚠️ تم حفظ التعديل لكن لم يُعتمد السؤال لأنه غير مكتمل أو بدون إجابة صحيحة؛ ما زال بانتظار مراجعتك في المهمة.";
            }
            else
            {
                message = "✅ تم حفظ التعديل، وما زال السؤال بانتظار اعتمادك في المهمة.";
            }

            TempData["Message"] = message;
            return RedirectToAction("Review", "QuestionReviewTasks", new { area = "Instructors", id = taskItem.TaskId });
        }

        // ─── مساعدات بناء نموذج التعديل ──────────────────────────
        private async Task<QuestionCreateViewModel?> BuildEditModelAsync(Guid id)
        {
            var question = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Curriculum)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return null;

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
                var correct = question.CorrectAnswer.Trim();
                var idx = optionsList.FindIndex(o =>
                    (!string.IsNullOrWhiteSpace(o.Text) && o.Text.Trim() == correct) ||
                    (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl.Trim() == correct));
                if (idx >= 0) selectedCorrectIndex = idx;
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
                Options = BuildOptionsForEdit(optionsList)
            };

            await FillEditListsAsync(model);

            ViewBag.PassageDuration = await _context.VerbalPassages
                .AsNoTracking()
                .Where(v => v.Id == question.VerbalPassageId)
                .Select(v => v.DurationSeconds)
                .FirstOrDefaultAsync();

            return model;
        }

        private async Task FillEditListsAsync(QuestionCreateViewModel model)
        {
            var allowed = new HashSet<int>(await GetAccessibleCurriculumIdsAsync());

            var curriculums = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            model.Curriculums = curriculums
                .Where(c => allowed.Contains(c.Id))
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title,
                    Selected = c.Id == model.CurriculumId
                })
                .ToList();

            model.Sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == model.CurriculumId)
                .OrderBy(s => s.Title)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Title,
                    Selected = s.Id == model.SectionId
                })
                .ToListAsync();

            model.Lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == model.SectionId && l.IsActive)
                .OrderBy(l => l.Title)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Title,
                    Selected = l.Id == model.LessonId
                })
                .ToListAsync();

            var passages = await _context.VerbalPassages
                .AsNoTracking()
                .OrderBy(v => v.Title)
                .Select(v => new { v.Id, v.Title })
                .ToListAsync();

            model.VerbalPassages = passages
                .Select(v => new SelectListItem
                {
                    Value = v.Id.ToString(),
                    Text = v.Title,
                    Selected = model.VerbalPassageId.HasValue && model.VerbalPassageId.Value == v.Id
                })
                .ToList();
        }

        private static List<QuestionOptionViewModel> BuildOptionsForEdit(List<QuestionOption> dbOptions)
        {
            var result = new List<QuestionOptionViewModel>();

            foreach (var option in dbOptions.OrderBy(o => o.Id))
            {
                var imageUrl = string.IsNullOrWhiteSpace(option.ImageUrl) ? null : option.ImageUrl.Trim();
                result.Add(new QuestionOptionViewModel
                {
                    Text = string.IsNullOrWhiteSpace(option.Text) || option.Text.Trim() == "[صورة]"
                        ? string.Empty
                        : option.Text.Trim(),
                    ExistingImageUrl = imageUrl,
                    ImageUrl = imageUrl,
                    RemoveImage = false
                });
            }

            while (result.Count < 4)
                result.Add(new QuestionOptionViewModel { Text = string.Empty });

            return result.Take(4).ToList();
        }

        // ─── رفع/حذف صور الأسئلة (نفس مسار الإدارة) ──────────────
        private static async Task<string?> SaveImageAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "questions");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return QuestionUploadVirtualPath + uniqueFileName;
        }

        private static bool IsLocalUploadUrl(string? imageUrl) =>
            !string.IsNullOrWhiteSpace(imageUrl) &&
            imageUrl.Replace('\\', '/').StartsWith(QuestionUploadVirtualPath, StringComparison.OrdinalIgnoreCase);

        private static void AddImageForDeletion(HashSet<string> urls, string? imageUrl)
        {
            if (IsLocalUploadUrl(imageUrl)) urls.Add(imageUrl!);
        }

        private static void DeleteImageFiles(IEnumerable<string> imageUrls, IEnumerable<string?> retainedImageUrls)
        {
            var retained = retainedImageUrls
                .Where(IsLocalUploadUrl)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var uploadsFolder = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "questions"));
            var allowedPrefix = uploadsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            foreach (var url in imageUrls.Where(IsLocalUploadUrl).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (retained.Contains(url)) continue;

                var fileName = Path.GetFileName(url.Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                var filePath = Path.GetFullPath(Path.Combine(uploadsFolder, fileName));
                if (!filePath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }
                catch
                {
                    // حذف الملف لا يجب أن يفشل الحفظ بعد نجاح قاعدة البيانات.
                }
            }
        }
    }
}
