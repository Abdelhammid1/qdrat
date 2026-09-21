using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class ParentSmartPracticeService : IParentSmartPracticeService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IParentAccessService _accessService;
        private readonly IStudentWeaknessAnalyzerService _weaknessAnalyzer;
        private readonly IAdaptiveQuestionPickerService _questionPicker;

        public ParentSmartPracticeService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IParentAccessService accessService,
            IStudentWeaknessAnalyzerService weaknessAnalyzer,
            IAdaptiveQuestionPickerService questionPicker)
        {
            _contextFactory = contextFactory;
            _accessService = accessService;
            _weaknessAnalyzer = weaknessAnalyzer;
            _questionPicker = questionPicker;
        }

        public async Task<bool> CanCreatePracticeTodayAsync(int parentId, int studentId)
        {
            using var db = _contextFactory.CreateDbContext();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // حد 2 طلبات يوميًا
            int todayCount = await db.Set<ParentSmartPracticeRequest>()
                .AsNoTracking()
                .Where(r => r.ParentId == parentId &&
                            r.StudentId == studentId &&
                            r.CreatedAt >= today && r.CreatedAt < tomorrow)
                .CountAsync();

            if (todayCount >= 2) return false;

            // هل لديه اختبار رسمي اليوم؟
            bool hasExamToday = await db.ExamAssignmentsToStudents
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId &&
                               e.ScheduledDate >= today && e.ScheduledDate < tomorrow);

            if (hasExamToday) return false;

            return true;
        }

        public async Task<ParentSmartPracticeCreateViewModel> BuildCreateModelAsync(string parentUserId, int? studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var parentId = await _accessService.GetCurrentParentIdAsync(parentUserId);
            if (parentId == null)
                return new ParentSmartPracticeCreateViewModel { CanCreate = false, BlockingReason = "لم يتم التعرف على ولي الأمر." };

            var students = await db.Students
                .AsNoTracking()
                .Where(s => s.ParentId == parentId)
                .Select(s => new ParentChildCardViewModel { StudentId = s.StudentID, StudentName = s.FullName })
                .ToListAsync();

            int activeStudentId = studentId ?? students.FirstOrDefault()?.StudentId ?? 0;

            if (activeStudentId == 0)
                return new ParentSmartPracticeCreateViewModel
                {
                    CanCreate = false,
                    BlockingReason = "لا يوجد طلاب مرتبطون بحسابك.",
                    AvailableStudents = students
                };

            bool canCreate = await CanCreatePracticeTodayAsync(parentId.Value, activeStudentId);

            // التحقق من الواجبات المتأخرة
            string? blockingReason = null;
            if (!canCreate)
            {
                blockingReason = "لا ننصح بإضافة اختبار جديد اليوم. الطالب لديه مهام تعليمية كافية.";
            }
            else
            {
                var batchIds = await db.StudentBatchEnrollments
                    .AsNoTracking()
                    .Where(e => e.StudentID == activeStudentId)
                    .Select(e => e.BatchId)
                    .ToListAsync();

                var allSets = await db.HomeworkSets.AsNoTracking().ToListAsync();
                var relevantSets = allSets.Where(hs => batchIds.Contains(hs.BatchId)).Select(s => s.Id).ToList();

                bool hasLate = false;
                if (relevantSets.Count > 0)
                {
                    var submitted = await db.HomeworkSetStudents
                        .AsNoTracking()
                        .Where(x => x.StudentId == activeStudentId && x.IsSubmitted)
                        .Select(x => x.HomeworkSetId)
                        .ToListAsync();
                    var submittedSet = new System.Collections.Generic.HashSet<int>(submitted);

                    hasLate = allSets
                        .Where(s => batchIds.Contains(s.BatchId))
                        .Any(s => !submittedSet.Contains(s.Id) &&
                                  s.EndAt.HasValue && s.EndAt.Value < DateTime.Now);
                }

                if (hasLate)
                {
                    canCreate = false;
                    blockingReason = "الأفضل اليوم متابعة الواجبات المتأخرة قبل إنشاء تدريب جديد.";
                }
            }

            var curriculums = await db.Curriculums
                .AsNoTracking()
                .Select(c => new CurriculumSelectItem { Id = c.Id, Title = c.Title })
                .ToListAsync();

            var analysis = await _weaknessAnalyzer.AnalyzeAsync(activeStudentId, null, null);

            return new ParentSmartPracticeCreateViewModel
            {
                StudentId = activeStudentId,
                AvailableStudents = students,
                AvailableCurriculums = curriculums,
                CanCreate = canCreate,
                BlockingReason = blockingReason,
                StudentInsightSummary = analysis.SafeWeaknessSummary,
                QuestionCount = 10,
                DurationMinutes = 15
            };
        }

        public async Task<ParentSmartPracticeResultViewModel> CreateSmartPracticeAsync(
            string parentUserId,
            ParentSmartPracticeCreateViewModel model)
        {
            using var db = _contextFactory.CreateDbContext();

            var parentId = await _accessService.GetCurrentParentIdAsync(parentUserId);
            if (parentId == null)
                return Fail("لم يتم التعرف على ولي الأمر.");

            bool canAccess = await _accessService.CanAccessStudentAsync(parentUserId, model.StudentId);
            if (!canAccess)
                return Fail("لا يمكنك الوصول لهذا الطالب.");

            bool canCreate = await CanCreatePracticeTodayAsync(parentId.Value, model.StudentId);
            if (!canCreate)
                return Fail("لا ننصح بإضافة اختبار جديد اليوم.");

            // تحليل نقاط الضعف
            var analysis = await _weaknessAnalyzer.AnalyzeAsync(model.StudentId, model.CurriculumId, model.SectionId);

            // اختيار الأسئلة
            var pickRequest = new AdaptiveQuestionPickRequest
            {
                StudentId = model.StudentId,
                CurriculumId = model.CurriculumId,
                SectionId = model.SectionId,
                QuestionCount = Math.Min(model.QuestionCount, 15),
                PracticeMode = model.PracticeMode,
                StudentLevel = analysis.StudentLevel,
                WeakSectionIds = analysis.WeakSections.Select(w => w.SectionId).ToList()
            };

            var questionIds = await _questionPicker.PickQuestionsAsync(pickRequest);
            if (!questionIds.Any())
                return Fail("لم يتم العثور على أسئلة مناسبة لهذا الطلب.");

            // إنشاء الاختبار
            var exam = new Exam
            {
                Title = $"تدريب ذكي — {GetModeLabel(model.PracticeMode)}",
                Type = ExamType.Manual,
                CurriculumId = model.CurriculumId > 0 ? (int?)model.CurriculumId : null,
                SectionId = model.SectionId,
                TotalQuestions = questionIds.Count,
                DurationMinutes = model.DurationMinutes,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            // تعيين الاختبار للطالب (صالح 7 أيام)
            var assignment = new ExamAssignmentToStudent
            {
                ExamId = exam.Id,
                StudentId = model.StudentId,
                ScheduledDate = DateTime.Now,
                EndAt = DateTime.Now.AddDays(7),
                DurationMinutes = model.DurationMinutes,
                QuestionCount = questionIds.Count,
                SourceType = "ParentRequest",
                IsSent = true,
                CreatedAt = DateTime.UtcNow
            };
            db.ExamAssignmentsToStudents.Add(assignment);
            await db.SaveChangesAsync();

            // ربط الأسئلة بالاختبار وبالتعيين الفردي
            int order = 1;
            foreach (var qId in questionIds)
            {
                db.ExamQuestions.Add(new ExamQuestion
                {
                    QuestionId = qId,
                    ExamId = exam.Id,
                    ExamAssignmentToStudentId = assignment.Id,
                    Order = order++
                });
            }
            await db.SaveChangesAsync();

            // إنشاء طلب التدريب
            var request = new ParentSmartPracticeRequest
            {
                ParentId = parentId.Value,
                StudentId = model.StudentId,
                CurriculumId = model.CurriculumId,
                SectionId = model.SectionId,
                RequestedQuestionCount = questionIds.Count,
                RequestedDurationMinutes = model.DurationMinutes,
                PracticeMode = model.PracticeMode,
                Status = "Active",
                CreatedAt = DateTime.Now,
                SafeSummary = analysis.SafeWeaknessSummary,
                RecommendationSnapshotJson = System.Text.Json.JsonSerializer.Serialize(analysis),
                GeneratedExamAssignmentToStudentId = assignment.Id
            };

            db.Set<ParentSmartPracticeRequest>().Add(request);
            await db.SaveChangesAsync();

            // تسجيل إجراء ولي الأمر
            var log = new ParentActionLog
            {
                ParentId = parentId.Value,
                StudentId = model.StudentId,
                ActionType = "SmartPracticeRequest",
                Description = $"طلب تدريب ذكي ({model.PracticeMode}) - {questionIds.Count} سؤال",
                CreatedAt = DateTime.Now
            };
            db.Set<ParentActionLog>().Add(log);
            await db.SaveChangesAsync();

            return new ParentSmartPracticeResultViewModel
            {
                RequestId = request.Id,
                Success = true,
                Message = "تم إرسال التدريب بنجاح. سيظهر في قائمة اختبارات ابنك الآن.",
                SafeSummary = analysis.SafeWeaknessSummary ?? string.Empty,
                RedirectUrl = $"/Parents/SmartPractice/Details/{request.Id}"
            };
        }

        public async Task<ParentSmartPracticeDetailsViewModel?> GetDetailsAsync(int requestId, int parentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var req = await db.Set<ParentSmartPracticeRequest>()
                .AsNoTracking()
                .Where(r => r.Id == requestId && r.ParentId == parentId)
                .Select(r => new
                {
                    r.Id, r.StudentId, r.PracticeMode, r.Status,
                    r.RequestedQuestionCount, r.RequestedDurationMinutes,
                    r.CreatedAt, r.SafeSummary,
                    r.GeneratedExamAssignmentToStudentId
                })
                .FirstOrDefaultAsync();

            if (req == null) return null;

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == req.StudentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            var vm = new ParentSmartPracticeDetailsViewModel
            {
                RequestId = req.Id,
                StudentName = studentName,
                PracticeMode = req.PracticeMode,
                PracticeModeLabel = GetModeLabel(req.PracticeMode),
                Status = req.Status,
                StatusLabel = GetStatusLabel(req.Status),
                StatusColor = req.Status == "Active" ? "success" : "secondary",
                QuestionCount = req.RequestedQuestionCount,
                DurationMinutes = req.RequestedDurationMinutes,
                CreatedAt = req.CreatedAt,
                SafeSummary = req.SafeSummary ?? string.Empty
            };

            // جلب نتيجة الطالب إن وجدت
            if (req.GeneratedExamAssignmentToStudentId.HasValue)
            {
                var examStatus = await db.ExamStudentStatuses
                    .AsNoTracking()
                    .Where(s => s.ExamAssignmentToStudentId == req.GeneratedExamAssignmentToStudentId
                             && s.StudentId == req.StudentId)
                    .OrderByDescending(s => s.SubmittedAt)
                    .FirstOrDefaultAsync();

                if (examStatus != null)
                {
                    if (examStatus.IsSubmitted)
                    {
                        vm.Status = "Completed";
                        vm.StatusLabel = "مكتمل";
                        vm.StatusColor = "success";
                        vm.HasResult = true;
                        vm.ResultScore = examStatus.Score.HasValue
                            ? Math.Round((double)examStatus.Score.Value, 1)
                            : null;
                        vm.SafeResultSummary = vm.ResultScore.HasValue
                            ? vm.ResultScore.Value >= 75 ? "أداء ممتاز، استمر!"
                            : vm.ResultScore.Value >= 50 ? "جيد، لكن هناك مجال للتحسين."
                            : "يحتاج مراجعة إضافية لتعزيز الفهم."
                            : null;
                    }
                    else if (examStatus.StartedAt.HasValue)
                    {
                        vm.Status = "InProgress";
                        vm.StatusLabel = "قيد التنفيذ";
                        vm.StatusColor = "warning";
                    }
                }
            }

            return vm;
        }

        private static string GetModeLabel(string mode) => mode switch
        {
            "WeaknessBased" => "تعزيز نقاط الضعف",
            "Review" => "مراجعة عامة",
            "Challenge" => "تحدٍّ",
            "ExamPreparation" => "استعداد لاختبار",
            "QuickPractice" => "تدريب سريع",
            _ => mode
        };

        private static string GetStatusLabel(string status) => status switch
        {
            "Active" => "نشط",
            "Pending" => "قيد المعالجة",
            "Completed" => "مكتمل",
            _ => status
        };

        private static ParentSmartPracticeResultViewModel Fail(string message) =>
            new() { Success = false, Message = message, SafeSummary = string.Empty, RedirectUrl = string.Empty };
    }
}
