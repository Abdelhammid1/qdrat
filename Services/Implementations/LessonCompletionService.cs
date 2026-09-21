using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class LessonCompletionService : ILessonCompletionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISectionExamGeneratorService _examGenerator;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISystemSettingService _systemSettingService;

        public LessonCompletionService(
            ApplicationDbContext context,
            ISectionExamGeneratorService examGenerator,
            IHttpContextAccessor httpContextAccessor,
            ISystemSettingService systemSettingService)
        {
            _context = context;
            _examGenerator = examGenerator;
            _httpContextAccessor = httpContextAccessor;
            _systemSettingService = systemSettingService;
        }

        // ✅ تسجيل المؤشرات المكتملة
        public async Task<bool> RegisterCompletedLessonsAsync(LessonCompletionInputDto dto)
        {
            if (dto == null || dto.LessonIds == null || !dto.LessonIds.Any())
                return false;

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return false;

            foreach (var lessonId in dto.LessonIds)
            {
                var alreadyExists = await _context.BatchLessonCompletions.AnyAsync(x =>
                    x.BatchId == dto.BatchId &&
                    x.LessonId == lessonId &&
                    x.LectureId == dto.LectureId);

                if (!alreadyExists)
                {
                    _context.BatchLessonCompletions.Add(new BatchLessonCompletion
                    {
                        BatchId = dto.BatchId,
                        SectionId = dto.SectionId,
                        LectureId = dto.LectureId,
                        LessonId = lessonId,
                        CompletionTitle = dto.CompletionTitle,
                        CompletionDate = DateTime.Now,
                        LastCompletedAt = DateTime.Now,
                        AddedBy = userId
                    });
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new Exception("فشل في حفظ البيانات: " + (ex.InnerException?.Message ?? ex.Message));
            }

            return true;
        }

        // ✅ جلب المحاور المكتملة للدفعة
        public async Task<List<CompletedSectionSummaryViewModel>> GetCompletedSectionsForBatchAsync(int batchId)
        {
            var sectionIds = await _context.BatchLessonCompletions
                .Where(x => x.BatchId == batchId)
                .Select(x => x.SectionId)
                .Distinct()
                .ToListAsync();

            var result = new List<CompletedSectionSummaryViewModel>();

            foreach (var sectionId in sectionIds)
            {
                var section = await _context.Sections.FindAsync(sectionId);
                var completedCount = await _context.BatchLessonCompletions
                    .CountAsync(x => x.BatchId == batchId && x.SectionId == sectionId);

                var lastLecture = await _context.Lecture
                    .Where(l => l.BatchId == batchId && l.SectionId == sectionId)
                    .OrderByDescending(l => l.Date)
                    .FirstOrDefaultAsync();

                result.Add(new CompletedSectionSummaryViewModel
                {
                    SectionId = sectionId,
                    SectionTitle = section?.Title ?? "-",
                    CompletedLessonsCount = completedCount,
                    LastLectureTitle = lastLecture?.Title,
                    LastLectureDate = lastLecture?.Date
                });
            }

            return result;
        }

        // ✅ إعداد صفحة تأكيد إرسال الواجب
        public async Task<ConfirmHomeworkViewModel> PrepareConfirmHomeworkViewModelAsync(int batchId, int? lectureId = null)
        {
            var batch = await _context.Batches.FindAsync(batchId);
            if (batch == null) return null;

            var completionsQuery = _context.BatchLessonCompletions
                .Include(x => x.Section)
                .Where(x => x.BatchId == batchId);

            if (lectureId.HasValue)
                completionsQuery = completionsQuery.Where(x => x.LectureId == lectureId.Value);

            var completions = await completionsQuery
                .OrderByDescending(x => x.CompletionDate)
                .ToListAsync();

            if (!completions.Any())
                return null;

            var rawQuestions = await (
                from l in _context.Lessons
                join q in _context.Questions on l.Id equals q.LessonId
                join blc in _context.BatchLessonCompletions on l.Id equals blc.LessonId
                where blc.BatchId == batchId
                                      && (!lectureId.HasValue || blc.LectureId == lectureId.Value)
                                      && (l.IsActive || l.IsActive == false) // ✅ السماح بجلب الدروس حتى لو IsActive غير محدد
                                      && q.IsReviewed
                                      && q.IsComplete
                                      && !q.IsRejected
                                      && !string.IsNullOrWhiteSpace(q.CorrectAnswer)
                                      && l.SectionId > 0                     // ✅ تأكد أن الدرس مرتبط بمحور فعلاً

                select new
                {
                    q.LessonId,
                    LessonTitle = l.Title,
                    SectionId = l.SectionId
                }).ToListAsync();

            var defaultQuestions = await _systemSettingService.GetIntAsync("DefaultHomeworkQuestionsPerLesson", 3);

            var grouped = rawQuestions
                .GroupBy(q => new { q.LessonId, q.LessonTitle, q.SectionId })
                .Select(g => new LessonSummaryViewModel
                {
                    LessonId = g.Key.LessonId,
                    LessonTitle = g.Key.LessonTitle,
                    SectionId = g.Key.SectionId,
                    TotalQuestions = g.Count(),
                    ReviewedQuestions = g.Count(),
                    QuestionsToUse = g.Count() >= defaultQuestions ? defaultQuestions : g.Count()
                }).ToList();

            return new ConfirmHomeworkViewModel
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CurriculumId = completions.FirstOrDefault()?.Section?.CurriculumId ?? 0,
                CompletionTitle = completions.FirstOrDefault()?.CompletionTitle,
                Lessons = grouped
            };
        }

        // ✅ توليد الواجبات وإرسالها للطلاب
        public async Task<bool> GenerateHomeworksAsync(ConfirmHomeworkViewModel vm, bool forceGenerateAnyway)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return false;

            var students = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == vm.BatchId)
                .ToListAsync();

            if (!students.Any())
                throw new Exception("❌ لا توجد طلاب في هذه الدفعة.");

            // 🟢 إنشاء الهيدر الأساسي للواجب
            var homeworkSet = new HomeworkSet
            {
                BatchId = vm.BatchId,
                CurriculumId = vm.CurriculumId,
                Title = "واجب مؤشرات",
                AssignedByUserId = userId,
                CreatedAt = DateTime.Now,
                CompletionTitle = vm.CompletionTitle,
                StartAt = vm.StartAt ?? DateTime.Now,
                EndAt = vm.EndAt ?? DateTime.Now.AddDays(3),
                IsSent = false
            };

            _context.HomeworkSets.Add(homeworkSet);
            await _context.SaveChangesAsync();

            // 🟢 ربط الواجب بالمحاور (فقط الدروس المفعّلة)
            var hsSections = vm.Lessons
         .Where(l => l.SectionId != 0 && l.SectionId != null)
         .GroupBy(l => l.SectionId)
         .Select(g => new HomeworkSetSection
         {
             HomeworkSetId = homeworkSet.Id,
             SectionId = g.Key
         })
         .ToList();

            if (!hsSections.Any())
                throw new Exception("❌ لا توجد دروس مرتبطة بمحاور صالحة لإرسال الواجب.");


            if (!hsSections.Any())
                throw new Exception("❌ لا توجد دروس مفعّلة صالحة لإرسال الواجب.");

            _context.HomeworkSetSections.AddRange(hsSections);
            await _context.SaveChangesAsync();

            var allHomeworks = new List<QdratNew.Entities.Homework>();

            foreach (var lesson in vm.Lessons.Where(l => l.SectionId > 0))
            {
                var questions = await (
                    from q in _context.Questions
                    join l in _context.Lessons on q.LessonId equals l.Id
                    where q.LessonId == lesson.LessonId
                          && q.IsReviewed
                          && q.IsComplete
                          && !q.IsRejected
                          && !string.IsNullOrWhiteSpace(q.CorrectAnswer)
                          && l.IsActive
                          && !_context.Homeworks.Any(h => h.QuestionId == q.Id && h.HomeworkSet.BatchId == vm.BatchId)
                    select q
                )
                .OrderBy(r => Guid.NewGuid())
                .Take(lesson.QuestionsToUse)
                .ToListAsync();

                if (!questions.Any())
                    continue;

                if (questions.Count < lesson.QuestionsToUse && !forceGenerateAnyway)
                    continue;

                foreach (var student in students)
                {
                    foreach (var q in questions)
                    {
                        allHomeworks.Add(new QdratNew.Entities.Homework
                        {
                            StudentId = student.StudentID,
                            QuestionId = q.Id,
                            HomeworkSetId = homeworkSet.Id,
                            LessonId = lesson.LessonId,
                            CreatedAt = DateTime.Now,
                            AssignedAt = DateTime.Now,
                            IsSent = true,
                            Status = HomeworkStatus.Pending
                        });
                    }
                }
            }

            if (!allHomeworks.Any())
                throw new Exception("❌ لا يمكن إرسال الواجب، لا توجد أسئلة صالحة بعد الفلترة.");

            _context.Homeworks.AddRange(allHomeworks);
            homeworkSet.IsSent = true;
            await _context.SaveChangesAsync();

            return true;
        }

        // ✅ توليد واجب إضافي لمجموعة من المحاور والطلاب
        public async Task<bool> GenerateHomeworksForExtraSetAsync(int homeworkSetId, List<int> sectionIds, List<int> studentIds, int questionsPerStudent)
        {
            // 🟦 تحقق من وجود الواجب
            var set = await _context.HomeworkSets.FirstOrDefaultAsync(h => h.Id == homeworkSetId);
            if (set == null)
                throw new Exception("❌ لم يتم العثور على الواجب المحدد.");

            // 🟩 جلب جميع الدروس أولاً (تجنب Contains داخل SQL)
            var allLessons = await _context.Lessons
                .AsNoTracking()
                .ToListAsync();

            // 🟩 فلترة الدروس داخل الذاكرة بناءً على المحاور المحددة
            var lessons = allLessons
                .Where(l => sectionIds.Contains(l.SectionId))
                .ToList();

            if (!lessons.Any())
                throw new Exception("⚠️ لا توجد دروس مرتبطة بالمحاور المحددة.");

            // 🟨 جلب جميع الأسئلة الصالحة من قاعدة البيانات (فقط)
            var allQuestions = await _context.Questions
                .AsNoTracking()
                .Where(q =>
                    q.IsReviewed &&
                    q.IsComplete &&
                    !q.IsRejected &&
                    !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .ToListAsync();

            // 🟨 فلترة الأسئلة داخل الذاكرة بناءً على الدروس التي تم استخراجها
            var questions = allQuestions
                .Where(q => lessons.Any(l => l.Id == q.LessonId))
                .ToList();

            if (!questions.Any())
                throw new Exception("⚠️ لا توجد أسئلة صالحة مرتبطة بالمحاور المحددة.");

            // 🕒 حفظ الوقت الحالي مرة واحدة
            var now = DateTime.UtcNow;
            var allHomeworks = new List<QdratNew.Entities.Homework>();

            // 🧠 توزيع الأسئلة على الطلاب
            foreach (var studentId in studentIds)
            {
                // تخطى الطالب إذا كان الواجب مرسل له بالفعل
                var alreadyHasHomework = await _context.Homeworks
                    .AnyAsync(h => h.HomeworkSetId == homeworkSetId && h.StudentId == studentId);

                if (alreadyHasHomework)
                    continue;

                // اختيار عشوائي من الأسئلة
                var selectedQuestions = questions
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(questionsPerStudent)
                    .ToList();

                foreach (var q in selectedQuestions)
                {
                    allHomeworks.Add(new QdratNew.Entities.Homework
                    {
                        StudentId = studentId,
                        QuestionId = q.Id,
                        LessonId = q.LessonId,
                        HomeworkSetId = homeworkSetId,
                        AssignedAt = now,
                        IsCompleted = false,
                        Status = HomeworkStatus.Pending,
                        IsSent = true
                    });
                }
            }

            if (!allHomeworks.Any())
                throw new Exception("⚠️ لم يتم توليد أي واجبات جديدة، ربما جميع الطلاب لديهم الواجب بالفعل.");

            // 💾 حفظ النتائج في قاعدة البيانات
            _context.Homeworks.AddRange(allHomeworks);
            set.IsSent = true;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
