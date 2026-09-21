using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Notifications;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Exams.Generators
{
    public class PlacementExamGeneratorService : IPlacementExamGeneratorService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly INotificationCenterService _notificationService;
        private readonly ITimeZoneService _timeZoneService;

        public PlacementExamGeneratorService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
            INotificationCenterService notificationService,
            ITimeZoneService timeZoneService)
        {
            _contextFactory = contextFactory;
            _notificationService = notificationService;
            _timeZoneService = timeZoneService;
        }

        // ✅ توليد اختبار تحديد المستوى (ExamId)
        public async Task<int> GeneratePlacementTestReturnExamIdAsync(
            int studentId,
            int courseId,
            int totalQuestions,
            int durationMinutes,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId)
                ?? throw new Exception("❌ الطالب غير موجود");

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId)
                ?? throw new Exception("❌ الدورة غير موجودة");

            var nowUtc = _timeZoneService.GetNowUtc();

            // ✅ جلب المناهج المرتبطة بالدورة
            var curriculums = await (
                from c in _context.Curriculums
                join cc in _context.CourseCurriculums on c.Id equals cc.CurriculumId
                where cc.CourseId == course.Id
                select c
            ).ToListAsync();

            if (!curriculums.Any())
                throw new Exception("⚠️ لا توجد مناهج مرتبطة بهذه الدورة");

            int questionsPerCurriculum = Math.Max(1, totalQuestions / curriculums.Count);

            // 🧾 إنشاء سجل الامتحان
            var exam = new Exam
            {
                Title = $"اختبار تحديد المستوى ({course.Name})",
                Type = ExamType.LevelAssessment,
                TotalQuestions = totalQuestions,
                DurationMinutes = durationMinutes,
                IsActive = true,
                CreatedAt = nowUtc,
                CourseId = courseId // ✅ ضمان ربط الدورة
            };
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            int order = 1;
            var random = new Random();
            var addedQuestionIds = new HashSet<Guid>();
            var examQuestions = new List<ExamQuestion>();

            foreach (var curriculum in curriculums)
            {
                var sections = await _context.Sections
                    .Where(s => s.CurriculumId == curriculum.Id)
                    .AsNoTracking()
                    .ToListAsync();

                if (!sections.Any()) continue;

                int perSection = Math.Max(1, questionsPerCurriculum / sections.Count);

                foreach (var section in sections)
                {
                    var sectionQuestions = await (
                        from q in _context.Questions.AsNoTracking()
                        join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                        where l.SectionId == section.Id &&
                              q.IsReviewed &&
                              q.IsComplete &&
                              !q.IsRejected &&
                              q.CorrectAnswer != null
                        select q
                    ).ToListAsync();

                    var selected = sectionQuestions
                        .Where(q => !addedQuestionIds.Contains(q.Id))
                        .OrderBy(x => random.Next())
                        .Take(perSection)
                        .ToList();

                    foreach (var q in selected)
                    {
                        examQuestions.Add(new ExamQuestion
                        {
                            ExamId = exam.Id,
                            QuestionId = q.Id,
                            Order = order++
                        });
                        addedQuestionIds.Add(q.Id);
                    }
                }
            }

            // ✅ إدخال الأسئلة
            await _context.ExamQuestions.AddRangeAsync(examQuestions);
            await _context.SaveChangesAsync();

            // 🟡 في حال وجود عجز، أكمله من أسئلة الدورة بالكامل
            int currentCount = examQuestions.Count;
            if (currentCount < totalQuestions)
            {
                int deficit = totalQuestions - currentCount;

                var fallbackPool = await (
                    from q in _context.Questions.AsNoTracking()
                    join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                    join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                    join c in _context.Curriculums.AsNoTracking() on s.CurriculumId equals c.Id
                    join cc in _context.CourseCurriculums.AsNoTracking() on c.Id equals cc.CurriculumId
                    where cc.CourseId == courseId &&
                          q.IsReviewed &&
                          q.IsComplete &&
                          !q.IsRejected &&
                          q.CorrectAnswer != null
                    select q.Id
                ).ToListAsync();

                var additional = fallbackPool
                    .Where(id => !addedQuestionIds.Contains(id))
                    .OrderBy(x => random.Next())
                    .Take(deficit)
                    .ToList();

                foreach (var qid in additional)
                {
                    examQuestions.Add(new ExamQuestion
                    {
                        ExamId = exam.Id,
                        QuestionId = qid,
                        Order = order++
                    });
                    addedQuestionIds.Add(qid);
                }

                await _context.ExamQuestions.AddRangeAsync(
                    examQuestions.Where(eq => eq.Order > currentCount)
                );
                await _context.SaveChangesAsync();
            }

            // ❌ لا نربط الطالب هنا لتجنب التكرار
            // سيتم الربط في الدالة GeneratePlacementTestReturnAssignmentIdAsync

            return exam.Id;
        }

        // ✅ توليد AssignmentId (الوحيدة التي تربط الطالب)
        public async Task<int> GeneratePlacementTestReturnAssignmentIdAsync(
            int studentId,
            int courseId,
            int totalQuestions,
            int durationMinutes,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var now = _timeZoneService.GetNowSaudi();

            int examId = await GeneratePlacementTestReturnExamIdAsync(
                studentId, courseId, totalQuestions, durationMinutes, startDate, endDate);

            var assignment = await _context.ExamAssignments
                .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId);

            if (assignment == null)
            {
                var assignedAtUtc = startDate.HasValue ? _timeZoneService.ConvertToUtc(startDate.Value) : _timeZoneService.GetNowUtc();
                var dueDateUtc = endDate.HasValue ? _timeZoneService.ConvertToUtc(endDate.Value) : assignedAtUtc.AddMinutes(durationMinutes);

                assignment = new ExamAssignment
                {
                    ExamId = examId,
                    StudentId = studentId,
                    AssignedAt = assignedAtUtc,
                    DueDate = dueDateUtc
                };

                _context.ExamAssignments.Add(assignment);

                _context.ExamStudentStatuses.Add(new ExamStudentStatus
                {
                    StudentId = studentId,
                    ExamId = examId,
                    AssignedAt = assignedAtUtc,
                    Status = ExamStatus.Pending
                });

                await _context.SaveChangesAsync();

                // 🔔 إشعار الطالب
                var nowSaudi = _timeZoneService.GetNowSaudi();
                await _notificationService.SendAsync(new Notification
                {
                    StudentID = studentId,
                    Message = $"📘 تم تعيين اختبار تحديد المستوى الجديد، يبدأ في {_timeZoneService.ConvertToSaudi(assignedAtUtc):yyyy/MM/dd HH:mm} وينتهي في {_timeZoneService.ConvertToSaudi(dueDateUtc):yyyy/MM/dd HH:mm}.",
                    SentAt = nowSaudi,
                    Category = NotificationCategory.Exam,
                    TargetUrl = $"/Students/Exams/StartExam/{examId}"
                });
            }

            return assignment.Id;
        }

        // ✅ توليد اختبار تحديد المستوى بناءً على توزيع المحاور اليدوي
        public async Task<int> GeneratePlacementTestFromSectionsAsync(
            int studentId,
            int courseId,
            List<PlacementExamSectionSelectionVm> sections,
            int totalQuestions,
            int durationMinutes,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var nowUtc = _timeZoneService.GetNowUtc();

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId)
                ?? throw new Exception("❌ الطالب غير موجود");

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId)
                ?? throw new Exception("❌ الدورة غير موجودة");

            var exam = new Exam
            {
                Title = $"اختبار تحديد المستوى ({course.Name})",
                Type = ExamType.LevelAssessment,
                TotalQuestions = totalQuestions,
                DurationMinutes = durationMinutes,
                CreatedAt = nowUtc,
                IsActive = true,
                CourseId = courseId
            };
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            var questionIds = new List<Guid>();
            var random = new Random();

            // 🧩 اختيار الأسئلة من المحاور المحددة
            foreach (var section in sections)
            {
                int sectionQuestionCount = section.QuestionCount;
                if (sectionQuestionCount <= 0)
                    continue;

                // 🧩 جميع الأسئلة داخل هذا المحور
                var sectionQuestions = await (
                    from q in _context.Questions.AsNoTracking()
                    join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                    where l.SectionId == section.SectionId &&
                          q.IsReviewed && q.IsComplete &&
                          !q.IsRejected && q.CorrectAnswer != null
                    select q.Id
                ).ToListAsync();

                if (!sectionQuestions.Any())
                    continue;

                // 🎯 اختيار العدد المطلوب فقط من المحور بالكامل (بدون تقسيم على الدروس)
                var selected = sectionQuestions
                    .OrderBy(x => random.Next())
                    .Take(sectionQuestionCount)
                    .ToList();

                questionIds.AddRange(selected);

                // 🟡 في حالة عدم كفاية الأسئلة، أكمل من بنك أسئلة الدورة العامة
                if (selected.Count < sectionQuestionCount)
                {
                    int deficit = sectionQuestionCount - selected.Count;

                    var fallbackQuestions = await (
                        from q in _context.Questions.AsNoTracking()
                        join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                        join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                        join c in _context.Curriculums.AsNoTracking() on s.CurriculumId equals c.Id
                        join cc in _context.CourseCurriculums.AsNoTracking() on c.Id equals cc.CurriculumId
                        where cc.CourseId == courseId &&
                              q.IsReviewed && q.IsComplete &&
                              !q.IsRejected && q.CorrectAnswer != null
                        select q.Id
                    ).ToListAsync();

                    var additional = fallbackQuestions
                        .Where(id => !selected.Contains(id))
                        .OrderBy(x => random.Next())
                        .Take(deficit)
                        .ToList();

                    questionIds.AddRange(additional);
                }
            }






            // 🟡 لو في عجز -> اكمله من باقي المحاور في الدورة كلها
            // 🟡 لو في عجز -> اكمله من باقي المحاور في الدورة كلها
            if (questionIds.Count < totalQuestions)
            {
                int deficit = totalQuestions - questionIds.Count;

                var fallbackPool = await (
                    from q in _context.Questions
                    join l in _context.Lessons on q.LessonId equals l.Id
                    join s in _context.Sections on l.SectionId equals s.Id
                    join c in _context.Curriculums on s.CurriculumId equals c.Id
                    join cc in _context.CourseCurriculums on c.Id equals cc.CurriculumId
                    where cc.CourseId == courseId &&
                          q.IsReviewed && q.IsComplete && !q.IsRejected &&
                          q.CorrectAnswer != null
                    select q.Id
                ).ToListAsync();

                // 🟢 استبدال Contains بـ Join محلي
                var remainingPool = (
                    from q in fallbackPool
                    join sel in questionIds on q equals sel into gj
                    from sub in gj.DefaultIfEmpty()
                    where sub == Guid.Empty // لم يُختَر بعد
                    select q
                ).ToList();

                var randomFill = remainingPool
                    .OrderBy(x => random.Next())
                    .Take(deficit)
                    .ToList();

                questionIds.AddRange(randomFill);
            }


            // ✅ إدخال الأسئلة
            var examQuestions = questionIds.Select((qid, index) => new ExamQuestion
            {
                ExamId = exam.Id,
                QuestionId = qid,
                Order = index + 1
            }).ToList();

            _context.ExamQuestions.AddRange(examQuestions);
            await _context.SaveChangesAsync();

            // ✅ ربط الطالب
            var assignedAtUtc = startDate.HasValue ? _timeZoneService.ConvertToUtc(startDate.Value) : nowUtc;
            var dueDateUtc = endDate.HasValue ? _timeZoneService.ConvertToUtc(endDate.Value) : nowUtc.AddMinutes(durationMinutes);

            _context.ExamAssignments.Add(new ExamAssignment
            {
                ExamId = exam.Id,
                StudentId = studentId,
                AssignedAt = assignedAtUtc,
                DueDate = dueDateUtc
            });

            _context.ExamStudentStatuses.Add(new ExamStudentStatus
            {
                StudentId = studentId,
                ExamId = exam.Id,
                AssignedAt = assignedAtUtc,
                Status = ExamStatus.Pending
            });

            await _context.SaveChangesAsync();

            var nowSaudi = _timeZoneService.GetNowSaudi();
            await _notificationService.SendAsync(new Notification
            {
                StudentID = studentId,
                Message = $"📘 تم تعيين اختبار تحديد المستوى ({exam.Title})، يبدأ في {_timeZoneService.ConvertToSaudi(assignedAtUtc):yyyy/MM/dd HH:mm} وينتهي في {_timeZoneService.ConvertToSaudi(dueDateUtc):yyyy/MM/dd HH:mm}.",
                SentAt = nowSaudi,
                Category = NotificationCategory.Exam,
                TargetUrl = $"/Students/Exams/StartExam/{exam.Id}"
            });

            return exam.Id;
        }
    }
}
