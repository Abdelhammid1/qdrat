using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.HomeworkDraft.Interfaces;
using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;

namespace QdratNew.Services.HomeworkDraft.Implementations
{
    public class HomeworkDraftService : IHomeworkDraftService
    {
        private readonly ApplicationDbContext _context;

        public HomeworkDraftService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // 📄 قائمة المسودات
        // ===============================
        public List<HomeworkDraftListVM> GetDrafts(
            int partnerId,
            int subscriptionPeriodId)
        {
            // =========================
            // جلب المسودات الأساسية
            // =========================
            var drafts = _context.HomeworkDrafts
                .AsNoTracking()
                .Where(d =>
                    d.PartnerId == partnerId &&
                    d.SubscriptionPeriodId == subscriptionPeriodId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.CreatedAt,
                    QuestionsCount = d.Questions.Count(),

                    // قد لا يكون للمسودة Curriculum إذا لم تحتوِ أسئلة
                    CurriculumId = d.Questions
                        .Select(q => (int?)q.Question.Lesson.Section.CurriculumId)
                        .FirstOrDefault()
                })
                .ToList();

            // =========================
            // جلب أسماء الدورات مرة واحدة
            // =========================
            var allCourseCurriculums = _context.CourseCurriculums
                .AsNoTracking()
                .Select(cc => new
                {
                    cc.CurriculumId,
                    CourseName = cc.Course.Name
                })
                .ToList();

            // =========================
            // بناء Lookup في الذاكرة
            // =========================
            var courseLookup = allCourseCurriculums
                .GroupBy(c => c.CurriculumId)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().CourseName
                );

            // =========================
            // بناء النتيجة النهائية
            // =========================
            var result = new List<HomeworkDraftListVM>();

            foreach (var d in drafts)
            {
                string courseName = "غير محدد";

                if (d.CurriculumId.HasValue &&
                    courseLookup.ContainsKey(d.CurriculumId.Value))
                {
                    courseName = courseLookup[d.CurriculumId.Value];
                }

                result.Add(new HomeworkDraftListVM
                {
                    DraftId = d.Id,
                    Title = d.Title,
                    CreatedAt = d.CreatedAt,
                    TotalQuestions = d.QuestionsCount,
                    CourseName = courseName
                });
            }

