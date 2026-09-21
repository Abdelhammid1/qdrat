using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Instructors.Exams;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Instructor.Exam;
using QdratNew.ViewModels.Instructors.Exam;
using static QdratNew.ViewModels.Exam.ExamAssignmentDetailsViewModel;

namespace QdratNew.Areas.Instructors.Controllers.Exam
{
    [Area("Instructors")]
    public class InstructorExamDraftController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;
        private readonly IExamGenerationService _generationService;
        private readonly InstructorExamDraftManagementService _draftService;
        public InstructorExamDraftController(
            ApplicationDbContext context,
            InstructorExamDraftManagementService draftService,
            IExamGenerationService generationService,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
            _draftService = draftService;
            _generationService = generationService;
        }

        // =========================================
        // 📋 Drafts (Scoped)
        // =========================================
        public async Task<IActionResult> Drafts()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var drafts = await _context.ExamDrafts
                .AsNoTracking()
                .Where(d =>
                    !d.IsArchived &&
                    d.CreatedByInstructorId == instructorId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new ExamDraftVM
                {
                    Id = d.Id,
                    Title = d.Title,
                    CreatedAt = d.CreatedAt,
                    QuestionCount = d.DraftQuestions.Count
                })
                .ToListAsync();

            return View(drafts);
        }



        // =========================================
        // 🧠 Create Draft
        // =========================================
        public async Task<IActionResult> Create()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var allowedCurriculumIds = await GetInstructorAllowedCurriculumIdsAsync(instructorId);

            var curriculums = await _context.Curriculums
                .AsNoTracking()
                .Select(c => new SelectItemVM
                {
                    Id = c.Id,
                    Name = c.Title
                })
                .ToListAsync();

            var model = new ExamDraftCreateVM
            {
                Curriculums = curriculums
                    .Where(c => allowedCurriculumIds.Contains(c.Id))
                    .OrderBy(c => c.Name)
                    .ToList()
            };

            return View(model);
        }




        // =========================================
        // 👁️ Preview (Scoped)
        // =========================================
        public async Task<IActionResult> PreviewDraft(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var draft = await _context.ExamDrafts
                .AsNoTracking()
                .Where(d => d.Id == id && d.CreatedByInstructorId == instructorId)
                .Select(d => new ExamDraftVM
                {
                    Id = d.Id,
                    Title = d.Title,
                    CreatedAt = d.CreatedAt,

                    SectionGroups = d.DraftQuestions
                        .GroupBy(q => q.Question.Lesson.Section)
                        .Select(g => new SectionGroupVM
                        {
                            SectionId = g.Key.Id,
                            SectionTitle = g.Key.Title,

                            Questions = g
                                .OrderBy(x => x.Order)
                                .Select(x => new ExamDraftQuestionVM
                                {
                                    QuestionId = x.QuestionId,
                                    QuestionTitle = x.Question.Title
                                }).ToList()
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (draft == null)
                return NotFound();

            return View(draft);
        }

        public async Task<IActionResult> SearchQuestions(
      int draftId,
      int sectionId,
      int? lessonId,
      int? difficulty,
      string search,
      int page = 1)
        {
            int pageSize = 20;

            var allQuestions = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Lesson)
                .ToListAsync();

            var filtered = allQuestions
                .Where(q => q.Lesson.SectionId == sectionId)
                .ToList();

            if (lessonId.HasValue)
                filtered = filtered.Where(q => q.LessonId == lessonId).ToList();

            if (difficulty.HasValue)
                filtered = filtered.Where(q => (int)q.Difficulty == difficulty).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();

                filtered = filtered.Where(q =>
                    (q.Title != null && q.Title.ToLower().Contains(search)) ||
                    (q.InternalNote != null && q.InternalNote.ToLower().Contains(search))
                ).ToList();
            }

            int totalCount = filtered.Count;

            var paged = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var existingIds = await _context.ExamDraftQuestions
                .Where(x => x.ExamDraftId == draftId)
                .Select(x => x.QuestionId)
                .ToListAsync();

            return Json(new
            {
                total = totalCount,
                data = paged.Select(q => new
                {
                    id = q.Id,
                    title = q.Title,
                    note = q.InternalNote,
                    exists = existingIds.Contains(q.Id)
                })
            });
        }


        public async Task<IActionResult> PreviewQuestion(Guid questionId)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                        .ThenInclude(s => s.Curriculum)
                .Include(q => q.VerbalPassage)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Content("السؤال غير موجود");

            var vm = question.ToDisplayModel(); // 🔥 أهم سطر

            return PartialView("_QuestionPreviewPartial", vm);
        }        // =========================================
        // 🚀 SendDraft (GET Scoped)
        // =========================================
        public async Task<IActionResult> SendDraft(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            // 🟢 جلب الدفعات المسموح بها
            var allowedBatchIds = await _scopeService.GetAllowedBatchIdsAsync(instructorId);

            var batches = await _context.Batches
                .AsNoTracking()
                .ToListAsync();

            var filteredBatches = batches
                .Where(b => allowedBatchIds.Contains(b.Id))
                .ToList();

            // 🟢 جلب الطلاب المسموح بهم
            var allowedStudentIds = await _scopeService.GetAllowedStudentIdsAsync(instructorId);

            var students = await _context.Students
                .AsNoTracking()
                .ToListAsync();

            var filteredStudents = students
                .Where(s => allowedStudentIds.Contains(s.StudentID))
                .ToList();

            var vm = new ViewModels.Instructor.Exam.SendExamDraftVM
            {
                DraftId = id,

                Batches = filteredBatches
                    .Select(b => new SelectItemVM
                    {
                        Id = b.Id,
                        Name = b.Name
                    }).ToList(),

                Students = filteredStudents
                    .Select(s => new SelectItemVM
                    {
                        Id = s.StudentID,
                        Name = s.FullName
                    }).ToList()
            };

            return View(vm);
        }


        // =========================================
        // ⚙️ AutoGenerate (GET) - ACTIVE LESSONS ONLY
        // =========================================
        public async Task<IActionResult> AutoGenerate(int curriculumId)
        {

            TempData.Remove("Error"); // 🔥 حل المشكلة


            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var hasAccess = await InstructorHasAccessToCurriculumAsync(instructorId, curriculumId);
            if (!hasAccess)
                return Forbid();

            // =========================================
            // 1) Sections
            // =========================================
            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == curriculumId)
                .ToListAsync();

            if (sections.Count == 0)
            {
                return View(new ExamAutoGenerateVM
                {
                    CurriculumId = curriculumId
                });
            }

            // =========================================
            // 2) تحميل Lessons (كلها)
            // =========================================
            var allLessons = await _context.Lessons
                .AsNoTracking()
                .ToListAsync();

            // =========================================
            // 3) فلترة Lessons الفعالة فقط (في الذاكرة)
            // =========================================
            var activeLessons = allLessons
                .Where(l => l.IsActive) // 🔥 الشرط الأساسي
                .ToList();

            // =========================================
            // 4) فلترة حسب Sections
            // =========================================
            var sectionIds = sections.Select(s => s.Id).ToList();

            var filteredLessons = activeLessons
                .Where(l => sectionIds.Contains(l.SectionId))
                .ToList();

            // =========================================
            // 5) تحميل الأسئلة (مرة واحدة للإحصائيات)
            // =========================================
            var questions = await _context.Questions
                .AsNoTracking()
                .ToListAsync();

            // =========================================
            // 6) بناء ViewModel
            // =========================================
            var vm = new ExamAutoGenerateVM
            {
                CurriculumId = curriculumId,
                Sections = sections.Select(s => new SectionWithLessonsVM
                {
                    SectionId = s.Id,
                    SectionName = s.Title,

                    Lessons = filteredLessons
                        .Where(l => l.SectionId == s.Id)
                        .Select(l => new LessonGenerateVM
                        {
                            LessonId = l.Id,
                            LessonName = l.Title,

                            // إحصائية
                            AvailableQuestionsCount = questions.Count(q => q.LessonId == l.Id),
                            SelectedCount = 0
                        })
                        .ToList()
                }).ToList()
            };

            return View(vm);
        }




        // =========================================
        // ⚙️ AutoGenerate (POST) - GENERATE DRAFT
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoGenerate(ExamAutoGenerateVM model)
        {
            TempData.Remove("Error");

            // تنظيف ModelState من الحقول غير المهمة
            ModelState.Remove("Sections");

            foreach (var key in ModelState.Keys
                .Where(k => k.Contains("SectionName") || k.Contains("LessonName"))
                .ToList())
            {
                ModelState.Remove(key);
            }

            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
            {
                TempData["Error"] = "⚠️ تعذر تحميل بيانات الحساب";
                return RedirectToAction(nameof(Drafts));
            }

            if (model == null)
            {
                TempData["Error"] = "⚠️ البيانات غير صحيحة";
                return RedirectToAction(nameof(Drafts));
            }

            var hasAccess = await InstructorHasAccessToCurriculumAsync(instructorId, model.CurriculumId);
            if (!hasAccess)
            {
                TempData["Error"] = "⚠️ هذا المنهج غير مرتبط بصلاحيات المدرب";
                return RedirectToAction(nameof(Create));
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("", "يجب إدخال عنوان للنموذج");
                await ReloadAutoGenerateData(model);
                return View(model);
            }

            // =========================================
            // تجميع الدروس المطلوبة
            // =========================================
            var requestedLessons = new List<LessonGenerateVM>();

            foreach (var section in model.Sections ?? new List<SectionWithLessonsVM>())
            {
                foreach (var lesson in section.Lessons ?? new List<LessonGenerateVM>())
                {
                    if (lesson.SelectedCount > 0)
                    {
                        requestedLessons.Add(lesson);
                    }
                }
            }

            if (requestedLessons.Count == 0)
            {
                ModelState.AddModelError("", "يجب اختيار عدد أسئلة لمؤشر واحد على الأقل");
                await ReloadAutoGenerateData(model);
                return View(model);
            }

            var context = _context;

            int draftId = 0;

            var strategy = context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        var now = DateTime.UtcNow;

                        // =========================================
                        // إنشاء Draft
                        // =========================================
                        var draft = new ExamDraft
                        {
                            Title = model.Title,
                            CurriculumId = model.CurriculumId,
                            CreatedByInstructorId = instructorId,
                            CreatedAt = now,
                            IsArchived = false
                        };

                        context.ExamDrafts.Add(draft);
                        await context.SaveChangesAsync();

                        draftId = draft.Id;

                        // =========================================
                        // تحميل الأسئلة مرة واحدة
                        // =========================================
                        var allQuestions = await context.Questions
                            .AsNoTracking()
                            .ToListAsync();

                        var selectedQuestions = new List<Question>();

                        foreach (var lesson in requestedLessons)
                        {
                            var lessonQuestions = allQuestions
                                .Where(q => q.LessonId == lesson.LessonId)
                                .Take(lesson.SelectedCount)
                                .ToList();

                            selectedQuestions.AddRange(lessonQuestions);
                        }

                        // منع التكرار
                        selectedQuestions = selectedQuestions
                            .GroupBy(q => q.Id)
                            .Select(g => g.First())
                            .ToList();

                        // =========================================
                        // إنشاء DraftQuestions
                        // =========================================
                        var draftQuestions = new List<ExamDraftQuestion>();

                        int order = 1;

                        foreach (var q in selectedQuestions)
                        {
                            draftQuestions.Add(new ExamDraftQuestion
                            {
                                ExamDraftId = draft.Id,
                                QuestionId = q.Id,
                                Order = order++
                            });
                        }

                        await context.BulkInsertAsync(draftQuestions);

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                TempData["Success"] = "تم إنشاء النموذج بنجاح";

                return RedirectToAction(nameof(PreviewDraft), new { id = draftId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);

                await ReloadAutoGenerateData(model);

                return View(model);
            }
        }


        private async Task ReloadAutoGenerateData(ExamAutoGenerateVM model)
        {
            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == model.CurriculumId)
                .ToListAsync();

            var allLessons = await _context.Lessons
                .AsNoTracking()
                .ToListAsync();

            var activeLessons = allLessons
                .Where(l => l.IsActive)
                .ToList();

            var sectionIds = sections.Select(s => s.Id).ToList();

            var filteredLessons = activeLessons
                .Where(l => sectionIds.Contains(l.SectionId))
                .ToList();

            var questions = await _context.Questions
                .AsNoTracking()
                .ToListAsync();

            model.Sections = sections.Select(s => new SectionWithLessonsVM
            {
                SectionId = s.Id,
                SectionName = s.Title,

                Lessons = filteredLessons
                    .Where(l => l.SectionId == s.Id)
                    .Select(l => new LessonGenerateVM
                    {
                        LessonId = l.Id,
                        LessonName = l.Title,
                        AvailableQuestionsCount = questions.Count(q => q.LessonId == l.Id),
                        SelectedCount = 0
                    })
                    .ToList()
            }).ToList();
        }

        [HttpPost]
        public async Task<IActionResult> Archive(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var draft = await _context.ExamDrafts
                .FirstOrDefaultAsync(d => d.Id == id && d.CreatedByInstructorId == instructorId);

            if (draft == null)
                return NotFound();

            draft.IsArchived = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم أرشفة النموذج";
            return RedirectToAction(nameof(Drafts));
        }



        public async Task<IActionResult> Archived()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var drafts = await _context.ExamDrafts
                .AsNoTracking()
                .Where(d =>
                    d.IsArchived &&
                    d.CreatedByInstructorId == instructorId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new ExamDraftVM
                {
                    Id = d.Id,
                    Title = d.Title,
                    CreatedAt = d.CreatedAt,
                    QuestionCount = d.DraftQuestions.Count
                })
                .ToListAsync();

            return View(drafts);
        }


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var draft = await _context.ExamDrafts
                .Include(d => d.DraftQuestions)
                .FirstOrDefaultAsync(d => d.Id == id && d.CreatedByInstructorId == instructorId);

            if (draft == null)
                return NotFound();

            _context.ExamDraftQuestions.RemoveRange(draft.DraftQuestions);
            _context.ExamDrafts.Remove(draft);

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف النموذج";
            return RedirectToAction(nameof(Drafts));
        }


        public async Task<IActionResult> RemoveQuestion(int draftId, Guid questionId)
        {
            var success = await _draftService.RemoveQuestionAsync(draftId, questionId);

            if (!success)
                TempData["Error"] = "فشل حذف السؤال.";

            return RedirectToAction(nameof(PreviewDraft), new { id = draftId });
        }


        public async Task<IActionResult> AddQuestion(
            int draftId,
            int sectionId,
            int? lessonId,
            int? difficulty,
            string search,
            int page = 1)
        {
            int pageSize = 20;

            var allQuestions = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Lesson)
                .ToListAsync();

            // 🔥 Section
            var filtered = allQuestions
                .Where(q => q.Lesson.SectionId == sectionId)
                .ToList();

            // 🔥 Lesson
            if (lessonId.HasValue)
            {
                filtered = filtered
                    .Where(q => q.LessonId == lessonId.Value)
                    .ToList();
            }

            // 🔥 Difficulty
            if (difficulty.HasValue)
            {
                filtered = filtered
                    .Where(q => (int)q.Difficulty == difficulty.Value)
                    .ToList();
            }

            // 🔥 Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();

                filtered = filtered.Where(q =>
                    (q.Title != null && q.Title.ToLower().Contains(search)) ||
                    (q.InternalNote != null && q.InternalNote.ToLower().Contains(search))
                ).ToList();
            }



            // 🔥 احصائيات حسب Difficulty
            var easyCount = filtered.Count(q => (int)q.Difficulty == 0);
            var mediumCount = filtered.Count(q => (int)q.Difficulty == 1);
            var hardCount = filtered.Count(q => (int)q.Difficulty == 2);
            var totalCountAll = filtered.Count;





            // 🔥 Pagination
            int totalCount = filtered.Count;

            var paged = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var existing = await _context.ExamDraftQuestions
                .Where(x => x.ExamDraftId == draftId)
                .Select(x => x.QuestionId)
                .ToListAsync();

            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .Select(l => new LessonFilterVM
                {
                    Id = l.Id,
                    Name = l.Title
                })
                .ToListAsync();

            return View(new AddQuestionVM
            {
                DraftId = draftId,
                SectionId = sectionId,
                SelectedLessonId = lessonId,
                SelectedDifficulty = difficulty,
                Search = search,
                Lessons = lessons,
                EasyCount = easyCount,
                MediumCount = mediumCount,
                HardCount = hardCount,
                TotalCountAll = totalCountAll,
                ExistingQuestionIds = existing,
                CurrentPage = page,
                TotalCount = totalCount,
                PageSize = pageSize,

                Questions = paged.Select(q => new QuestionItemVM
                {
                    Id = q.Id,
                    Title = q.Title,
                    InternalNote = q.InternalNote
                }).ToList()
            });
        }


        [HttpPost]
        public async Task<IActionResult> AddQuestion(int draftId, Guid questionId)
        {
            var success = await _draftService.AddQuestionAsync(draftId, questionId);

            if (!success)
                TempData["Error"] = "فشل إضافة السؤال.";

            return RedirectToAction(nameof(PreviewDraft), new { id = draftId });
        }


        public async Task<IActionResult> SelectSection(int draftId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var allowedCurriculumIds = await GetInstructorAllowedCurriculumIdsAsync(instructorId);

            var allSections = await _context.Sections
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    s.CurriculumId
                })
                .ToListAsync();

            var sections = allSections
                .Where(s => allowedCurriculumIds.Contains(s.CurriculumId))
                .Select(s => new
                {
                    s.Id,
                    s.Title
                })
                .ToList();

            ViewBag.DraftId = draftId;

            return View(sections);
        }




        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var draft = await _context.ExamDrafts
                .FirstOrDefaultAsync(d => d.Id == id && d.CreatedByInstructorId == instructorId);

            if (draft == null)
                return NotFound();

            draft.IsArchived = false;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم استرجاع النموذج";

            return RedirectToAction(nameof(Archived));
        }


        [HttpGet]
        public IActionResult ReplaceQuestion(int draftId, Guid oldQuestionId, int sectionId)
        {
            var vm = new ReplaceQuestionPageVM
            {
                DraftId = draftId,
                OldQuestionId = oldQuestionId,
                SectionId = sectionId,

                TotalCountAll = _context.Questions.Count(q => q.SectionId == sectionId),
                EasyCount = _context.Questions.Count(q => q.SectionId == sectionId && q.Difficulty == DifficultyLevel.Easy),
                MediumCount = _context.Questions.Count(q => q.SectionId == sectionId && q.Difficulty == DifficultyLevel.Medium),
                HardCount = _context.Questions.Count(q => q.SectionId == sectionId && q.Difficulty == DifficultyLevel.Hard),

                Lessons = _context.Lessons
                    .Where(l => l.SectionId == sectionId)
                    .Select(l => new LessonVM
                    {
                        Id = l.Id,
                        Name = l.Title
                    }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplaceQuestion(int draftId, Guid oldQuestionId, Guid newQuestionId)
        {
            var success = await _draftService.ReplaceQuestionAsync(
                draftId,
                oldQuestionId,
                newQuestionId);

            if (!success)
                TempData["Error"] = "فشل استبدال السؤال.";

            return RedirectToAction(nameof(PreviewDraft), new { id = draftId });
        }


        [HttpGet]
        public IActionResult SearchReplacementQuestions(
          int draftId,
          Guid oldId,
          int sectionId,
          int? lessonId,
          int? difficulty,
          string search,
          int page = 1)
        {
            int pageSize = 20;

            // ============================
            // 1️⃣ Base Query (SQL Safe)
            // ============================
            var query = _context.Questions
                .AsNoTracking()
                .Where(q =>
                    q.SectionId == sectionId &&
                    q.Id != oldId &&
                    q.IsComplete &&
                    q.IsAnswerConfirmed);

            if (lessonId.HasValue)
                query = query.Where(q => q.LessonId == lessonId);

            if (difficulty.HasValue)
                query = query.Where(q => (int)q.Difficulty == difficulty);

            // ============================
            // 2️⃣ تحميل البيانات الأساسية فقط
            // ============================
            var baseData = query
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.InternalNote,
                    q.Explanation,
                    q.CreatedAt
                })
                .ToList();

            // ============================
            // 3️⃣ البحث داخل الذاكرة (SQL 2014 SAFE)
            // ============================
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                baseData = baseData
                    .Where(q =>
                        (!string.IsNullOrEmpty(q.Title) && q.Title.Contains(search)) ||
                        (!string.IsNullOrEmpty(q.InternalNote) && q.InternalNote.Contains(search)) ||
                        (!string.IsNullOrEmpty(q.Explanation) && q.Explanation.Contains(search))
                    )
                    .ToList();
            }

            // ============================
            // 4️⃣ العدد الكلي
            // ============================
            var total = baseData.Count;

            // ============================
            // 5️⃣ Pagination داخل الذاكرة
            // ============================
            var data = baseData
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    id = q.Id,
                    title = q.Title,
                    note = q.Explanation // ✔ للعرض فقط
                })
                .ToList();

            // ============================
            // 6️⃣ Response
            // ============================
            return Json(new
            {
                total,
                data
            });
        }


    }
}
