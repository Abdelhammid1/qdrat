using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Batch;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Lesson;
using QdratNew.ViewModels.Students;
using System.Linq;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{



    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class BatchLessonCompletionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISectionExamGeneratorService _examGeneratorService;
        private readonly UserManager<ApplicationUser> _userManager;
        public BatchLessonCompletionsController(ApplicationDbContext context, ISectionExamGeneratorService examGeneratorService, UserManager<ApplicationUser> userManager   )
        {
            _context = context;
            _examGeneratorService = examGeneratorService;
            _userManager = userManager;
        }
        public async Task<IActionResult> SelectLessons(int? batchId)
        {
            var batches = await _context.Batches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"الدفعة - {b.Name}"
                }).ToListAsync();

            var vm = new LessonCompletionFormViewModel
            {
                BatchList = batches,
                CurriculumList = new List<SelectListItem>(),
                SectionList = new List<SelectListItem>(),
                AvailableLessons = new List<LessonViewItem>(),
                SelectedBatchId = batchId ?? 0
            };

            if (TempData.ContainsKey("CompletedLessonIds"))
            {
                vm.PreSelectedLessonIds = TempData["CompletedLessonIds"].ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToList();
            }

            return View(vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCompletedLessons(LessonCompletionFormViewModel model)
        {
            if (string.IsNullOrEmpty(model.CompletedLessonIds))
            {
                TempData["Error"] = "لم يتم اختيار أي مؤشرات.";
                return RedirectToAction("SelectLessons", new { batchId = model.SelectedBatchId });
            }

            var lessonIds = model.CompletedLessonIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

            var existing = await _context.BatchLessonCompletions
                .Where(x => x.BatchId == model.SelectedBatchId)
                .ToListAsync();

            _context.BatchLessonCompletions.RemoveRange(existing);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var lessonId in lessonIds)
            {
                _context.BatchLessonCompletions.Add(new BatchLessonCompletion
                {
                    BatchId = model.SelectedBatchId,
                    LessonId = lessonId,
                    CompletionDate = DateTime.Now,
                    LastCompletedAt = DateTime.Now,
                    CompletionTitle = model.CompletionTitle,
                    AddedBy = userId
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حفظ المؤشرات المنتهية بنجاح.";
            return RedirectToAction("ConfirmHomework", "BatchLessonCompletions", new { batchId = model.SelectedBatchId });
        }


        [HttpGet]
        public async Task<IActionResult> GetSectionsByCurriculum(int curriculumId)
        {
            var sections = await _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .OrderBy(s => s.Title)
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title
                }).ToListAsync();

            return Json(sections);
        }



        [HttpGet]
        public async Task<JsonResult> GetLessonsBySection(int sectionId)
        {
            var lessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .Select(l => new { l.Id, l.Title })
                .ToListAsync();

            return Json(lessons);
        }


        [HttpGet]
        public async Task<IActionResult> GetUnitsBySection(int sectionId)
        {
            var units = await _context.SectionUnits
                .Where(su => su.SectionId == sectionId)
                .Select(su => new
                {
                    id = su.UnitId,
                    title = su.Unit.Title
                }).ToListAsync();

            return Json(units);
        }

        [HttpGet]
        public async Task<IActionResult> GetLessonsByUnit(int unitId)
        {
            var lessons = await _context.Lessons
                .Where(l => l.UnitId == unitId)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title
                }).ToListAsync();

            return Json(lessons);
        }



        [HttpGet]
        public async Task<IActionResult> ConfirmHomework(int batchId)
        {
            var batch = await _context.Batches
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound();

            var lessonIds = await _context.BatchLessonCompletions
                .Where(b => b.BatchId == batchId)
                .Select(b => b.LessonId)
                .ToListAsync();

            if (!lessonIds.Any())
            {
                TempData["Error"] = "لا توجد مؤشرات مسجلة لهذه الدفعة.";
                return RedirectToAction("SelectLessons");
            }

            var allLessons = await _context.Lessons.ToListAsync();
            var lessons = (
                           from l in allLessons
                           join id in lessonIds on l.Id equals id
                           select l
                           ).ToList();

            // ✅ أخذ أول SectionId من الدروس (الآن الدرس يحتوي على SectionId مباشر)
            var firstSectionId = lessons.FirstOrDefault()?.SectionId ?? 0;

            var curriculumId = await _context.Sections
                .Where(s => s.Id == firstSectionId)
                .Select(s => s.CurriculumId)
                .FirstOrDefaultAsync();

            var vm = new QdratNew.ViewModels.Homework.ConfirmHomeworkViewModel
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CurriculumId = curriculumId,
                Lessons = new List<QdratNew.ViewModels.Homework.LessonSummaryViewModel>()
            };

            foreach (var lesson in lessons)
            {
                var allQuestions = await _context.Questions
                    .Where(q => q.LessonId == lesson.Id)
                    .ToListAsync();

                var reviewed = allQuestions.Where(q => q.IsReviewed).ToList();

                vm.Lessons.Add(new QdratNew.ViewModels.Homework.LessonSummaryViewModel
                {
                    LessonId = lesson.Id,
                    LessonTitle = lesson.Title,
                    TotalQuestions = allQuestions.Count,
                    ReviewedQuestions = reviewed.Count,
                    QuestionsToUse = reviewed.Count > 0 ? Math.Min(reviewed.Count, 3) : 0,
                    SectionId = lesson.SectionId // ✅ مباشرة من الدرس
                });
            }

            return View("ConfirmHomework", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmHomework(QdratNew.ViewModels.Homework.ConfirmHomeworkViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var allErrors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => $"⚠️ {x.Key}: {x.Value.Errors.First().ErrorMessage}")
                    .ToList();

                TempData["Error"] = string.Join("<br/>", allErrors);
                return RedirectToAction("ConfirmHomework", new { batchId = model.BatchId });
            }

            var batch = await _context.Batches.FindAsync(model.BatchId);
            if (batch == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الدفعة المطلوبة.";
                return RedirectToAction("SelectLessons");
            }

            var students = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == model.BatchId)
                .ToListAsync();

            if (students.Count == 0)
            {
                TempData["Error"] = "⚠️ لا يوجد طلاب في هذه الدفعة.";
                return RedirectToAction("SelectLessons");
            }

            var homeworkSet = new HomeworkSet
            {
                Title = $"واجب {batch.Name} - {DateTime.Now:yyyy/MM/dd}",
                BatchId = batch.Id,
                CurriculumId = model.CurriculumId,
                CompletionTitle = model.CompletionTitle,
                AssignedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = DateTime.Now
            };

            _context.HomeworkSets.Add(homeworkSet);
            await _context.SaveChangesAsync();

            // تسجيل المؤشرات المنتهية لكل طالب
            var lessonIdList = model.Lessons.Select(l => l.LessonId).ToList();

            foreach (var student in students)
            {
                var existing = await _context.StudentLessonCompletions
                    .Where(s => s.StudentId == student.StudentID)
                    .Select(s => new { s.StudentId, s.LessonId })
                    .ToListAsync();

                var toAdd = (from lid in lessonIdList
                             join e in existing on lid equals e.LessonId into gj
                             from sub in gj.DefaultIfEmpty()
                             where sub == null
                             select lid).ToList();

                foreach (var lessonId in toAdd)
                {
                    _context.StudentLessonCompletions.Add(new StudentLessonCompletion
                    {
                        StudentId = student.StudentID,
                        LessonId = lessonId,
                        CompletionDate = DateTime.Now
                    });
                }
            }
            await _context.SaveChangesAsync();

            int totalAssigned = 0;
            var usedQuestionIdsInSet = new HashSet<Guid>();

            foreach (var lesson in model.Lessons)
            {
                int questionsCount = lesson.IsManualSelection ?? false
                    ? lesson.ManuallySelectedQuestionsCount
                    : lesson.QuestionsToUse;

                if (questionsCount == 0)
                    continue;

                var reviewedQuestions = await _context.Questions
                    .Where(q => q.LessonId == lesson.LessonId && q.IsReviewed && q.IsComplete && !q.IsRejected)
                    .OrderBy(q => Guid.NewGuid())
                    .ToListAsync();

                if (!reviewedQuestions.Any())
                    continue;

                var lectureId = await _context.Lecture
                    .Where(l => l.BatchId == model.BatchId && l.SectionId == lesson.SectionId)
                    .OrderByDescending(l => l.Date)
                    .Select(l => (int?)l.Id)
                    .FirstOrDefaultAsync();

                foreach (var student in students)
                {
                    var filteredQuestions = reviewedQuestions
                      .OrderBy(q => Guid.NewGuid())
                      .Take(questionsCount)
                      .ToList();



                    foreach (var question in filteredQuestions)
                    {
                        _context.Homeworks.Add(new Homework
                        {
                            StudentId = student.StudentID,
                            LessonId = lesson.LessonId,
                            QuestionId = question.Id,
                            AssignedAt = DateTime.Now,
                            Status = HomeworkStatus.Pending,
                            HomeworkSetId = homeworkSet.Id,
                            LectureId = lectureId
                        });

                        usedQuestionIdsInSet.Add(question.Id);
                        totalAssigned++;
                    }
                }
            }

            if (totalAssigned < 20 && !model.ForceGenerateAnyway)
            {
                TempData["Error"] = $"⚠️ تم تعيين فقط {totalAssigned} سؤالًا. يجب تعيين على الأقل 20 سؤال.";
                TempData["AllowForceGenerate"] = true;
                TempData["CompletedLessonIds"] = string.Join(",", model.Lessons.Select(l => l.LessonId));
                return RedirectToAction("ConfirmHomework", new { batchId = model.BatchId });
            }

            await _context.SaveChangesAsync();

            // توليد اختبار المحور تلقائيًا عند اكتمال المؤشرات لكل طالب
            var allLessons = await _context.Lessons.ToListAsync();

            var sectionIds = (
                from l in allLessons
                join lid in lessonIdList on l.Id equals lid
                select l.SectionId
            ).Distinct().ToList();



            foreach (var student in students)
            {
                var lessonIds = model.Lessons.Select(x => x.LessonId).ToList();


                var studentSectionIds = (
                    from l in allLessons
                    join id in lessonIds on l.Id equals id
                    select l.SectionId
                ).Distinct().ToList();


                foreach (var sectionId in studentSectionIds)
                {
                    var allLessonIds = await _context.Lessons
      .Where(l => l.SectionId == sectionId && l.IsActive)
      .Select(l => l.Id)
      .ToListAsync();


                    // استعلام باستخدام Join بدل Contains
                    var allCompletions = await _context.StudentLessonCompletions
       .Where(slc => slc.StudentId == student.StudentID)
       .ToListAsync();

                    var studentLessons = allCompletions
                        .Where(slc => allLessonIds.Contains(slc.LessonId))  // هذا مقبول في الذاكرة
                        .Select(slc => slc.LessonId)
                        .Distinct()
                        .ToList();


                    if (studentLessons.Count == allLessons.Count)
                    {
                        await _examGeneratorService.CheckAndGenerateExamAsync(student.StudentID, sectionId);
                    }
                }
            }



            foreach (var student in students)
            {
                _context.Notifications.Add(new Notification
                {
                    StudentID = student.StudentID,
                    Message = "📌 تم إضافة واجب جديد لك في المنصة. يرجى الدخول على لوحة التحكم للاطلاع عليه.",
                    SentAt = DateTime.Now,
                    Category = NotificationCategory.Reminder
                });
            }

            await _context.SaveChangesAsync();

            _context.Notifications.Add(new Notification
            {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Message = $"📢 تم إرسال واجب جديد للدفعة: {batch.Name}",
                SentAt = DateTime.Now,
                Category = NotificationCategory.Important
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم توليد الواجبات بنجاح.";
            return RedirectToAction("Details", "Batches", new { id = model.BatchId });
        }

     


        [HttpGet]
        public async Task<IActionResult> GetCurriculaByBatch(int batchId)
        {
            var courseId = await _context.Batches
                .Where(b => b.Id == batchId)
                .Select(b => b.CourseId)
                .FirstOrDefaultAsync();

            var curricula = await _context.CourseCurriculums
                .Where(cc => cc.CourseId == courseId)
                .Select(cc => new
                {
                    id = cc.Curriculum.Id,
                    title = cc.Curriculum.Title
                })
                .ToListAsync();


            return Json(curricula);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBatches()
        {
            var batches = await _context.Batches
                .Select(b => new { id = b.Id, name = b.Name })
                .ToListAsync();
            return Json(batches);
        }

        [HttpGet]
        public async Task<IActionResult> CompletedLessonsDetails(int batchId, string completionTitle)
        {
            completionTitle = Uri.UnescapeDataString(completionTitle ?? "").Trim();

            // 🟦 جلب الطلاب
            var studentsRaw = await (
        from e in _context.StudentBatchEnrollments
        join s in _context.Students on e.StudentID equals s.StudentID
        join u in _userManager.Users on s.UserId equals u.Id into JU
        from u in JU.DefaultIfEmpty()
        where e.BatchId == batchId
        orderby (u != null ? u.FullName : s.FullName)
        select new
        {
            StudentId = s.StudentID,
            FullName = (u != null ? u.FullName : s.FullName)
        }
    ).ToListAsync();


            var studentIds = studentsRaw.Select(s => s.StudentId).ToList();

            // 🟦 جلب المؤشرات الفعالة المرتبطة بعنوان التسجيل
            var lessonsRaw = await (
                from c in _context.BatchLessonCompletions
                join l in _context.Lessons on c.LessonId equals l.Id
                where c.BatchId == batchId &&
                      c.CompletionTitle == completionTitle &&
                      l.IsActive
                select new { l.Id }
            ).ToListAsync();

            var lessonIds = lessonsRaw.Select(x => x.Id).ToList();

            if (!lessonIds.Any())
            {
                return View(new CompletedLessonsDetailsViewModel
                {
                    BatchId = batchId,
                    CompletionTitle = completionTitle,
                    Students = new List<StudentCompletionDetailViewModel>()
                });
            }

            // 🟦 جلب الواجبات باستخدام Join بدل Contains
            // 🟧 جلب كل الواجبات المرتبطة بالدفعة والمؤشرات (دون Contains أو Join على List)
            var allHomeworks = await (
       from h in _context.Homeworks
       select new
       {
           h.StudentId,
           h.LessonId,
           h.StudentAnswer,
           h.IsCorrect
       }
   ).ToListAsync();

            var homeworksRaw = allHomeworks
    .Where(h =>
        studentIds.Contains(h.StudentId) &&
        h.LessonId.HasValue &&
        lessonIds.Contains(h.LessonId.Value)
    )
    .ToList();


            // 🟦 تجهيز البيانات
            var result = studentsRaw.Select(student =>
            {
                var studentHw = homeworksRaw.Where(h => h.StudentId == student.StudentId).ToList();
                int total = studentHw.Count;
                int answered = studentHw.Count(h => !string.IsNullOrEmpty(h.StudentAnswer));
                int correct = studentHw.Count(h => h.IsCorrect == true);

                double percentage = total > 0 ? Math.Round((correct * 100.0) / total, 1) : 0;

                string status = total == 0 ? "❌ لم يحل" :
                                percentage >= 70 ? "✅ ملتزم" : "⚠️ ضعيف";

                return new StudentCompletionDetailViewModel
                {
                    StudentId = student.StudentId,
                    StudentName = student.FullName,
                    QuestionCount = total,
                    AnsweredCount = answered,
                    CorrectCount = correct,
                    Percentage = percentage,
                    Status = status
                };
            }).ToList();

            return View(new CompletedLessonsDetailsViewModel
            {
                BatchId = batchId,
                CompletionTitle = completionTitle,
                Students = result
            });
        }

        [HttpGet]
        public async Task<IActionResult> HomeworkDetails(int studentId, int batchId, string completionTitle)
        {
            completionTitle = Uri.UnescapeDataString(completionTitle ?? "").Trim();

            // ✅ جلب معرفات الدروس الفعالة المرتبطة بالتسجيل
            var lessonIds = await (
                from blc in _context.BatchLessonCompletions
                join l in _context.Lessons on blc.LessonId equals l.Id
                where blc.BatchId == batchId
                      && blc.CompletionTitle == completionTitle
                      && l.IsActive
                select blc.LessonId
            ).ToListAsync();

            if (!lessonIds.Any())
            {
                return View(new CompletedLessonsDetailsViewModel
                {
                    BatchId = batchId,
                    CompletionTitle = completionTitle,
                    HomeworkDetails = new List<StudentHomeworkDetailViewModel>()
                });
            }

            // ✅ جلب كافة الواجبات للطالب (ونقوم بتصفية الدروس بعدين لتجنب استخدام Contains داخل SQL)
            var allHomeworks = await (
                from h in _context.Homeworks
                join q in _context.Questions on h.QuestionId equals q.Id
                where h.StudentId == studentId
                select new
                {
                    h.LessonId,
                    q.Id,
                    q.Title,
                    q.CorrectAnswer,
                    h.StudentAnswer,
                    h.IsCorrect
                }
            ).ToListAsync();

            var filtered = allHomeworks
     .Where(h =>
         h.LessonId.HasValue &&               // 🔒 تأكيد وجود Lesson
         lessonIds.Contains(h.LessonId.Value) // 🔒 مقارنة صحيحة
     )
     .Select(h => new StudentHomeworkDetailViewModel
     {
         QuestionId = h.Id,
         QuestionText = h.Title,
         StudentAnswer = h.StudentAnswer ?? "❌ لم يُجب",
         CorrectAnswer = h.CorrectAnswer,
         IsCorrect = h.IsCorrect ?? false
     })
     .ToList();

            var model = new CompletedLessonsDetailsViewModel
            {
                BatchId = batchId,
                CompletionTitle = completionTitle,
                StudentName = await _context.Students.Where(s => s.StudentID == studentId).Select(s => s.FullName).FirstOrDefaultAsync(),
                BatchName = await _context.Batches.Where(b => b.Id == batchId).Select(b => b.Name).FirstOrDefaultAsync(),
                QuestionCount = filtered.Count,
                AnsweredCount = filtered.Count(h => !string.IsNullOrEmpty(h.StudentAnswer)),
                CorrectCount = filtered.Count(h => h.IsCorrect),
                Percentage = filtered.Count > 0 ? Math.Round((filtered.Count(h => h.IsCorrect) * 100.0) / filtered.Count, 1) : 0,
                Status = filtered.Count == 0 ? "❌ لم يُجب" : (filtered.Count(h => h.IsCorrect) * 100.0 / filtered.Count >= 70 ? "✅ ملتزم" : "⚠️ ضعيف"),
                HomeworkDetails = filtered
            };


            return View(model);
        }



        [HttpGet]
        public async Task<IActionResult> GetCompletedLessonsDetailsData(int batchId, string completionTitle)
        {
            completionTitle = Uri.UnescapeDataString(completionTitle ?? "").Trim();

            // ✅ الطلاب داخل الدفعة
            var students = await (
            from e in _context.StudentBatchEnrollments
            join s in _context.Students on e.StudentID equals s.StudentID
            where e.BatchId == batchId
            select new
            {
                StudentId = s.StudentID,
                FullName = s.FullName
            }
        ).ToListAsync();

            var studentIdsList = students.Select(s => s.StudentId).ToList();

            // ✅ المؤشرات المرتبطة بعنوان التسجيل والدروس الفعالة فقط
            var lessonIdsList = await (
                from c in _context.BatchLessonCompletions
                join l in _context.Lessons on c.LessonId equals l.Id
                where c.BatchId == batchId
                      && c.CompletionTitle == completionTitle
                      && l.IsActive
                select c.LessonId
            ).ToListAsync();

            if (!lessonIdsList.Any() || !studentIdsList.Any())
            {
                return Json(new List<object>()); // ✅ لا بيانات
            }

            // ✅ جلب الواجبات المرتبطة بالمؤشرات والطلاب عبر join يدوي
            var allHomeworks = await _context.Homeworks
                .Select(h => new
                {
                    h.StudentId,
                    h.LessonId,
                    h.StudentAnswer,
                    h.IsCorrect
                })
                .ToListAsync();

            var homeworks = allHomeworks
       .Where(h =>
           studentIdsList.Contains(h.StudentId) &&
           h.LessonId.HasValue &&
           lessonIdsList.Contains(h.LessonId.Value)
       )
       .ToList();


            // ✅ تجهيز النتائج
            var result = students.Select(student =>
            {
                var studentHw = homeworks.Where(h => h.StudentId == student.StudentId).ToList();
                int total = studentHw.Count;
                int answered = studentHw.Count(h => !string.IsNullOrWhiteSpace(h.StudentAnswer));
                int correct = studentHw.Count(h => h.IsCorrect == true);

                double percentage = total > 0 ? Math.Round((correct * 100.0) / total, 1) : 0;

                string status = total == 0 ? "❌ لم يحل"
                                : percentage >= 70 ? "✅ ملتزم"
                                : "⚠️ ضعيف";

                return new
                {
                    studentName = student.FullName,
                    totalQuestions = total,
                    answeredCount = answered,
                    correctCount = correct,
                    scorePercent = percentage,
                    status = status
                };
            }).ToList();

            return Json(result);
        }


        [HttpGet]
        public async Task<IActionResult> SendReminderForIncompleteHomework(int batchId, string completionTitle)
        {
            var lessonIds = await _context.BatchLessonCompletions
                .Where(x => x.BatchId == batchId && x.CompletionTitle == completionTitle)
                .Select(x => x.LessonId)
                .ToListAsync();

            var students = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == batchId)
                .ToListAsync();

            var homeworks = await (
          from h in _context.Homeworks
          join s in _context.StudentBatchEnrollments on h.StudentId equals s.StudentID
          join lid in lessonIds on h.LessonId equals lid
          where s.BatchId == batchId
          select h
      ).ToListAsync();

            int notifiedCount = 0;

            foreach (var student in students)
            {
                var studentHomeworks = homeworks.Where(h => h.StudentId == student.StudentID).ToList();
                var total = studentHomeworks.Count();
                var answered = studentHomeworks.Count(h =>
                    h.Status == HomeworkStatus.Submitted || h.Status == HomeworkStatus.Reviewed
                );

                if (total > 0 && answered < total)
                {
                    _context.Notifications.Add(new Notification
                    {
                        StudentID = student.StudentID,
                        Message = $"⚠️ لديك واجب غير مكتمل مرتبط بالمؤشرات المنتهية: {completionTitle}. يرجى الدخول فورًا وحله.",
                        SentAt = DateTime.Now,
                        Category = NotificationCategory.Reminder
                    });
                    notifiedCount++;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = notifiedCount > 0, count = notifiedCount });
        }





        [HttpGet]
        public async Task<IActionResult> CompletedLessonsReport(int? batchId)
        {
            var rawData = await (
                from c in _context.BatchLessonCompletions
                join b in _context.Batches on c.BatchId equals b.Id
                join l in _context.Lessons on c.LessonId equals l.Id
                where !batchId.HasValue || c.BatchId == batchId.Value
                select new
                {
                    c.BatchId,
                    BatchName = b.Name,
                    c.CompletionTitle,
                    c.CompletionDate,
                    c.AddedBy
                }
            ).ToListAsync(); // ⬅️ كل العمليات التالية ستكون على الذاكرة

            var usernames = rawData
                .Select(x => x.AddedBy?.Split(":").LastOrDefault()?.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();

            var allUsers = await _context.Users
                .Select(u => new { u.UserName, FullName = u.FullName ?? "" })
                .ToListAsync();

            var userDict = allUsers
                .Where(u => usernames.Contains(u.UserName))
                .ToDictionary(u => u.UserName, u => u.FullName);

            var result = rawData
                .GroupBy(x => new { x.BatchId, x.BatchName, x.CompletionTitle, x.AddedBy })
                .Select(g =>
                {
                    var addedByUsername = g.Key.AddedBy?.Split(":").LastOrDefault()?.Trim();
                    userDict.TryGetValue(addedByUsername ?? "", out var fullName);

                    return new BatchCompletedLessonsReportViewModel
                    {
                        BatchId = g.Key.BatchId,
                        BatchName = g.Key.BatchName,
                        CompletionTitle = g.Key.CompletionTitle,
                        AddedBy = g.Key.AddedBy,
                        AddedByName = fullName ?? "غير معروف",
                        CompletedLessonCount = g.Count(),
                        CompletionDate = g.Max(x => x.CompletionDate)
                    };
                }).ToList();

            return View("CompletedLessonsReport", result);
        }




        [HttpGet]
        public async Task<IActionResult> HomeworkSummary(int batchId)
        {
            var batch = await _context.Batches
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound();

            var completions = await _context.BatchLessonCompletions
                .Where(c => c.BatchId == batchId)
                .Include(c => c.Lesson)
                .ThenInclude(l => l.Section)
                .ToListAsync();

            var usernames = completions
                .Select(c => c.AddedBy?.Split(":").LastOrDefault()?.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();

            string instructor = "غير معروف";
            var instructorRaw = await _context.Users
                .Where(u => usernames.Contains(u.UserName))
                .Select(u => u.FullName)
                .Where(name => name != null)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(instructorRaw))
                instructor = instructorRaw;

            var students = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == batchId)
                .ToListAsync();

            var studentIds = students.Select(s => s.StudentID).ToList();

            var homeworkStatsRaw = await _context.Homeworks
                .Where(h => studentIds.Contains(h.StudentId) &&
                    (h.Status == HomeworkStatus.Submitted || h.Status == HomeworkStatus.Reviewed))
                .GroupBy(h => h.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    TotalSolved = g.Count(),
                    TotalScore = g.Sum(x => (double?)x.Score) ?? 0
                })
                .ToListAsync();

            var homeworkStats = homeworkStatsRaw
                .Select(g => new
                {
                    StudentId = g.StudentId,
                    Solved = g.TotalSolved > 0,
                    AvgScore = g.TotalSolved > 0 ? g.TotalScore / g.TotalSolved : (double?)null
                })
                .ToList();
            var studentsWithNames = await (
                                            from e in _context.StudentBatchEnrollments
                                            join st in _context.Students on e.StudentID equals st.StudentID
                                            where e.BatchId == batch.Id
                                            select new
                                            {
                                                StudentId = st.StudentID,
                                                FullName = st.FullName
                                            }
                                        ).ToListAsync();


            var vm = new BatchHomeworkSummaryViewModel
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CompletionTitle = completions.FirstOrDefault()?.CompletionTitle ?? "بدون عنوان",
                CompletionDate = completions.FirstOrDefault()?.CompletionDate ?? DateTime.MinValue,
                InstructorName = instructor,
                Lessons = completions.Select(c => new LessonSummary
                {
                    LessonTitle = c.Lesson.Title,
                    SectionTitle = c.Lesson.Section.Title
                }).ToList(),
                Students = studentsWithNames.Select(s =>
                {
                    var stat = homeworkStats.FirstOrDefault(h => h.StudentId == s.StudentId);
                    return new StudentHomeworkStatus
                    {
                        StudentName = s.FullName,
                        HasSolved = stat?.Solved ?? false,
                        Score = stat?.AvgScore
                    };
                }).ToList()
            };


            return View("HomeworkSummary", vm);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateHomeworkSetJson([FromBody] RegenerateHomeworkDto dto)
        {
            if (dto == null || dto.BatchId == 0 || string.IsNullOrWhiteSpace(dto.CompletionTitle))
                return Json(new { success = false, message = "البيانات غير صالحة." });
            Console.WriteLine($"🔍 CompletionTitle Received: {dto.CompletionTitle}");

            try
            {
                // جلب الدروس المرتبطة بعنوان التسجيل باستخدام Join فقط
                var lessonJoin = await (
                    from blc in _context.BatchLessonCompletions
                    join l in _context.Lessons on blc.LessonId equals l.Id
                    where blc.BatchId == dto.BatchId && blc.CompletionTitle == dto.CompletionTitle
                    select new
                    {
                        l.Id,
                        l.SectionId
                    }
                ).ToListAsync();

                var lessonIds = lessonJoin.Select(x => x.Id).ToList();

                if (!lessonIds.Any())
                    return Json(new { success = false, message = "لا توجد مؤشرات لإعادة الإرسال." });

                var firstSectionId = lessonJoin.First().SectionId;

                var curriculumId = await _context.Sections
                    .Where(s => s.Id == firstSectionId)
                    .Select(s => s.CurriculumId)
                    .FirstOrDefaultAsync();
                var vm = new QdratNew.ViewModels.Homework.ConfirmHomeworkViewModel
                {
                    BatchId = dto.BatchId,
                    CurriculumId = curriculumId,
                    CompletionTitle = dto.CompletionTitle, // ✅ أضف هذا السطر
                    Lessons = lessonIds.Select(id => new QdratNew.ViewModels.Homework.LessonSummaryViewModel
                    {
                        LessonId = id,
                        IsManualSelection = false
                    }).ToList(),
                    ForceGenerateAnyway = true
                };


                // استدعاء داخلي لأكشن ConfirmHomework
                var result = await ConfirmHomework(vm);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.InnerException?.Message ?? ex.Message
                });
            }

        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateHomeworkSetForBatch(int batchId, string completionTitle)
        {
            // جلب جميع المؤشرات المرتبطة بعنوان التسجيل والدفعة
            var lessonIds = await _context.BatchLessonCompletions
                .Where(x => x.BatchId == batchId && x.CompletionTitle == completionTitle)
                .Select(x => x.LessonId)
                .ToListAsync();

            if (!lessonIds.Any())
            {
                TempData["Error"] = "❌ لا توجد مؤشرات مسجلة لإعادة إرسال الواجب.";
                return RedirectToAction("CompletedLessonsReport", new { batchId });
            }

            // جلب أول Section مرتبط بالدروس لتحديد Curriculum
            var firstSectionId = await _context.Lessons
                .Where(l => lessonIds.Contains(l.Id))
                .Select(l => l.SectionId)
                .FirstOrDefaultAsync();

            var curriculumId = await _context.Sections
                .Where(s => s.Id == firstSectionId)
                .Select(s => s.CurriculumId)
                .FirstOrDefaultAsync();

            var vm = new QdratNew.ViewModels.Homework.ConfirmHomeworkViewModel
            {
                BatchId = batchId,
                CurriculumId = curriculumId,
                Lessons = lessonIds.Select(id => new QdratNew.ViewModels.Homework.LessonSummaryViewModel
                {
                    LessonId = id,
                    IsManualSelection = false // ⬅️ الاعتماد على الأسئلة المراجَعة تلقائيًا
                }).ToList(),
                ForceGenerateAnyway = true // ⬅️ لتجاوز شرط 20 سؤال إن لزم
            };

            // نداء ConfirmHomework مباشرة
            return await ConfirmHomework(vm);
        }





        [HttpGet]
        public async Task<IActionResult> GetCompletedLessonsReportData(int? batchId, DateTime? fromDate)
        {
            // Step 1: قراءة البيانات الخام من المؤشرات المكتملة
            var rawCompletions = await (
                from c in _context.BatchLessonCompletions
                join b in _context.Batches on c.BatchId equals b.Id
                join l in _context.Lessons on c.LessonId equals l.Id
                select new
                {
                    c.BatchId,
                    BatchName = b.Name,
                    c.CompletionTitle,
                    c.CompletionDate,
                    AddedBy = c.AddedBy
                }
            ).ToListAsync();

            // Step 2: فلترة بالدفعة والتاريخ
            if (batchId.HasValue)
                rawCompletions = rawCompletions.Where(x => x.BatchId == batchId.Value).ToList();

            if (fromDate.HasValue)
                rawCompletions = rawCompletions.Where(x => x.CompletionDate >= fromDate.Value).ToList();

            // Step 3: استخراج أسماء المستخدمين من الـ AddedBy
            var addedByUsernames = rawCompletions
                .Select(x => x.AddedBy?.Split(":").LastOrDefault()?.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();

            // Step 4: جلب كل المستخدمين مرة واحدة ثم فلترة في الذاكرة
            var allUsers = await _context.Users
                .Select(u => new { u.UserName, FullName = u.FullName ?? "" })
                .ToListAsync();

            // Step 5: بناء Dictionary يدويًا دون استخدام Contains داخل الاستعلام
            var userDict = allUsers
                .Where(u => addedByUsernames.Any(name => name == u.UserName)) // فلترة بالذاكرة
                .ToDictionary(u => u.UserName, u => u.FullName);

            // Step 6: بناء النتيجة النهائية
            var result = rawCompletions
                .GroupBy(x => new { x.BatchId, x.BatchName, x.CompletionTitle, x.AddedBy })
                .Select(g =>
                {
                    var addedByUsername = g.Key.AddedBy?.Split(":").LastOrDefault()?.Trim();
                    userDict.TryGetValue(addedByUsername ?? "", out var fullName);

                    return new
                    {
                        batchId = g.Key.BatchId,
                        batchName = g.Key.BatchName,
                        completionTitle = g.Key.CompletionTitle,
                        addedBy = g.Key.AddedBy,
                        addedByName = fullName ?? g.Key.AddedBy,
                        completedLessonCount = g.Count(),
                        completionDate = g.Max(x => x.CompletionDate)
                    };
                }).ToList();

            return Json(result);
        }




    }
}
