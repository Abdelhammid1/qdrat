using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.Services.Exams.Implementations
{
    public class PartnerExamGenerationService : IPartnerExamGenerationService
    {
        private readonly ApplicationDbContext _context;

        public PartnerExamGenerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // 1️⃣ Generate Preview (NO SAVE)  ✅ FIXED
        // =====================================================
        public GeneratedExamResult GeneratePreview(GenerateExamRequestVM request)
        {
            if (request == null)
                throw new InvalidOperationException("بيانات التوليد غير صحيحة.");

            if (request.Curriculums == null || !request.Curriculums.Any())
                throw new InvalidOperationException("يجب اختيار منهج واحد على الأقل.");

            // =====================================================
            // 1️⃣ استخراج كل الـ LessonIds المطلوبة مرة واحدة
            // =====================================================
            var requestedLessonIds = request.Curriculums
                .Where(c => c.Sections != null)
                .SelectMany(c => c.Sections)
                .Where(s => s.Lessons != null)
                .SelectMany(s => s.Lessons)
                .Where(l => l.QuestionCount > 0)
                .Select(l => l.LessonId)
                .Distinct()
                .ToList();

            if (!requestedLessonIds.Any())
                throw new InvalidOperationException("يجب تحديد عدد أسئلة للمؤشرات.");

            // =====================================================
            // 2️⃣ جلب كل الأسئلة مرة واحدة فقط (SQL Server 2014 SAFE)
            // =====================================================
            var allQuestions = _context.Questions
       .AsNoTracking()
       .Where(q =>
           q.IsComplete &&
           q.IsAnswerConfirmed)
       .Select(q => new
       {
           q.Id,
           q.Title,
           q.Difficulty,
           q.CurriculumId,
           q.SectionId,
           SectionTitle = q.Section.Title,
           q.LessonId,
           LessonTitle = q.Lesson.Title
       })
       .ToList();

            // =====================================================
            // 3️⃣ تصفية في الذاكرة لتجنب Contains داخل SQL
            // =====================================================
            var filteredQuestions = allQuestions
                .Where(q => requestedLessonIds.Any(id => id == q.LessonId))
                .ToList();

            if (!filteredQuestions.Any())
                throw new InvalidOperationException("لا توجد أسئلة مطابقة للتوزيع المطلوب.");

            // =====================================================
            // 4️⃣ التوزيع حسب Lesson
            // =====================================================
            var resultQuestions = new List<GeneratedQuestionItem>();

            foreach (var curriculum in request.Curriculums)
            {
                if (curriculum.Sections == null)
                    continue;

                foreach (var section in curriculum.Sections)
                {
                    if (section.Lessons == null)
                        continue;

                    foreach (var lesson in section.Lessons)
                    {
                        if (lesson.QuestionCount <= 0)
                            continue;

                        var lessonPool = filteredQuestions
                            .Where(q =>
                                q.LessonId == lesson.LessonId &&
                                q.CurriculumId == curriculum.CurriculumId &&
                                q.SectionId == section.SectionId)
                            .OrderBy(x => Guid.NewGuid())
                            .Take(lesson.QuestionCount)
                            .ToList();

                        foreach (var q in lessonPool)
                        {
                            resultQuestions.Add(new GeneratedQuestionItem
                            {
                                QuestionId = q.Id,
                                Title = q.Title,
                                Difficulty = q.Difficulty.ToString(),
                                CurriculumId = curriculum.CurriculumId,
                                SectionId = q.SectionId,
                                SectionTitle = q.SectionTitle,
                                LessonId = q.LessonId,
                                LessonTitle = q.LessonTitle
                            });
                        }
                    }
                }
            }

            if (!resultQuestions.Any())
                throw new InvalidOperationException("لم يتم توليد أي أسئلة.");

            // =====================================================
            // 5️⃣ إزالة التكرار (احتياطي)
            // =====================================================
            var distinctQuestions = resultQuestions
                .GroupBy(x => x.QuestionId)
                .Select(g => g.First())
                .ToList();

            return new GeneratedExamResult
            {
                ExamTitle = request.ExamTitle,
                Questions = distinctQuestions
            };
        }

        // =====================================================
        // 2️⃣ Save Draft
        // =====================================================
        public int SaveDraft(GeneratedExamResult generated, int partnerId)
        {
            if (generated == null || generated.Questions == null || !generated.Questions.Any())
                throw new InvalidOperationException("لا توجد أسئلة لحفظ المسودة.");

            var strategy = _context.Database.CreateExecutionStrategy();
            int draftId = 0;

            strategy.Execute(() =>
            {
                using var transaction = _context.Database.BeginTransaction();

                var firstCurriculumId = generated.Questions
                    .Select(q => q.CurriculumId)
                    .Distinct()
                    .FirstOrDefault();

                if (firstCurriculumId <= 0)
                    throw new InvalidOperationException("لم يتم تحديد منهج صالح للمسودة.");

                var draft = new ExamDraft
                {
                    Title = generated.ExamTitle,
                    PartnerId = partnerId,
                    CurriculumId = firstCurriculumId,
                    CreatedAt = DateTime.Now,
                    IsArchived = false
                };

                _context.ExamDrafts.Add(draft);
                _context.SaveChanges();

                int order = 1;
                foreach (var q in generated.Questions)
                {
                    _context.ExamDraftQuestions.Add(new ExamDraftQuestion
                    {
                        ExamDraftId = draft.Id,
                        QuestionId = q.QuestionId,
                        Order = order++
                    });
                }

                _context.SaveChanges();
                transaction.Commit();

                draftId = draft.Id;
            });

            return draftId;
        }


        // =====================================================
        // 3️⃣ Drafts List
        // =====================================================
        public List<ExamDraftListVM> GetPartnerDrafts(int partnerId)
        {
            return _context.ExamDrafts
                .Where(d => d.PartnerId == partnerId && !d.IsArchived)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new ExamDraftListVM
                {
                    DraftId = d.Id,
                    Title = d.Title,
                    CreatedAt = d.CreatedAt,
                    QuestionsCount = d.DraftQuestions.Count
                })
                .ToList();
        }

        // =====================================================
        // 4️⃣ Preview Draft
        // =====================================================
        public ExamDraftPreviewVM GetDraftForPreview(int draftId)
        {
            var questions = _context.ExamDraftQuestions
                .Where(x => x.ExamDraftId == draftId)
                .Select(x => new
                {
                    x.QuestionId,
                    LessonId = x.Question.Lesson.Id,
                    LessonTitle = x.Question.Lesson.Title,
                    SectionId = x.Question.Section.Id,
                    SectionTitle = x.Question.Section.Title,
                    Title = x.Question.Title
                })
                .ToList();

            if (!questions.Any())
                throw new InvalidOperationException("لا توجد أسئلة في هذه المسودة.");

            var lessonGroups = questions
                .GroupBy(q => new { q.LessonId, q.LessonTitle })
                .Select(g => new ExamDraftLessonGroupVM
                {
                    LessonId = g.Key.LessonId,
                    LessonTitle = g.Key.LessonTitle,
                    Questions = g.Select(x => new ExamDraftQuestionItemVM
                    {
                        QuestionId = x.QuestionId,
                        Title = x.Title
                    }).ToList()
                })
                .ToList();

            var draftInfo = _context.ExamDrafts
                .Where(d => d.Id == draftId)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    CourseName = d.Curriculum.CourseCurriculums
                        .Select(cc => cc.Course.Name)
                        .FirstOrDefault(),
                    CurriculumName = d.Curriculum.Title
                })
                .FirstOrDefault();

            return new ExamDraftPreviewVM
            {
                DraftId = draftInfo.Id,
                Title = draftInfo.Title,
                CourseName = draftInfo.CourseName ?? "",
                CurriculumName = draftInfo.CurriculumName ?? "",
                TotalQuestions = questions.Count,
                LessonGroups = lessonGroups
            };
        }

        public ReplaceExamDraftQuestionVM GetReplaceCandidates(
    int draftId,
    Guid oldQuestionId,
    int lessonId)
        {
            // ===============================
            // 1️⃣ الأسئلة المستخدمة في المسودة
            // ===============================
            var usedQuestionIds = _context.ExamDraftQuestions
                .Where(x => x.ExamDraftId == draftId)
                .Select(x => x.QuestionId)
                .ToList();

            // ===============================
            // 2️⃣ جلب أسئلة من نفس الـ Lesson
            // ===============================
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
                .ToList() // SQL Server 2014 SAFE
                .Where(q =>
                    !usedQuestionIds.Contains(q.Id) &&
                    q.Id != oldQuestionId)
                .Select(q => new ReplaceCandidateQuestionVM
                {
                    QuestionId = q.Id,
                    Title = q.Title
                })
                .ToList();

            // ===============================
            // 3️⃣ بيانات العرض
            // ===============================
            var lesson = _context.Lessons
                .Where(l => l.Id == lessonId)
                .Select(l => new { l.Id, l.Title })
                .FirstOrDefault();

            var oldQuestionTitle = _context.Questions
                .Where(q => q.Id == oldQuestionId)
                .Select(q => q.Title)
                .FirstOrDefault();

            return new ReplaceExamDraftQuestionVM
            {
                DraftId = draftId,
                OldQuestionId = oldQuestionId,
                LessonId = lessonId,
                LessonTitle = lesson?.Title,
                OldQuestionTitle = oldQuestionTitle,
                Candidates = candidates
            };
        }



        public void AddQuestionToDraft(
    int draftId,
    Guid questionId)
        {
            var exists = _context.ExamDraftQuestions
                .Any(x =>
                    x.ExamDraftId == draftId &&
                    x.QuestionId == questionId);

            if (exists)
                throw new InvalidOperationException("السؤال موجود بالفعل داخل المسودة.");

            var maxOrder = _context.ExamDraftQuestions
                .Where(x => x.ExamDraftId == draftId)
                .Select(x => (int?)x.Order)
                .Max() ?? 0;

            _context.ExamDraftQuestions.Add(new ExamDraftQuestion
            {
                ExamDraftId = draftId,
                QuestionId = questionId,
                Order = maxOrder + 1
            });

            _context.SaveChanges();
        }




        // =====================================================
        // 5️⃣ Replace Question
        // =====================================================
      






        public void ReplaceQuestion(int draftId, Guid oldQuestionId, Guid newQuestionId)
        {
            var item = _context.ExamDraftQuestions
                .FirstOrDefault(x =>
                    x.ExamDraftId == draftId &&
                    x.QuestionId == oldQuestionId);

            if (item == null)
                throw new InvalidOperationException("السؤال غير موجود.");

            item.QuestionId = newQuestionId;
            _context.SaveChanges();
        }

        // =====================================================
        // 6️⃣ Send Draft
        // =====================================================
        public SendExamDraftVM PrepareSendDraftVM(int draftId, int partnerId)
        {
            var draft = _context.ExamDrafts
                .Include(d => d.Curriculum)
                .FirstOrDefault(d =>
                    d.Id == draftId &&
                    !d.IsArchived);

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة.");

            if (draft.PartnerId != partnerId)
                throw new InvalidOperationException("لا تملك صلاحية الوصول لهذه المسودة.");

            var courseId = _context.CourseCurriculums
                .Where(x => x.CurriculumId == draft.CurriculumId)
                .Select(x => x.CourseId)
                .FirstOrDefault();

            var batches = _context.Batches
                .Where(b =>
                    b.CourseId == courseId &&
                    b.Branch.PartnerId == partnerId)
                .Select(b => new BatchSelectItemVM
                {
                    BatchId = b.Id,
                    BatchName = b.Name
                })
                .ToList();

            return new SendExamDraftVM
            {
                DraftId = draftId,
                ExamTitle = draft.Title,
                CurriculumId = draft.CurriculumId,
                CourseId = courseId,
                AvailableBatches = batches,
                DurationMinutes = 60
            };
        }

        public void SendDraftToBatch(SendExamDraftVM model, int partnerId)
        {
            if (model == null || model.BatchIds == null || !model.BatchIds.Any())
                throw new InvalidOperationException("يجب اختيار دفعة واحدة على الأقل.");

            // ===============================
            // 1️⃣ جلب المسودة + الأسئلة
            // ===============================
            var draftData = _context.ExamDrafts
                .Where(d =>
                    d.Id == model.DraftId &&
                    d.PartnerId == partnerId &&
                    !d.IsArchived)
                .Select(d => new
                {
                    Draft = d,
                    QuestionIds = d.DraftQuestions
                        .OrderBy(q => q.Order)
                        .Select(q => q.QuestionId)
                        .ToList()
                })
                .FirstOrDefault();

            if (draftData == null)
                throw new InvalidOperationException("المسودة غير موجودة أو لا تخص هذا الشريك.");

            // ===============================
            // 2️⃣ فلترة الدفعات (مهم جدًا)
            // فقط الدفعات التي تحتوي طلاب فعليًا
            // ===============================
            // 1️⃣ تحميل كل بيانات الربط مرة واحدة
            var enrollments = _context.StudentBatchEnrollments
                .Select(x => x.BatchId)
                .ToList();

            // 2️⃣ فلترة في الذاكرة (SQL 2014 SAFE)
            var validBatchIds = enrollments
                .Where(e => model.BatchIds.Any(b => b == e))
                .Distinct()
                .ToList();

            if (!validBatchIds.Any())
                throw new InvalidOperationException("لا توجد دفعات تحتوي طلاب.");

            // ===============================
            // 3️⃣ تنفيذ الإرسال لكل دفعة
            // ===============================
            foreach (var batchId in validBatchIds)
            {
                var batch = _context.Batches
                    .Where(b =>
                        b.Id == batchId &&
                        b.Branch.PartnerId == partnerId)
                    .Select(b => new { b.Id })
                    .FirstOrDefault();

                if (batch == null)
                    continue;

                // ===============================
                // 4️⃣ إنشاء Exam
                // ===============================
                var exam = new Exam
                {
                    Title = draftData.Draft.Title,
                    CurriculumId = draftData.Draft.CurriculumId,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Exams.Add(exam);
                _context.SaveChanges();

                // ===============================
                // 5️⃣ Assignment
                // ===============================
                var assignment = new ExamAssignmentToBatch
                {
                    ExamId = exam.Id,
                    BatchId = batchId,
                    ScheduledDate = model.StartAt,
                    EndAt = model.EndAt,
                    DurationMinutes = model.DurationMinutes,
                    CreatedAt = DateTime.Now,
                    AssignedAt = DateTime.Now,
                    IsSentToStudents = true,
                    Title = exam.Title,
                    TotalQuestions = draftData.QuestionIds.Count
                };

                _context.ExamAssignmentsToBatches.Add(assignment);
                _context.SaveChanges();

                // ===============================
                // 6️⃣ تثبيت الأسئلة (مرة واحدة)
                // ===============================
                var examQuestions = new List<ExamQuestion>();
                int order = 1;

                foreach (var qId in draftData.QuestionIds)
                {
                    examQuestions.Add(new ExamQuestion
                    {
                        ExamId = exam.Id,
                        ExamAssignmentId = assignment.Id,
                        QuestionId = qId,
                        Order = order++
                    });
                }

                _context.ExamQuestions.AddRange(examQuestions);
                _context.SaveChanges();

                // ===============================
                // 7️⃣ جلب الطلاب الفعليين
                // ===============================
                var studentIds = _context.StudentBatchEnrollments
                    .Where(x => x.BatchId == batchId)
                    .Select(x => x.StudentID)
                    .ToList();

                if (!studentIds.Any())
                    continue;

                // ===============================
                // 8️⃣ إنشاء Status لكل الطلاب (مرة واحدة)
                // ===============================
                var statuses = new List<ExamStudentStatus>();

                foreach (var studentId in studentIds)
                {
                    statuses.Add(new ExamStudentStatus
                    {
                        StudentId = studentId,
                        ExamId = exam.Id,
                        ExamAssignmentId = assignment.Id,
                        Status = ExamStatus.Pending,
                        IsSubmitted = false,
                        AssignedAt = DateTime.Now
                    });
                }

                _context.ExamStudentStatuses.AddRange(statuses);
                _context.SaveChanges();
            }
        }

        // =====================================================
        // 4️⃣ Get Add Question Candidates (Lesson-based)
        // =====================================================
        public AddExamDraftQuestionVM GetAddQuestionCandidates(
            int draftId,
            int lessonId)
        {
            // 1️⃣ الأسئلة المستخدمة بالفعل في المسودة
            var usedQuestionIds = _context.ExamDraftQuestions
                .Where(x => x.ExamDraftId == draftId)
                .Select(x => x.QuestionId)
                .ToList();

            // 2️⃣ جلب أسئلة من نفس الـ Lesson وغير مستخدمة
            var questions = _context.Questions
                .Where(q =>
                    q.LessonId == lessonId &&
                    q.IsComplete &&
                    q.IsAnswerConfirmed)
                .Select(q => new
                {
                    q.Id,
                    q.Title
                })
                .ToList() // SQL Server 2014 SAFE
                .Where(q => !usedQuestionIds.Contains(q.Id))
                .Select(q => new AddExamDraftQuestionItemVM
                {
                    QuestionId = q.Id,
                    Title = q.Title
                })
                .ToList();

            // 3️⃣ بيانات المحور
            var lessonTitle = _context.Lessons
                .Where(l => l.Id == lessonId)
                .Select(l => l.Title)
                .FirstOrDefault();

            return new AddExamDraftQuestionVM
            {
                DraftId = draftId,
                LessonId = lessonId,
                LessonTitle = lessonTitle,
                Questions = questions
            };
        }

        public void ConfirmStudentExam(ConfirmStudentExamVM model)
        {
            if (model == null || model.QuestionIds == null || !model.QuestionIds.Any())
                throw new InvalidOperationException("لا توجد أسئلة لإرسال الاختبار.");

            // ===============================
            // 1) إنشاء Exam
            // ===============================
            var exam = new Exam
            {
                Title = model.ExamTitle,
                CurriculumId = model.CurriculumId,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Exams.Add(exam);
            _context.SaveChanges();

            // ===============================
            // 2) Assignment لطالب
            // ===============================
            var assignment = new ExamAssignmentToStudent
            {
                ExamId = exam.Id,
                StudentId = model.StudentId,
                IsSent = true,
                ScheduledDate = model.ScheduledDate ?? DateTime.Now,
                EndAt = model.EndAt ?? DateTime.Now.AddMinutes(60),
                CreatedAt = DateTime.Now
            };

            _context.ExamAssignmentsToStudents.Add(assignment);
            _context.SaveChanges();

            // ===============================
            // 3) تثبيت الأسئلة
            // ===============================
            int order = 1;
            foreach (var qId in model.QuestionIds)
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamAssignmentToStudentId = assignment.Id,
                    QuestionId = qId,
                    Order = order++
                });
            }

            _context.SaveChanges();

            // ===============================
            // 4) StudentStatus
            // ===============================
            _context.ExamStudentStatuses.Add(new ExamStudentStatus
            {
                StudentId = model.StudentId,
                ExamId = exam.Id,
                ExamAssignmentToStudentId = assignment.Id,
                Status = ExamStatus.Pending,
                AssignedAt = DateTime.Now
            });

            _context.SaveChanges();
        }

    }
}
