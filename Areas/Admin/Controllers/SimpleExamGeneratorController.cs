using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SimpleMassExamGeneratorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SimpleMassExamGeneratorController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string Safe(string s) => (s ?? "").Replace(";", "").Replace("--", "").Trim();


        // ===============================
        // GET: تحميل الدفعات + النماذج
        // ===============================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new SimpleMassExamViewModel
            {
                Batches = await _context.Batches
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync()
            };

            vm.ModelSelections = await _context.ProfessionalModels
                .Where(m => !m.IsArchived && m.ModelType == QdratNew.Enums.ProfessionalModelType.Exam)
                .OrderBy(m => m.Title)
                .Select(m => new ProfessionalModelSelectionVm
                {
                    ModelId = m.Id,
                    ModelTitle = m.Title,
                    QuestionCount = 0
                })
                .ToListAsync();
            ViewBag.Models = await _context.ProfessionalModels
                .Where(m => !m.IsArchived && m.ModelType == QdratNew.Enums.ProfessionalModelType.Exam)
                .OrderBy(m => m.Title)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Title })
                .ToListAsync();

            return View(vm);
        }


        [HttpPost]
        public async Task<IActionResult> PreviewQuestions([FromBody] List<ModelSelectionPreview> items)
        {
            var final = new List<Question>();

            foreach (var item in items)
            {
                var qs = await _context.ProfessionalModelQuestions
                    .Where(m => m.ModelId == item.ModelId && !m.Model.IsArchived)
                    .Select(m => m.Question)
                    .Take(item.Count)
                    .ToListAsync();

                final.AddRange(qs);
            }

            return PartialView("_ExamPreview", final);
        }


        // ===============================
        // API: تحميل الطلاب حسب الدفعة
        // ===============================
        [HttpGet]
        public async Task<IActionResult> LoadStudents(int batchId)
        {
            var students = await _context.StudentBatchEnrollments
                .Where(x => x.BatchId == batchId)
                .Include(x => x.Student)
                .Select(x => new
                {
                    id = x.StudentID,
                    name = x.Student.FullName
                })
                .ToListAsync();

            return Json(students);
        }


        // =============================================
        // POST: إرسال الاختبار لعدة طلاب باختبار مستقل
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SimpleMassExamViewModel vm)
        {
            try
            {
                if (vm.SelectedStudentIds == null || vm.SelectedStudentIds.Count == 0)
                {
                    TempData["Error"] = "يجب اختيار الطلاب.";
                    return RedirectToAction("Create");
                }

                int studentsCount = vm.SelectedStudentIds.Count;
                int totalRequested = vm.ModelSelections.Sum(x => x.QuestionCount);

                if (totalRequested == 0)
                {
                    TempData["Error"] = "حدد عدد الأسئلة المطلوبة من النماذج.";
                    return RedirectToAction("Create");
                }

                // ==============================
                // 🔵 سيناريو A: من 1 إلى 10 طلاب → Exam منفصل لكل طالب
                // ==============================
                if (studentsCount <= 10)
                {
                    foreach (var studentId in vm.SelectedStudentIds)
                    {
                        // 1) Exam جديد لهذا الطالب فقط
                        var exam = new Exam
                        {
                            Title = vm.Title,
                            Type = ExamType.Course,
                            DurationMinutes = vm.DurationMinutes,
                            TotalQuestions = totalRequested,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };

                        _context.Exams.Add(exam);
                        await _context.SaveChangesAsync();

                        // 2) Assignment لهذا الطالب
                        var assignment = new ExamAssignmentToBatch
                        {
                            ExamId = exam.Id,

                            // 🔥 المفتاح الحقيقي لمنع إرسال الاختبار للدفعة
                            BatchId = 0, // ← هذا يجعل الامتحان غير مرتبط بأي دفعة

                            Title = exam.Title,
                            DurationMinutes = vm.DurationMinutes,
                            TotalQuestions = totalRequested,
                            ScheduledDate = vm.StartAt ?? DateTime.UtcNow,
                            EndAt = vm.EndAt ?? DateTime.UtcNow.AddMinutes(vm.DurationMinutes),
                            IsOnline = true,
                            IsSentToStudents = true
                        };

                        _context.ExamAssignmentsToBatches.Add(assignment);
                        await _context.SaveChangesAsync();

                        // 3) الأسئلة من النماذج
                        List<Guid> finalQuestions = new();

                        foreach (var sel in vm.ModelSelections.Where(x => x.QuestionCount > 0))
                        {
                            var q = await _context.ProfessionalModelQuestions
                                .Where(m => m.ModelId == sel.ModelId)
                                .Select(m => m.QuestionId.Value)
                                .OrderBy(x => Guid.NewGuid())
                                .Take(sel.QuestionCount)
                                .ToListAsync();

                            finalQuestions.AddRange(q);
                        }

                        // 4) إضافة الأسئلة
                        int order = 1;
                        foreach (var qid in finalQuestions)
                        {
                            _context.ExamQuestions.Add(new ExamQuestion
                            {
                                ExamId = exam.Id,
                                ExamAssignmentId = assignment.Id,
                                QuestionId = qid,
                                Order = order++
                            });
                        }

                        await _context.SaveChangesAsync();

                        // 5) إضافة ExamStudentStatus
                        _context.ExamStudentStatuses.Add(new ExamStudentStatus
                        {
                            StudentId = studentId,
                            ExamId = exam.Id,
                            ExamAssignmentId = assignment.Id,
                            AssignedAt = DateTime.UtcNow,
                            Status = ExamStatus.Pending,
                            IsSubmitted = false
                        });

                        await _context.SaveChangesAsync();
                    }

                    TempData["Success"] = "✔ تم إرسال اختبارات منفصلة للطلاب المختارين.";
                    return RedirectToAction("Create");
                }

                // ==============================
                // 🟢 سيناريو B: أكثر من 10 طلاب → Exam واحد مشترك للجميع
                // ==============================

                // 1) Exam واحد فقط
                var sharedExam = new Exam
                {
                    Title = vm.Title + " (اختبار مشترك)",
                    Type = ExamType.Course,
                    DurationMinutes = vm.DurationMinutes,
                    TotalQuestions = totalRequested,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Exams.Add(sharedExam);
                await _context.SaveChangesAsync();

                // 2) Assignment واحد
                var sharedAssignment = new ExamAssignmentToBatch
                {
                    ExamId = sharedExam.Id,
                    BatchId = vm.BatchId,
                    Title = sharedExam.Title,
                    DurationMinutes = vm.DurationMinutes,
                    TotalQuestions = totalRequested,
                    ScheduledDate = vm.StartAt ?? DateTime.UtcNow,
                    EndAt = vm.EndAt ?? DateTime.UtcNow.AddMinutes(vm.DurationMinutes),
                    IsOnline = true,
                    IsSentToStudents = true
                };

                _context.ExamAssignmentsToBatches.Add(sharedAssignment);
                await _context.SaveChangesAsync();

                // 3) الأسئلة من النماذج (مرة واحدة)
                List<Guid> sharedQuestions = new();

                foreach (var sel in vm.ModelSelections.Where(x => x.QuestionCount > 0))
                {
                    var q = await _context.ProfessionalModelQuestions
                        .Where(m => m.ModelId == sel.ModelId)
                        .Select(m => m.QuestionId.Value)
                        .OrderBy(x => Guid.NewGuid())
                        .Take(sel.QuestionCount)
                        .ToListAsync();

                    sharedQuestions.AddRange(q);
                }

                int sharedOrder = 1;
                foreach (var qid in sharedQuestions)
                {
                    _context.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamId = sharedExam.Id,
                        ExamAssignmentId = sharedAssignment.Id,
                        QuestionId = qid,
                        Order = sharedOrder++
                    });
                }


           


                await _context.SaveChangesAsync();

                // 4) إنشاء ExamStudentStatus لكل طالب
                foreach (var studentId in vm.SelectedStudentIds)
                {
                    _context.ExamStudentStatuses.Add(new ExamStudentStatus
                    {
                        StudentId = studentId,
                        ExamId = sharedExam.Id,
                        ExamAssignmentId = sharedAssignment.Id,
                        AssignedAt = DateTime.UtcNow,
                        Status = ExamStatus.Pending,
                        IsSubmitted = false
                    });
                }

                await _context.SaveChangesAsync();
                if (studentsCount <= 10)
                {
                    TempData["Success"] =
                        $"✔ تم إنشاء {studentsCount} اختبارًا منفصلًا للطلاب المختارين.<br>" +
                        $"🧩 عدد الأسئلة: {totalRequested}.<br>" +
                        $"⏱️ وقت البدء: {vm.StartAt?.ToString("yyyy-MM-dd HH:mm")}<br>" +
                        $"⏳ وقت الانتهاء: {vm.EndAt?.ToString("yyyy-MM-dd HH:mm")}<br>" +
                        $"🔒 أسئلة مختلفة لكل طالب لمنع الغش.";

                    return RedirectToAction("Create");
                }
                else
                {
                    TempData["Success"] =
                        $"✔ تم إنشاء اختبار واحد مشترك لجميع الطلاب ({studentsCount} طالبًا).<br>" +
                        $"🧩 عدد الأسئلة: {totalRequested}.<br>" +
                        $"⏱️ وقت البدء: {vm.StartAt?.ToString("yyyy-MM-dd HH:mm")}<br>" +
                        $"⏳ وقت الانتهاء: {vm.EndAt?.ToString("yyyy-MM-dd HH:mm")}<br>" +
                        $"📌 جميع الطلاب يدخلون الاختبار عبر ExamAssignmentId واحد.";

                    return RedirectToAction("Create");
                }

                TempData["Success"] = "✔ تم إرسال اختبار واحد مشترك لجميع الطلاب.";
                return RedirectToAction("Create");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
                return RedirectToAction("Create");
            }
        }



    }
}
