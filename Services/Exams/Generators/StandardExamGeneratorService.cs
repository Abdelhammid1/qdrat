using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Helpers;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Notifications;

namespace QdratNew.Services.Exams.Generators
{
    public class StandardExamGeneratorService : IStandardExamGeneratorService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ISystemSettingService _systemSettingService;
        private readonly IExamQuestionSelectorService _questionSelectorService;
        private readonly INotificationCenterService _notificationService;
        private readonly ITimeZoneService _timeZoneService;

public StandardExamGeneratorService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    ISystemSettingService systemSettingService,
    IExamQuestionSelectorService questionSelectorService,
    INotificationCenterService notificationService,
    ITimeZoneService timeZoneService)
{
            _contextFactory = contextFactory;
            _systemSettingService = systemSettingService;
    _questionSelectorService = questionSelectorService;
    _notificationService = notificationService;
    _timeZoneService = timeZoneService;
}


        public async Task<int> GenerateExamForCurriculumAsync(
            int curriculumId,
            int batchId,
            int sectionId,
            int? createdByInstructorId = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var existingAssignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.BatchId == batchId
                                       && a.CurriculumId == curriculumId
                                       && a.SectionId == sectionId);

            if (existingAssignment != null)
                return existingAssignment.Id;

            var curriculum = await _context.Curriculums.FindAsync(curriculumId);
            var section = await _context.Sections.FindAsync(sectionId);
            var batch = await _context.Batches.FindAsync(batchId);

            if (curriculum == null || section == null || batch == null)
                throw new Exception("❌ البيانات غير صحيحة لتوليد الاختبار");

            var totalQuestions = await _systemSettingService.GetIntAsync("CurriculumExamQuestionsCount", 40);
            int easy = totalQuestions / 3;
            int medium = totalQuestions / 3;
            int hard = totalQuestions - easy - medium;

            var exam = new Exam
            {
                Title = $"اختبار مؤشرات المنهج: {curriculum.Title}",
                Type = ExamType.PerformanceScale,
                CurriculumId = curriculumId,
                SectionId = sectionId,
                TotalQuestions = totalQuestions,
                EasyQuestionCount = easy,
                MediumQuestionCount = medium,
                HardQuestionCount = hard,
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = _timeZoneService.GetNowUtc()
            };
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            var assignment = new ExamAssignmentToBatch
            {
                ExamId = exam.Id,
                CurriculumId = curriculumId,
                SectionId = sectionId,
                BatchId = batchId,
                Title = exam.Title,
                TotalQuestions = totalQuestions,
                EasyQuestionCount = easy,
                MediumQuestionCount = medium,
                HardQuestionCount = hard,
                DurationMinutes = 30,
                CreatedAt = _timeZoneService.GetNowUtc(),
                IsOnline = true,
                IsInLab = false,
                RequireAttendanceBeforeExam = false,
                CreatedByInstructorId = createdByInstructorId
            };
            _context.ExamAssignmentsToBatches.Add(assignment);
            await _context.SaveChangesAsync();

            var questions = await _questionSelectorService.SelectQuestionsAsync(
                curriculumId,
                totalQuestions,
                easy,
                medium,
                hard,
                QuestionUsageType.PerformanceScale);

            int order = 1;
            foreach (var q in questions)
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = exam.Id,
                    QuestionId = q.Id,
                    Order = order++,
                    ExamAssignmentId = assignment.Id
                });
            }
            await _context.SaveChangesAsync();

            var students = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == batchId)
                .ToListAsync();

            foreach (var student in students)
            {
                if (!_context.ExamAssignments.Any(e => e.ExamId == exam.Id && e.StudentId == student.StudentID))
                {
                    _context.ExamAssignments.Add(new ExamAssignment
                    {
                        ExamId = exam.Id,
                        StudentId = student.StudentID,
                        AssignedAt = _timeZoneService.GetNowUtc(),
                        DueDate = assignment.ScheduledDate
                    });
                }

                if (!_context.ExamStudentStatuses.Any(s =>
                    s.ExamId == exam.Id &&
                    s.StudentId == student.StudentID &&
                    s.ExamAssignmentId == assignment.Id))
                {
                    _context.ExamStudentStatuses.Add(new ExamStudentStatus
                    {
                        StudentId = student.StudentID,
                        ExamId = exam.Id,
                        ExamAssignmentId = assignment.Id,
                        AssignedAt = _timeZoneService.GetNowUtc(),
                        Status = ExamStatus.Pending
                    });
                }

                await _notificationService.SendAsync(new Notification
                {
                    StudentID = student.StudentID,
                    Message = $"📘 تم إرسال اختبار جديد بعنوان {exam.Title}، الرجاء الدخول وحله.",
                    SentAt = _timeZoneService.GetNowUtc(),
                    Category = NotificationCategory.Exam,
                    TargetUrl = $"/Students/Exams/StartExam/{assignment.Id}"
                });
            }

            await _context.SaveChangesAsync();
            return assignment.Id;
        }
    }
}
