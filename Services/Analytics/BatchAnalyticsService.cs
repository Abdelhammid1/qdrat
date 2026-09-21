using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public class BatchAnalyticsService : IBatchAnalyticsService
    {
        private readonly ApplicationDbContext _context;

        public BatchAnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BatchPerformanceDetailsVM?> BuildBatchPerformanceAsync(
            int batchId, int? curriculumId, int? instructorId)
        {
            var batch = await _context.Set<Batch>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == batchId);

            if (batch == null) return null;

            var course = await _context.Set<Course>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == batch.CourseId);

            // ── تحميل طلاب الدفعة فقط ─────────────────────────────────────────
            var batchStudentIds = await _context.Set<StudentBatchEnrollment>()
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .Select(x => x.StudentID)
                .Distinct()
                .ToListAsync();

            var batchStudentIdSet = new HashSet<int>(batchStudentIds);

            // ── بناء استعلام المحاولات بفلاتر SQL ────────────────────────────
            IQueryable<QuestionAttemptNew> attemptsQuery = _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(x => batchStudentIdSet.Contains(x.StudentId));

            if (curriculumId.HasValue)
            {
                var sectionIds = _context.Set<Section>()
                    .Where(s => s.CurriculumId == curriculumId).Select(s => s.Id);

                var lessonIds = _context.Set<Lesson>()
                    .Where(l => sectionIds.Contains(l.SectionId)).Select(l => l.Id);

                attemptsQuery = attemptsQuery.Where(x =>
                    (x.LessonId.HasValue && lessonIds.Contains(x.LessonId.Value)) ||
                    (x.SectionId.HasValue && sectionIds.Contains(x.SectionId.Value)));
            }

            if (instructorId.HasValue)
            {
                var instructorBatchIds = _context.Set<InstructorCurriculumBatch>()
                    .Where(x => x.InstructorId == instructorId)
                    .Select(x => x.BatchId);

                if (!await instructorBatchIds.ContainsAsync(batchId))
                    return BuildEmptyBatchResult(batch, course, batchStudentIds, curriculumId, instructorId);
            }

            var attempts = await attemptsQuery.ToListAsync();

            // ── البيانات المساعدة ─────────────────────────────────────────────
            var lessonsDb = await _context.Set<Lesson>().AsNoTracking().ToListAsync();
            var questionsDb = await _context.Set<Question>().AsNoTracking().ToListAsync();
            var instructorsDb = await _context.Set<Instructor>().AsNoTracking().ToListAsync();
            var icbDb = await _context.Set<InstructorCurriculumBatch>().AsNoTracking().ToListAsync();
            var studentsDb = await _context.Students.AsNoTracking().ToListAsync();
            var attendanceDb = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => batchStudentIdSet.Contains(a.StudentId))
                .ToListAsync();

            var lessonMap = lessonsDb.ToDictionary(l => l.Id);
            var questionMap = questionsDb.ToDictionary(q => q.Id);
            var instructorMap = instructorsDb.ToDictionary(i => i.Id);
            var studentMap = studentsDb.ToDictionary(s => s.StudentID);
            var attendanceByStudent = attendanceDb.GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ── مدربو الدفعة ──────────────────────────────────────────────────
            var batchInstructorIds = icbDb
                .Where(x => x.BatchId == batchId && x.InstructorId > 0)
                .Select(x => x.InstructorId)
                .Distinct()
                .ToList();

            var batchInstructorIdSet = new HashSet<int>(batchInstructorIds);

            // ── الدروس الضعيفة ────────────────────────────────────────────────
            var weakLessons = attempts
                .Where(x => x.LessonId.HasValue)
                .GroupBy(x => x.LessonId!.Value)
                .Select(g =>
                {
                    lessonMap.TryGetValue(g.Key, out var lesson);

                    var lessonTotal = g.Count();
                    var lessonWrong = g.Count(x => !x.IsCorrect);
                    var weaknessPct = lessonTotal == 0 ? 0 : Math.Round((lessonWrong * 100.0) / lessonTotal, 1);

                    int? lessonInstructorId = null;
                    string lessonInstructorName = "-";

                    var icbForBatch = icbDb.FirstOrDefault(x => x.BatchId == batchId);
                    if (icbForBatch != null)
                    {
                        lessonInstructorId = icbForBatch.InstructorId;
                        if (instructorMap.TryGetValue(icbForBatch.InstructorId, out var instr))
                            lessonInstructorName = instr.FullName;
                    }

                    var highRiskQuestions = g
                        .GroupBy(x => x.QuestionId)
                        .Select(qg =>
                        {
                            questionMap.TryGetValue(qg.Key, out var question);

                            var qTotal = qg.Count();
                            var qWrong = qg.Count(x => !x.IsCorrect);
                            var errPct = qTotal == 0 ? 0 : Math.Round((qWrong * 100.0) / qTotal, 1);

                            return new BatchHighRiskQuestionVM
                            {
                                QuestionId = qg.Key,
                                ReferenceNumber = question?.ReferenceNumber ?? "",
                                QuestionTitle = question?.Title ?? "سؤال غير معروف",
                                TotalAttempts = qTotal,
                                WrongAttempts = qWrong,
                                CorrectAttempts = qg.Count(x => x.IsCorrect),
                                ErrorPercentage = errPct
                            };
                        })
                        .Where(x => x.ErrorPercentage is >= 60 and <= 100)
                        .OrderByDescending(x => x.ErrorPercentage)
                        .ThenByDescending(x => x.WrongAttempts)
                        .ToList();

                    return new BatchWeakLessonDetailsVM
                    {
                        LessonId = g.Key,
                        LessonName = lesson?.Title ?? "مؤشر غير معروف",
                        InstructorId = lessonInstructorId,
                        InstructorName = lessonInstructorName,
                        AffectedStudents = g.Where(x => !x.IsCorrect).Select(x => x.StudentId).Distinct().Count(),
                        TotalAttempts = lessonTotal,
                        WrongAttempts = lessonWrong,
                        WeaknessPercentage = weaknessPct,
                        HighRiskQuestions = highRiskQuestions
                    };
                })
                .Where(x => x.WeaknessPercentage >= 50)
                .OrderByDescending(x => x.WeaknessPercentage)
                .ToList();

            // ── ملخص المدربين ─────────────────────────────────────────────────
            var instructorSummaries = batchInstructorIds
                .Select(id =>
                {
                    instructorMap.TryGetValue(id, out var instr);

                    var instrWeakLessons = weakLessons
                        .Where(x => x.InstructorId == id)
                        .ToList();

                    var instrTotal = instrWeakLessons.Sum(x => x.TotalAttempts);
                    var instrWrong = instrWeakLessons.Sum(x => x.WrongAttempts);

                    var instrScore = instrTotal == 0
                        ? (attempts.Count == 0 ? 0 : Math.Round((attempts.Count(x => x.IsCorrect) * 100.0) / attempts.Count, 1))
                        : Math.Round(((instrTotal - instrWrong) * 100.0) / instrTotal, 1);

                    return new BatchInstructorSummaryVM
                    {
                        InstructorId = id,
                        InstructorName = instr?.FullName ?? "مدرب غير معروف",
                        WeakLessonsCount = instrWeakLessons.Count,
                        HighRiskQuestionsCount = instrWeakLessons.Sum(x => x.HighRiskQuestions.Count),
                        AvgScore = instrScore
                    };
                })
                .OrderBy(x => x.AvgScore)
                .ToList();

            // ── مخاطر الطلاب ──────────────────────────────────────────────────
            var students = batchStudentIds
                .Select(studentId =>
                {
                    studentMap.TryGetValue(studentId, out var student);

                    var studentAttempts = attempts.Where(x => x.StudentId == studentId).ToList();

                    var examAttempts = studentAttempts.Where(x =>
                        x.ExamAssignmentId.HasValue ||
                        x.ExamId.HasValue ||
                        x.PerformanceIndicatorExamId.HasValue).ToList();

                    var hwAttempts = studentAttempts.Where(x => x.HomeworkSetId.HasValue).ToList();

                    var examScore = examAttempts.Count == 0 ? 0
                        : Math.Round((examAttempts.Count(x => x.IsCorrect) * 100.0) / examAttempts.Count, 1);

                    var hwScore = hwAttempts.Count == 0 ? 0
                        : Math.Round((hwAttempts.Count(x => x.IsCorrect) * 100.0) / hwAttempts.Count, 1);

                    var att = attendanceByStudent.GetValueOrDefault(studentId) ?? new();
                    var attScore = att.Count == 0 ? 0
                        : Math.Round((att.Count(a => a.IsPresent) * 100.0) / att.Count, 1);

                    var risk = (examScore < 50 || hwScore < 50 || attScore < 50) ? "خطر"
                        : (examScore < 70 || hwScore < 70 || attScore < 70) ? "متوسط" : "مستقر";

                    return new BatchStudentRiskVM
                    {
                        StudentId = studentId,
                        StudentName = student?.FullName ?? "طالب غير معروف",
                        ExamScore = examScore,
                        HomeworkScore = hwScore,
                        Attendance = attScore,
                        RiskLevel = risk
                    };
                })
                .OrderBy(x => x.ExamScore)
                .ToList();

            // ── KPIs ──────────────────────────────────────────────────────────
            var totalAttempts = attempts.Count;
            var correctAttempts = attempts.Count(x => x.IsCorrect);
            var wrongAttempts = attempts.Count(x => !x.IsCorrect);
            var avgScore = totalAttempts == 0 ? 0 : Math.Round((correctAttempts * 100.0) / totalAttempts, 1);
            var weaknessPct = totalAttempts == 0 ? 0 : Math.Round((wrongAttempts * 100.0) / totalAttempts, 1);

            string riskLevel, decisionMessage;
            if (weaknessPct >= 80) { riskLevel = "خطر عالي جدًا"; decisionMessage = "الدفعة تحتاج تدخل عاجل، ويجب إلزام المدربين بإعادة شرح المؤشرات والأسئلة عالية الخطأ."; }
            else if (weaknessPct >= 60) { riskLevel = "خطر عالي"; decisionMessage = "يجب إعادة شرح الدروس الحرجة وإرسال واجب علاجي واختبار قصير للدفعة."; }
            else if (weaknessPct >= 40) { riskLevel = "متوسط"; decisionMessage = "تحتاج الدفعة إلى متابعة مركزة على المؤشرات الأعلى ضعفًا."; }
            else { riskLevel = "مستقر"; decisionMessage = "أداء الدفعة مستقر حاليًا ولا يحتاج تدخل عاجل."; }

            return new BatchPerformanceDetailsVM
            {
                BatchId = batch.Id,
                CurriculumId = curriculumId,
                InstructorId = instructorId,
                BatchName = batch.Name,
                CourseName = course?.Name ?? "-",
                StudentsCount = batchStudentIds.Count,
                TotalAttempts = totalAttempts,
                CorrectAttempts = correctAttempts,
                WrongAttempts = wrongAttempts,
                AvgScore = avgScore,
                WeaknessPercentage = weaknessPct,
                CriticalLessonsCount = weakLessons.Count,
                HighRiskQuestionsCount = weakLessons.Sum(x => x.HighRiskQuestions.Count),
                RiskLevel = riskLevel,
                DecisionMessage = decisionMessage,
                Instructors = instructorSummaries,
                WeakLessons = weakLessons,
                Students = students
            };
        }

        public async Task<WeakLessonDetailsVM?> BuildWeakLessonAsync(
            int lessonId, int? curriculumId, int? batchId, int? instructorId)
        {
            var lesson = await _context.Set<Lesson>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == lessonId);

            if (lesson == null) return null;

            // ── محاولات الدرس فقط من قاعدة البيانات ─────────────────────────
            IQueryable<QuestionAttemptNew> attemptsQuery = _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(x => x.LessonId == lessonId);

            if (batchId.HasValue)
            {
                var batchStudentIds = _context.Set<StudentBatchEnrollment>()
                    .Where(x => x.BatchId == batchId)
                    .Select(x => x.StudentID);

                attemptsQuery = attemptsQuery.Where(x => batchStudentIds.Contains(x.StudentId));
            }

            if (instructorId.HasValue)
            {
                var instrBatchIds = _context.Set<InstructorCurriculumBatch>()
                    .Where(x => x.InstructorId == instructorId)
                    .Select(x => x.BatchId);

                var instrStudentIds = _context.Set<StudentBatchEnrollment>()
                    .Where(x => instrBatchIds.Contains(x.BatchId))
                    .Select(x => x.StudentID);

                attemptsQuery = attemptsQuery.Where(x => instrStudentIds.Contains(x.StudentId));
            }

            var attempts = await attemptsQuery.ToListAsync();

            // ── البيانات المساعدة ─────────────────────────────────────────────
            var questionsDb = await _context.Set<Question>()
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId)
                .ToListAsync();

            var studentIds = attempts.Select(x => x.StudentId).Distinct().ToList();
            var studentsDb = await _context.Students
                .AsNoTracking()
                .Where(s => studentIds.Contains(s.StudentID))
                .ToListAsync();

            var batchesDb = await _context.Set<Batch>().AsNoTracking().ToListAsync();
            var instructorsDb = await _context.Set<Instructor>().AsNoTracking().ToListAsync();
            var icbDb = await _context.Set<InstructorCurriculumBatch>().AsNoTracking().ToListAsync();
            var enrollments = await _context.Set<StudentBatchEnrollment>().AsNoTracking().ToListAsync();

            var questionMap = questionsDb.ToDictionary(q => q.Id);
            var studentMap = studentsDb.ToDictionary(s => s.StudentID);
            var batchMap = batchesDb.ToDictionary(b => b.Id);
            var instructorMap = instructorsDb.ToDictionary(i => i.Id);

            // ── حل الدفعة والمدرب ────────────────────────────────────────────
            int? resolvedBatchId = batchId;
            string batchName = "-";

            if (!resolvedBatchId.HasValue)
            {
                var firstStudentId = attempts.Select(x => x.StudentId).FirstOrDefault();
                if (firstStudentId > 0)
                    resolvedBatchId = enrollments.FirstOrDefault(e => e.StudentID == firstStudentId)?.BatchId;
            }

            if (resolvedBatchId.HasValue && batchMap.TryGetValue(resolvedBatchId.Value, out var resolvedBatch))
                batchName = resolvedBatch.Name;

            int? resolvedInstructorId = instructorId;
            string instructorName = "-";

            if (!resolvedInstructorId.HasValue && resolvedBatchId.HasValue)
                resolvedInstructorId = icbDb.FirstOrDefault(x => x.BatchId == resolvedBatchId.Value)?.InstructorId;

            if (resolvedInstructorId.HasValue && instructorMap.TryGetValue(resolvedInstructorId.Value, out var resolvedInstructor))
                instructorName = resolvedInstructor.FullName;

            // ── تحليل الأسئلة ─────────────────────────────────────────────────
            var questions = attempts
                .GroupBy(x => x.QuestionId)
                .Select(g =>
                {
                    questionMap.TryGetValue(g.Key, out var question);

                    var qTotal = g.Count();
                    var qWrong = g.Count(x => !x.IsCorrect);
                    var errPct = qTotal == 0 ? 0 : Math.Round((qWrong * 100.0) / qTotal, 1);

                    var wrongStudents = g
                        .GroupBy(x => x.StudentId)
                        .Select(sg =>
                        {
                            studentMap.TryGetValue(sg.Key, out var s);
                            var lastWrong = sg.Where(x => !x.IsCorrect)
                                .OrderByDescending(x => x.AttemptedAt)
                                .FirstOrDefault();

                            return new WeakLessonStudentErrorVM
                            {
                                StudentId = sg.Key,
                                StudentName = s?.FullName ?? "طالب غير معروف",
                                LastWrongAnswer = lastWrong?.SelectedAnswer ?? "",
                                LastAttemptedAt = lastWrong?.AttemptedAt ?? DateTime.MinValue,
                                WrongAttemptsCount = sg.Count(x => !x.IsCorrect),
                                CorrectAttemptsCount = sg.Count(x => x.IsCorrect)
                            };
                        })
                        .Where(x => x.WrongAttemptsCount > 0)
                        .OrderBy(x => x.StudentName)
                        .ToList();

                    return new WeakLessonQuestionDetailsVM
                    {
                        QuestionId = g.Key,
                        ReferenceNumber = question?.ReferenceNumber ?? "",
                        QuestionTitle = question?.Title ?? "سؤال غير معروف",
                        TotalAttempts = qTotal,
                        WrongAttempts = qWrong,
                        CorrectAttempts = g.Count(x => x.IsCorrect),
                        ErrorPercentage = errPct,
                        WrongStudents = wrongStudents
                    };
                })
                .Where(x => x.ErrorPercentage is >= 60 and <= 100)
                .OrderByDescending(x => x.ErrorPercentage)
                .ThenByDescending(x => x.WrongAttempts)
                .ToList();

            // ── الملخص ────────────────────────────────────────────────────────
            var totalAttempts = attempts.Count;
            var wrongAttempts = attempts.Count(x => !x.IsCorrect);
            var correctAttempts = attempts.Count(x => x.IsCorrect);
            var weaknessPct = totalAttempts == 0 ? 0 : Math.Round((wrongAttempts * 100.0) / totalAttempts, 1);

            string riskLevel, decisionMessage;
            if (weaknessPct >= 80) { riskLevel = "خطر عالي جدًا"; decisionMessage = "يجب توجيه المدرب لإعادة شرح هذا المؤشر بالكامل مع حل الأسئلة عالية الخطأ أمام الطلاب."; }
            else if (weaknessPct >= 60) { riskLevel = "خطر عالي"; decisionMessage = "يجب إعادة شرح الأسئلة التي تجاوزت 60% أخطاء مع اختبار قصير بعد الشرح."; }
            else if (weaknessPct >= 40) { riskLevel = "متوسط"; decisionMessage = "يحتاج المؤشر إلى مراجعة تدريبية مركزة على الأسئلة الأعلى خطأ."; }
            else { riskLevel = "مستقر"; decisionMessage = "لا توجد مشكلة جوهرية في هذا المؤشر حاليًا."; }

            return new WeakLessonDetailsVM
            {
                LessonId = lesson.Id,
                CurriculumId = curriculumId,
                LessonName = lesson.Title,
                BatchId = resolvedBatchId,
                BatchName = batchName,
                InstructorId = resolvedInstructorId,
                InstructorName = instructorName,
                TotalAttempts = totalAttempts,
                WrongAttempts = wrongAttempts,
                CorrectAttempts = correctAttempts,
                WeaknessPercentage = weaknessPct,
                AffectedStudents = attempts.Where(x => !x.IsCorrect).Select(x => x.StudentId).Distinct().Count(),
                RiskLevel = riskLevel,
                DecisionMessage = decisionMessage,
                Questions = questions
            };
        }

        private static BatchPerformanceDetailsVM BuildEmptyBatchResult(
            Batch batch, Course? course, List<int> studentIds,
            int? curriculumId, int? instructorId)
        {
            return new BatchPerformanceDetailsVM
            {
                BatchId = batch.Id,
                CurriculumId = curriculumId,
                InstructorId = instructorId,
                BatchName = batch.Name,
                CourseName = course?.Name ?? "-",
                StudentsCount = studentIds.Count,
                RiskLevel = "مستقر",
                DecisionMessage = "المدرب المحدد ليس مرتبطًا بهذه الدفعة."
            };
        }
    }
}
