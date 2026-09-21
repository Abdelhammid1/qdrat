using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Instructor.Exam;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Instructors.Exams
{
    public class InstructorExamMonitoringService : IInstructorExamMonitoringService
    {
        private readonly ApplicationDbContext _context;

        public InstructorExamMonitoringService(ApplicationDbContext context)
        {
            _context = context;
        }




        public async Task<HomeworkReviewViewModel> GetExamStudentReviewAsync(
    int examAssignmentId,
    int studentId,
    int instructorId)
        {
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Include(a => a.Question.VerbalPassage)
                .Where(a =>
                    a.StudentId == studentId &&
                    a.ExamAssignmentId == examAssignmentId)
                .AsNoTracking()
                .ToListAsync();

            if (!attempts.Any())
                return new HomeworkReviewViewModel();

            var grouped = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            return new HomeworkReviewViewModel
            {
                HomeworkSetId = examAssignmentId,
                TotalQuestions = grouped.Count,
                CorrectAnswers = grouped.Count(x => x.IsCorrect),
                WrongAnswers = grouped.Count(x => !x.IsCorrect),

                Questions = grouped.Select(x => new HomeworkQuestionReviewItems
                {
                    QuestionId = x.QuestionId,
                    QuestionText = x.Question.Title,
                    StudentAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.Question.CorrectAnswer,
                    IsCorrect = x.IsCorrect,
                    TimeTakenSeconds = x.TimeTakenSeconds,

                    ImageUrl = x.Question.ImageUrl,
                    IsQuantitative = x.Question.IsQuantitative,

                    ComparisonValue1 = x.Question.ValueA,
                    ComparisonValue2 = x.Question.ValueB,

                    VerbalPassageTitle = x.Question.VerbalPassage?.Title,
                    VerbalPassageContent = x.Question.VerbalPassage?.Content,

                    DisplayType = x.Question.Template switch
                    {
                        QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                        _ => QuestionDisplayType.WithImage
                    },

                    Options = x.Question.Options.Select(o => new HomeworkOptionReviewItem
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == x.Question.CorrectAnswer,
                        IsSelectedByStudent = o.Text == x.SelectedAnswer
                    }).ToList()

                }).ToList()
            };
        }


        public async Task<HomeworkAnalyticsViewModel> GetExamStudentReportAsync(
            int examAssignmentId,
            int studentId,
            int instructorId)
        {
            // =========================
            // Student Info
            // =========================
            var studentInfo = await _context.Students
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    s.FullName,
                    BatchName = s.BatchEnrollments
                        .Select(e => e.Batch.Name)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (studentInfo == null)
                return null;

            // =========================
            // Attempts
            // =========================
            var attempts = await _context.QuestionAttemptNew
                .Where(x =>
                    x.StudentId == studentId &&
                    x.ExamAssignmentId == examAssignmentId)
                .ToListAsync();

            if (!attempts.Any())
                return new HomeworkAnalyticsViewModel();

            // =========================
            // Analytics
            // =========================
            var grouped = attempts
                .GroupBy(x => x.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            var total = grouped.Count;
            var correct = grouped.Count(x => x.IsCorrect);
            var wrong = total - correct;
            var skipped = total - grouped.Count(x => x.SelectedAnswer != null);

            var avgTime = grouped
                .Where(x => x.TimeTakenSeconds > 0)
                .Select(x => x.TimeTakenSeconds)
                .DefaultIfEmpty(0)
                .Average();

            var score = total == 0 ? 0 : (correct * 100.0 / total);

            // =========================
            // Behavior
            // =========================
            StudentBehaviorLevel behaviorLevel;
            string behaviorText;

            if (avgTime < 5 && correct >= total * 0.8)
            {
                behaviorLevel = StudentBehaviorLevel.غش_محتمل;
                behaviorText = "يوجد احتمال غش بسبب السرعة العالية مع دقة مرتفعة.";
            }
            else if (avgTime < 5)
            {
                behaviorLevel = StudentBehaviorLevel.مريب;
                behaviorText = "سرعة الإجابة غير طبيعية وتحتاج مراجعة.";
            }
            else if (correct >= total * 0.7)
            {
                behaviorLevel = StudentBehaviorLevel.ممتاز;
                behaviorText = "أداء الطالب ممتاز ويظهر فهمًا قويًا.";
            }
            else
            {
                behaviorLevel = StudentBehaviorLevel.طبيعي;
                behaviorText = "أداء الطالب طبيعي ويحتاج إلى بعض التحسين.";
            }

            // =========================
            // Parent Report
            // =========================
            var parentReport = new ParentReportResult
            {
                Summary = $"أداء الطالب {behaviorText}",
                Recommendation = "نوصي بالاستمرار في التدريب والتركيز على نقاط الضعف.",
                HomeRecommendations = new List<string>
        {
            "الاستمرار في التدريب اليومي",
            "مراجعة الأخطاء السابقة",
            "حل اختبارات إضافية"
        }
            };

            // =========================
            // 🔥 Section Performance (للشارت)
            // =========================
            var sectionData = grouped
                .GroupBy(x => x.SectionId)
                .Select(g => new
                {
                    SectionId = g.Key,
                    Correct = g.Count(x => x.IsCorrect),
                    Total = g.Count()
                })
                .ToList();

            var sectionLabels = sectionData
                .Select(x => $"محور {x.SectionId}")
                .ToList();

            var sectionScores = sectionData
                .Select(x => x.Total == 0 ? 0 : (x.Correct * 100.0 / x.Total))
                .ToList();

            // =========================
            // 🔥 Progress Data (للشارت)
            // =========================
            var progressData = await _context.QuestionAttemptNew
                .Where(x => x.StudentId == studentId && x.ExamAssignmentId != null)
                .OrderBy(x => x.AttemptedAt)
                .GroupBy(x => x.ExamAssignmentId)
                .Select(g => g.Count(x => x.IsCorrect))
                .ToListAsync();


            // =========================
            // Sections Performance
            // =========================
            var sections = await _context.Sections
                .AsNoTracking()
                .ToListAsync();

            var sectionPerformance = grouped
       .Where(x => x.SectionId != null)
       .GroupBy(x => x.SectionId)
       .Select(g =>
       {
           var section = sections.FirstOrDefault(s => s.Id == g.Key);

           var totalQ = g.Count();
           var correctQ = g.Count(x => x.IsCorrect);

           return new SectionPerformanceVm
           {
               SectionId = g.Key ?? 0,
               SectionTitle = section != null ? section.Title : "غير معروف",
               Accuracy = totalQ == 0 ? 0 : Math.Round(correctQ * 100.0 / totalQ, 1)
           };
       })
       .ToList();


            // =========================
            // Return
            // =========================
            return new HomeworkAnalyticsViewModel
            {
                HomeworkSetId = examAssignmentId,

                StudentName = studentInfo.FullName,
                BatchName = studentInfo.BatchName,

                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedAnswers = skipped,

                ScorePercentage = score,
                AvgTimePerQuestion = avgTime,
                SectionsPerformance = sectionPerformance,
                BehaviorLevel = behaviorLevel,
                BehaviorAnalysisText = behaviorText,

                ParentReport = parentReport,

                // 🔥 بيانات الشارت
                SectionLabels = sectionLabels,
                SectionScores = sectionScores,
                ProgressScores = progressData
            };
        }
        // ============================
        // 👨‍🎓 Students
        // ============================

        public async Task<PartnerExamStudentsVM> GetExamStudentsAsync(int examAssignmentId, int instructorId)
        {
            // =========================
            // 1️⃣ تحميل الاختبار
            // =========================
            var assignment = await _context.ExamAssignmentsToBatches
                .Where(x => x.Id == examAssignmentId)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.BatchId,
                    BatchName = x.Batch.Name
                })
                .FirstOrDefaultAsync();

            if (assignment == null)
                return null;

            // =========================
            // 2️⃣ التحقق من صلاحية المدرب (الصح)
            // =========================
            var isAllowed = await _context.InstructorCurriculumBatches
                .AnyAsync(x =>
                    x.InstructorId == instructorId &&
                    x.BatchId == assignment.BatchId);

            if (!isAllowed)
            {
                return new PartnerExamStudentsVM
                {
                    ExamAssignmentId = assignment.Id,
                    ExamTitle = assignment.Title,
                    BatchName = assignment.BatchName,
                    Students = new List<PartnerExamStudentRowVM>()
                };
            }

            // =========================
            // 3️⃣ الطلاب
            // =========================
            var students = await _context.StudentBatchEnrollments
                .Where(x => x.BatchId == assignment.BatchId)
                .Select(x => new
                {
                    x.Student.StudentID,
                    x.Student.FullName
                })
                .ToListAsync();

            // =========================
            // 4️⃣ الحالات
            // =========================
            var statuses = await _context.ExamStudentStatuses
                .Where(x => x.ExamAssignmentId == examAssignmentId)
                .ToListAsync();

            // =========================
            // 5️⃣ بناء النتيجة
            // =========================
            var result = new List<PartnerExamStudentRowVM>();

            foreach (var s in students)
            {
                var status = statuses.FirstOrDefault(x => x.StudentId == s.StudentID);

                result.Add(new PartnerExamStudentRowVM
                {
                    StudentId = s.StudentID,
                    StudentName = s.FullName,
                    Status = status == null
                        ? "لم يبدأ"
                        : status.IsSubmitted
                            ? "تم التسليم"
                            : "قيد الحل",
                    IsSubmitted = status?.IsSubmitted ?? false
                });
            }

            return new PartnerExamStudentsVM
            {
                ExamAssignmentId = assignment.Id,
                ExamTitle = assignment.Title,
                BatchName = assignment.BatchName,
                Students = result
            };
        }



        public async Task<PartnerExamStudentsVM> GetIndividualExamStudentsAsync(int examAssignmentId, int instructorId)
        {
            // =========================
            // 1️⃣ تحميل الاختبار
            // =========================
            var assignment = await _context.ExamAssignmentsToStudents
     .Where(x => x.Id == examAssignmentId)
     .Select(x => new
     {
         x.Id,
         Title = x.Exam.Title
     })
     .FirstOrDefaultAsync();

            if (assignment == null)
                return null;

            // =========================
            // 2️⃣ الطلاب المرتبطين بالاختبار فقط
            // =========================
            var students = await _context.ExamAssignmentsToStudents
                .Where(x => x.Id == examAssignmentId)
                .Select(x => new
                {
                    x.StudentId,
                    StudentName = x.Student.FullName
                })
                .ToListAsync();

            // =========================
            // 3️⃣ الحالات
            // =========================
            var statuses = await _context.ExamStudentStatuses
                .Where(x => x.ExamAssignmentId == examAssignmentId)
                .ToListAsync();

            // =========================
            // 4️⃣ بناء النتيجة
            // =========================
            var result = new List<PartnerExamStudentRowVM>();

            foreach (var s in students)
            {
                var status = statuses.FirstOrDefault(x => x.StudentId == s.StudentId);

                result.Add(new PartnerExamStudentRowVM
                {
                    StudentId = s.StudentId,
                    StudentName = s.StudentName,
                    Status = status == null
                        ? "لم يبدأ"
                        : status.IsSubmitted
                            ? "تم التسليم"
                            : "قيد الحل",
                    IsSubmitted = status?.IsSubmitted ?? false
                });
            }

            return new PartnerExamStudentsVM
            {
                ExamAssignmentId = assignment.Id,
                ExamTitle = assignment.Title,
                BatchName = "اختبار فردي",
                Students = result
            };
        }






        // ============================
        // 📈 Attempts
        // ============================
        public async Task<PartnerExamAttemptsVM> GetStudentAttemptsAsync(int examAssignmentId, int studentId, int instructorId)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Where(x =>
                    x.Id == examAssignmentId &&
                    x.CreatedByInstructorId == instructorId)
                .Select(x => new { x.Id, x.Title })
                .FirstOrDefaultAsync();

            if (assignment == null)
                return null;

            var student = await _context.Students
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.StudentID, s.FullName })
                .FirstOrDefaultAsync();

            if (student == null)
                return null;

            var attempts = await _context.QuestionAttemptNew
                .Where(x =>
                    x.ExamAssignmentId == examAssignmentId &&
                    x.StudentId == studentId)
                .OrderBy(x => x.AttemptedAt)
                .Select(x => new PartnerExamAttemptRowVM
                {
                    AttemptNumber = x.AttemptNumber ?? 1,
                    AttemptedAt = x.AttemptedAt,
                    IsCorrect = x.IsCorrect,
                    TimeTakenSeconds = x.TimeTakenSeconds
                })
                .ToListAsync();

            return new PartnerExamAttemptsVM
            {
                ExamAssignmentId = examAssignmentId,
                StudentId = studentId,
                StudentName = student.FullName,
                ExamTitle = assignment.Title,
                Attempts = attempts
            };
        }

        // ============================
        // 📊 REPORT (Dashboard)
        // ============================
        public async Task<InstructorExamReportVM> GetExamReportAsync(int examAssignmentId, int instructorId)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Where(x =>
                    x.Id == examAssignmentId &&
                    x.CreatedByInstructorId == instructorId)
                .Select(x => new
                {
                    x.Id,
                    x.Title
                })
                .FirstOrDefaultAsync();

            if (assignment == null)
                return null;

            var attempts = await _context.QuestionAttemptNew
                .Where(x => x.ExamAssignmentId == examAssignmentId)
                .Select(x => new
                {
                    x.StudentId,
                    x.IsCorrect
                })
                .ToListAsync();

            if (!attempts.Any())
                return new InstructorExamReportVM
                {
                    ExamTitle = assignment.Title
                };

            // ============================
            // تجميع درجات الطلاب
            // ============================
            var scores = attempts
                .GroupBy(x => x.StudentId)
                .Select(g => g.Count(x => x.IsCorrect))
                .ToList();

            var totalStudents = scores.Count;

            var successCount = scores.Count(s => s >= 50);
            var failCount = totalStudents - successCount;

            return new InstructorExamReportVM
            {
                ExamTitle = assignment.Title,

                TotalStudents = totalStudents,
                Attempted = totalStudents,

                AvgScore = scores.Average(),
                HighestScore = scores.Max(),
                LowestScore = scores.Min(),

                SuccessCount = successCount,
                FailCount = failCount,

                Range0_50 = scores.Count(s => s < 50),
                Range50_75 = scores.Count(s => s >= 50 && s < 75),
                Range75_100 = scores.Count(s => s >= 75)
            };
        }
    }
}