            return result;
        }

        // ===============================
        // 💾 حفظ مسودة
        // ===============================
        public int SaveDraft(
            SaveHomeworkDraftVM model,
            int partnerId,
            int subscriptionPeriodId)
        {
            if (model == null || !model.QuestionIds.Any())
                throw new InvalidOperationException("لا توجد أسئلة لحفظ المسودة.");

            var draft = new QdratNew.Entities.HomeworkDraft
            {
                Title = model.Title,
                PartnerId = partnerId,
                SubscriptionPeriodId = subscriptionPeriodId,
                CreatedAt = DateTime.Now
            };

            _context.HomeworkDrafts.Add(draft);
            _context.SaveChanges();

            int order = 1;
            foreach (var qId in model.QuestionIds)
            {
                _context.HomeworkDraftQuestions.Add(new HomeworkDraftQuestion
                {
                    HomeworkDraftId = draft.Id,
                    QuestionId = qId,
                    Order = order++
                });
            }

            _context.SaveChanges();
            return draft.Id;
        }



        // ===============================
        // 🤖 توليد تلقائي
        // ===============================
        public int GenerateAutoDraft(
         int partnerId,
         int subscriptionPeriodId,
         HomeworkAutoGenerateVM model)
        {
            if (model.SelectedLessonIds == null || !model.SelectedLessonIds.Any())
                throw new InvalidOperationException("لم يتم اختيار أي مؤشرات للتوليد.");

            if (model.QuestionsPerLesson <= 0)
                throw new InvalidOperationException("عدد الأسئلة لكل مؤشر غير صحيح.");

            // =========================================
            // 1️⃣ إنشاء المسودة
            // =========================================
            var draft = new QdratNew.Entities.HomeworkDraft
            {
                Title = model.Title,
                PartnerId = partnerId,
                SubscriptionPeriodId = subscriptionPeriodId,
                CourseId = model.CourseId,
                CreatedAt = DateTime.UtcNow
            };

            _context.HomeworkDrafts.Add(draft);
            _context.SaveChanges();

            // =========================================
            // 2️⃣ جلب الأسئلة المؤهلة فقط (تقليل حجم البيانات)
            // =========================================
            var allQuestions = _context.Questions
                .AsNoTracking()
                .Where(q =>
                    q.LessonId != null &&
                    q.IsComplete &&
                    q.IsAnswerConfirmed)
                .Select(q => new
                {
                    q.Id,
                    q.LessonId
                })
                .ToList();
            // =========================================
            // 3️⃣ فلترة الأسئلة في الذاكرة حسب المؤشرات المختارة
            // =========================================
            // بناء Lookup للمؤشرات المختارة (بدون Contains)
            var selectedLessonLookup = model.SelectedLessonIds
                .ToDictionary(id => id, id => true);

            // فلترة الأسئلة بدون Contains نهائيًا
            var filteredQuestions = new List<(Guid Id, int LessonId)>();
            foreach (var q in allQuestions)
            {
                if (selectedLessonLookup.ContainsKey(q.LessonId))
                {
                    filteredQuestions.Add((q.Id, q.LessonId));
                }
            }

            if (!filteredQuestions.Any())
                throw new InvalidOperationException("لا توجد أسئلة مرتبطة بالمؤشرات المختارة.");

            // =========================================
            // 4️⃣ توزيع الأسئلة لكل مؤشر
            // =========================================
            int order = 1;

            foreach (var lessonId in model.SelectedLessonIds)
            {
                var lessonQuestions = filteredQuestions
      .Where(q => q.LessonId == lessonId)
      .OrderBy(x => Guid.NewGuid())
      .Take(model.QuestionsPerLesson)
      .ToList();

                foreach (var question in lessonQuestions)
                {
                    _context.HomeworkDraftQuestions.Add(new HomeworkDraftQuestion
                    {
                        HomeworkDraftId = draft.Id,
                        QuestionId = question.Id,
                        Order = order++
                    });
                }
            }

            _context.SaveChanges();

            // =========================================
            // 5️⃣ تحقق نهائي
            // =========================================
            bool hasAny = _context.HomeworkDraftQuestions
                .Any(q => q.HomeworkDraftId == draft.Id);

            if (!hasAny)
                throw new InvalidOperationException("لم يتم توليد أي أسئلة في المسودة.");

            return draft.Id;
        }



        public int GenerateDraftFromProfessionalModel(
            int partnerId,
            int subscriptionPeriodId,
            HomeworkGenerateFromProfessionalModelVM model)
        {
            if (model == null)
                throw new InvalidOperationException("بيانات النموذج الاحترافي غير صحيحة.");

            // =====================================
            // جلب الأسئلة من النموذج الاحترافي
            // =====================================
            var questionIds = _context.ProfessionalModelQuestions
         .Where(x => x.Model.Id == model.ProfessionalModelId)
         .Select(x => x.QuestionId)
         .Where(q => q.HasValue)
         .Select(q => q.Value)
         .ToList();


            if (!questionIds.Any())
                throw new InvalidOperationException("النموذج الاحترافي لا يحتوي على أسئلة.");

            return SaveDraft(
                new SaveHomeworkDraftVM
                {
                    Title = model.Title,
                    QuestionIds = questionIds
                },
                partnerId,
                subscriptionPeriodId
            );
        }
        public ReplaceHomeworkDraftQuestionVM GetReplaceCandidates(
    int draftId,
    Guid oldQuestionId,
    int lessonId)
        {
            var old = _context.Questions
                .Where(q => q.Id == oldQuestionId)
                .Select(q => q.Title)
                .FirstOrDefault();

            var usedIds = _context.HomeworkDraftQuestions
                .Where(x => x.HomeworkDraftId == draftId)
                .Select(x => x.QuestionId)
                .ToList();

            var candidates = _context.Questions
                .Where(q =>
                    q.LessonId == lessonId &&
                    q.IsComplete &&
                    q.IsAnswerConfirmed)
                .Select(q => new
                {
                    q.Id,
                    q.Title
                })
                .ToList()               // 👈 DB
                .Where(q => !usedIds.Contains(q.Id)) // 👈 Memory
                .Select(q => new ReplaceCandidateQuestionVM
                {
                    QuestionId = q.Id,
                    Title = q.Title
                })
                .ToList();

            return new ReplaceHomeworkDraftQuestionVM
            {
                DraftId = draftId,
                OldQuestionId = oldQuestionId,
                OldQuestionTitle = old ?? "",
                LessonId = lessonId,
                Candidates = candidates
            };
        }


        public void ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId)
        {
            if (newQuestionId == Guid.Empty)
                throw new InvalidOperationException("لم يتم اختيار سؤال بديل.");

            // 1️⃣ تأكد أن السؤال الجديد موجود
            var newQuestionExists = _context.Questions
                .AsNoTracking()
                .Any(q => q.Id == newQuestionId);

            if (!newQuestionExists)
                throw new InvalidOperationException("السؤال البديل غير موجود.");

            // 2️⃣ السؤال القديم داخل المسودة
            var oldDraftQuestion = _context.HomeworkDraftQuestions
                .FirstOrDefault(x =>
                    x.HomeworkDraftId == draftId &&
                    x.QuestionId == oldQuestionId);

            if (oldDraftQuestion == null)
                throw new InvalidOperationException("السؤال المراد استبداله غير موجود داخل المسودة.");

            // 3️⃣ منع التكرار
            var alreadyExists = _context.HomeworkDraftQuestions
                .Any(x =>
                    x.HomeworkDraftId == draftId &&
                    x.QuestionId == newQuestionId);

            if (alreadyExists)
                throw new InvalidOperationException("هذا السؤال موجود بالفعل داخل المسودة.");

            var order = oldDraftQuestion.Order;

            // 4️⃣ حذف القديم
            _context.HomeworkDraftQuestions.Remove(oldDraftQuestion);
            _context.SaveChanges();

            // 5️⃣ إضافة الجديد
            _context.HomeworkDraftQuestions.Add(new HomeworkDraftQuestion
            {
                HomeworkDraftId = draftId,
                QuestionId = newQuestionId,
                Order = order
            });

            _context.SaveChanges();
        }


        public List<ReplaceCandidateQuestionVM> GetAddCandidates(
    int draftId,
    int lessonId)
        {
            var used = _context.HomeworkDraftQuestions
                .Where(x => x.HomeworkDraftId == draftId)
                .Select(x => x.QuestionId)
                .ToList();

            return _context.Questions
                .Where(q =>
                    q.LessonId == lessonId &&
                    q.IsComplete &&
                    q.IsAnswerConfirmed)
                .Select(q => new
                {
                    q.Id,
                    q.Title
                })
                .ToList()
                .Where(q => !used.Contains(q.Id))
                .Select(q => new ReplaceCandidateQuestionVM
                {
                    QuestionId = q.Id,
                    Title = q.Title
                })
                .ToList();
        }

        public void AddQuestion(int draftId, Guid questionId)
        {
            bool exists = _context.HomeworkDraftQuestions
                .Any(x =>
                    x.HomeworkDraftId == draftId &&
                    x.QuestionId == questionId);

            if (exists)
                throw new InvalidOperationException("هذا السؤال موجود بالفعل داخل المسودة.");

            var maxOrder = _context.HomeworkDraftQuestions
                .Where(x => x.HomeworkDraftId == draftId)
                .Select(x => (int?)x.Order)
                .Max() ?? 0;

            _context.HomeworkDraftQuestions.Add(new HomeworkDraftQuestion
            {
                HomeworkDraftId = draftId,
                QuestionId = questionId,
                Order = maxOrder + 1
            });

            _context.SaveChanges();
        }


        // ===============================
        // 👁️ مراجعة المسودة
        // ===============================
        public HomeworkDraftPreviewVM GetDraftForPreview(int draftId)
        {
            var draft = _context.HomeworkDrafts
                .Where(d => d.Id == draftId)
                .Select(d => new
                {
                    d.Id,
                    d.Title
                })
                .FirstOrDefault();

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة.");

            var questions = _context.HomeworkDraftQuestions
                .Where(q => q.HomeworkDraftId == draftId)
                .Select(q => new
                {
                    q.QuestionId,
                    QuestionTitle = q.Question.Title,
                    LessonId = q.Question.Lesson.Id,
                    LessonTitle = q.Question.Lesson.Title,
                    CurriculumId = q.Question.Lesson.Section.CurriculumId
                })
                .ToList();

            if (!questions.Any())
                throw new InvalidOperationException("لا توجد أسئلة داخل المسودة.");

            // اسم الدورة
            var curriculumId = questions.First().CurriculumId;

            var courseName = _context.CourseCurriculums
                .Where(cc => cc.CurriculumId == curriculumId)
                .Select(cc => cc.Course.Name)
                .FirstOrDefault() ?? string.Empty;

            var lessonGroups = questions
                .GroupBy(q => new { q.LessonId, q.LessonTitle })
                .Select(g => new HomeworkDraftLessonGroupVM
                {
                    LessonId = g.Key.LessonId,
                    LessonTitle = g.Key.LessonTitle,
                    Questions = g.Select(x => new HomeworkDraftQuestionItemVM
                    {
                        QuestionId = x.QuestionId,
                        Title = x.QuestionTitle
                    }).ToList()
                })
                .OrderBy(g => g.LessonTitle)
                .ToList();

            return new HomeworkDraftPreviewVM
            {
                DraftId = draft.Id,
                Title = draft.Title,
                CourseName = courseName,
                TotalQuestions = questions.Count,
                LessonGroups = lessonGroups
            };
        }
    }
}
