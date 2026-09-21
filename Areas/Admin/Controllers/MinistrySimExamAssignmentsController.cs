using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Areas.Admin.Controllers
{
    // شاشة تصفّح إسنادات اختبار معمل القياس عبر 3 محاور: الدفعات / طلاب المعهد (إسناد فردي) / طلاب ضيوف —
    // مكمّلة لشاشات الإسناد الحالية (AssignToBatch/AssignToStudent) المرتبطة باختبار واحد، هذه الشاشة تتصفّح
    // من زاوية الدفعة/الطالب/الضيف عبر كل الاختبارات.
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee,DataEntry")]
    public class MinistrySimExamAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMinistrySimExamAssignmentService _assignmentService;

        public MinistrySimExamAssignmentsController(ApplicationDbContext context, IMinistrySimExamAssignmentService assignmentService)
        {
            _context = context;
            _assignmentService = assignmentService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // الدفعات التابعة لدورات تحتوي اختبار محاكاة وزارة واحد على الأقل فقط (تجنّب ضوضاء دفعات لا علاقة لها بالميزة)
            var coursesWithExamIds = await _context.MinistrySimExams
                .AsNoTracking()
                .Select(e => e.CourseId)
                .Distinct()
                .ToListAsync();

            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && !b.IsArchived && EF.Constant(coursesWithExamIds).Contains(b.CourseId))
                .OrderBy(b => b.Name)
                .Select(b => new MinistrySimExamBatchCardVm
                {
                    BatchId = b.Id,
                    Name = b.Name,
                    CourseName = b.Course.Name,
                    EnrolledStudentsCount = b.StudentBatchEnrollments.Count(e => e.Status == "Active"),
                    AssignedExamsCount = _context.MinistrySimExamAssignmentsToBatches.Count(a => a.BatchId == b.Id)
                })
                .ToListAsync();

            var studentAssignments = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .Select(a => new { a.StudentId, a.Student.FullName, ExamTitle = a.MinistrySimExam.Title })
                .ToListAsync();

            var instituteStudents = studentAssignments
                .GroupBy(a => new { a.StudentId, a.FullName })
                .Select(g => new MinistrySimExamInstituteStudentRowVm
                {
                    StudentId = g.Key.StudentId,
                    FullName = g.Key.FullName,
                    AssignedExamTitles = g.Select(x => x.ExamTitle).Distinct().ToList()
                })
                .OrderBy(x => x.FullName)
                .ToList();

            var guestAssignments = await _context.MinistrySimExamAssignmentsToGuests
                .AsNoTracking()
                .Select(a => new { a.GuestStudentId, ExamTitle = a.MinistrySimExam.Title })
                .ToListAsync();

            var guests = await _context.MinistrySimExamGuestStudents
                .AsNoTracking()
                .Where(g => g.IsActive)
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => new MinistrySimExamGuestCardVm
                {
                    GuestId = g.Id,
                    FullName = g.FullName,
                    PhoneNumber = g.PhoneNumber
                })
                .ToListAsync();

            foreach (var guest in guests)
                guest.AssignedExamTitles = guestAssignments.Where(a => a.GuestStudentId == guest.GuestId).Select(a => a.ExamTitle).Distinct().ToList();

            var vm = new MinistrySimExamAssignmentsOverviewVm
            {
                Batches = batches,
                InstituteStudents = instituteStudents,
                Guests = guests
            };

            return View(vm);
        }

        // "تفاصيل" على كارت الدفعة — قائمة الاختبارات المُسنَدة لها + نموذج إسناد اختبار جديد من نفس دورة الدفعة
        [HttpGet]
        public async Task<IActionResult> BatchDetails(int batchId)
        {
            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("❌ لم يتم العثور على الدفعة المطلوبة.");

            var assignedExams = await _context.MinistrySimExamAssignmentsToBatches
                .AsNoTracking()
                .Where(a => a.BatchId == batchId)
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => new MinistrySimExamAssignedExamRowVm
                {
                    ExamId = a.MinistrySimExamId,
                    Title = a.MinistrySimExam.Title,
                    AssignedAt = a.AssignedAt
                })
                .ToListAsync();

            var assignedExamIds = assignedExams.Select(a => a.ExamId).ToList();

            var assignableExams = await _context.MinistrySimExams
                .AsNoTracking()
                .Where(e => e.CourseId == batch.CourseId && e.IsPublished && !assignedExamIds.Contains(e.Id))
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title })
                .ToListAsync();

            var vm = new MinistrySimExamBatchDetailsVm
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseName = batch.Course.Name,
                AssignedExams = assignedExams,
                AssignableExams = assignableExams
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignExamToBatch(int batchId, int examId)
        {
            var courseId = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.CourseId)
                .FirstOrDefaultAsync();

            var curriculumIds = await _context.CourseCurriculums
                .AsNoTracking()
                .Where(cc => cc.CourseId == courseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            var instructorId = await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(icb => EF.Constant(curriculumIds).Contains(icb.CurriculumId))
                .Select(icb => (int?)icb.InstructorId)
                .FirstOrDefaultAsync();

            var result = await _assignmentService.AssignToBatchesAsync(examId, new List<int> { batchId }, instructorId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            if (result.SkippedMessages.Any())
                TempData["Warning"] = string.Join(" | ", result.SkippedMessages);

            return RedirectToAction(nameof(BatchDetails), new { batchId });
        }

        // "تفاصيل" على كارت الضيف — قائمة الاختبارات المُسنَدة له + نموذج إسناد اختبار جديد (كل الاختبارات المنشورة، بلا قيد دورة)
        [HttpGet]
        public async Task<IActionResult> GuestDetails(int guestId)
        {
            var guest = await _context.MinistrySimExamGuestStudents
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == guestId);

            if (guest == null)
                return NotFound("❌ لم يتم العثور على الطالب الضيف المطلوب.");

            var assignedExams = await _context.MinistrySimExamAssignmentsToGuests
                .AsNoTracking()
                .Where(a => a.GuestStudentId == guestId)
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => new MinistrySimExamAssignedExamRowVm
                {
                    ExamId = a.MinistrySimExamId,
                    Title = a.MinistrySimExam.Title,
                    AssignedAt = a.AssignedAt
                })
                .ToListAsync();

            var assignedExamIds = assignedExams.Select(a => a.ExamId).ToList();

            var assignableExams = await _context.MinistrySimExams
                .AsNoTracking()
                .Where(e => e.IsPublished && !assignedExamIds.Contains(e.Id))
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Title + " — " + e.Course.Name })
                .ToListAsync();

            var vm = new MinistrySimExamGuestDetailsVm
            {
                GuestId = guest.Id,
                FullName = guest.FullName,
                PhoneNumber = guest.PhoneNumber,
                AssignedExams = assignedExams,
                AssignableExams = assignableExams
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignExamToGuest(int guestId, int examId)
        {
            var result = await _assignmentService.AssignToGuestsAsync(examId, new List<int> { guestId });

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            if (result.SkippedMessages.Any())
                TempData["Warning"] = string.Join(" | ", result.SkippedMessages);

            return RedirectToAction(nameof(GuestDetails), new { guestId });
        }

        // إضافة طالب ضيف جديد (طبقة بيانات مستقلة عن جدول Students — راجع تعليق الكيان MinistrySimExamGuestStudent)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGuestStudent(string fullName, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["Error"] = "اسم الطالب الضيف مطلوب.";
                return RedirectToAction(nameof(Index), new { tab = "guests" });
            }

            _context.MinistrySimExamGuestStudents.Add(new MinistrySimExamGuestStudent
            {
                FullName = fullName.Trim(),
                PhoneNumber = phoneNumber?.Trim()
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إضافة الطالب الضيف بنجاح.";
            return RedirectToAction(nameof(Index), new { tab = "guests" });
        }
    }
}
