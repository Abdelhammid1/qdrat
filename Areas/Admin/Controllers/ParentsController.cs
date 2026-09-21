using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.Parent;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class ParentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ParentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Admin/Parents
        public async Task<IActionResult> Index(string? search, string? relation, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Parents
                .AsNoTracking()
                .Include(p => p.Students)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.FullName.Contains(search) ||
                    p.PhoneNumber.Contains(search) ||
                    p.NationalID.Contains(search));

            if (!string.IsNullOrWhiteSpace(relation) && Enum.TryParse<ParentRelation>(relation, out var rel))
                query = query.Where(p => p.RelationToStudent == rel);

            var total = await query.CountAsync();

            var parents = await query
                .OrderByDescending(p => p.DateCreated)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ParentRowViewModel
                {
                    ParentID = p.ParentID,
                    FullName = p.FullName,
                    PhoneNumber = p.PhoneNumber,
                    Email = p.Email,
                    RelationToStudent = p.RelationToStudent.ToString(),
                    NationalID = p.NationalID,
                    WhatsAppNumber = p.WhatsAppNumber,
                    DateCreated = p.DateCreated,
                    StudentsCount = p.Students.Count,
                    HasUserAccount = p.UserId != null
                })
                .ToListAsync();

            var vm = new ParentIndexViewModel
            {
                Parents = parents,
                SearchTerm = search,
                RelationFilter = relation,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };

            return View(vm);
        }

        // GET: Admin/Parents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var parent = await _context.Parents
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Students)
                .FirstOrDefaultAsync(p => p.ParentID == id);

            if (parent == null) return NotFound();

            var studentIds = parent.Students.Select(s => s.StudentID).ToList();

            var performanceLookup = await _context.StudentPerformances
                .AsNoTracking()
                .Where(sp => studentIds.Contains(sp.StudentID))
                .GroupBy(sp => sp.StudentID)
                .Select(g => new
                {
                    StudentID = g.Key,
                    HomeworkCount = g.Count(x => x.ActivityType == PerformanceActivityType.Homework),
                    AvgHomeworkScore = g.Where(x => x.ActivityType == PerformanceActivityType.Homework)
                                        .Select(x => (double?)x.Score).Average(),
                    LastPlacementScore = g.Where(x => x.ActivityType == PerformanceActivityType.PlacementExam)
                                          .OrderByDescending(x => x.ExamDate)
                                          .Select(x => (double?)x.Score).FirstOrDefault(),
                    LastKpiScore = g.Where(x => x.ActivityType == PerformanceActivityType.PerformanceIndicatorExam)
                                    .OrderByDescending(x => x.ExamDate)
                                    .Select(x => (double?)x.Score).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.StudentID);

            var vm = new ParentDetailsViewModel
            {
                ParentID = parent.ParentID,
                FullName = parent.FullName,
                NationalID = parent.NationalID,
                Email = parent.Email,
                PhoneNumber = parent.PhoneNumber,
                WhatsAppNumber = parent.WhatsAppNumber,
                RelationToStudent = parent.RelationToStudent,
                DateCreated = parent.DateCreated,
                HasUserAccount = parent.User != null,
                UserEmail = parent.User?.Email,
                UserName = parent.User?.UserName,
                Students = parent.Students.Select(s =>
                {
                    performanceLookup.TryGetValue(s.StudentID, out var perf);
                    return new ParentStudentSummary
                    {
                        StudentID = s.StudentID,
                        FullName = s.FullName,
                        NationalID = s.NationalID,
                        Level = s.Level,
                        School = s.School,
                        EnrollmentStatus = s.EnrollmentStatus,
                        Gender = s.Gender,
                        Age = s.Age,
                        ProfileImagePath = s.ProfileImagePath,
                        HomeworkCount = perf?.HomeworkCount ?? 0,
                        AvgHomeworkScore = perf?.AvgHomeworkScore,
                        LastPlacementScore = perf?.LastPlacementScore,
                        LastKpiScore = perf?.LastKpiScore
                    };
                }).ToList()
            };

            return View(vm);
        }

        // GET: Admin/Parents/Create
        public async Task<IActionResult> Create()
        {
            var vm = await BuildFormViewModelAsync(null);
            return View(vm);
        }

        // POST: Admin/Parents/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ParentFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var rebuilt = await BuildFormViewModelAsync(null);
                vm.AvailableUsers = rebuilt.AvailableUsers;
                vm.AvailableStudents = rebuilt.AvailableStudents;
                return View(vm);
            }

            var parent = new Parent
            {
                FullName = vm.FullName,
                NationalID = vm.NationalID,
                Email = vm.Email ?? string.Empty,
                PhoneNumber = vm.PhoneNumber,
                WhatsAppNumber = vm.WhatsAppNumber,
                RelationToStudent = vm.RelationToStudent,
                UserId = string.IsNullOrWhiteSpace(vm.UserId) ? null : vm.UserId,
                DateCreated = DateTime.UtcNow
            };

            _context.Parents.Add(parent);
            await _context.SaveChangesAsync();

            // Link selected students
            if (vm.SelectedStudentIds.Any())
            {
                var students = await _context.Students
                    .Where(s => vm.SelectedStudentIds.Contains(s.StudentID))
                    .ToListAsync();

                foreach (var student in students)
                    student.ParentId = parent.ParentID;

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"✅ تم إضافة ولي الأمر «{parent.FullName}» بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Parents/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var parent = await _context.Parents
                .Include(p => p.Students)
                .FirstOrDefaultAsync(p => p.ParentID == id);

            if (parent == null) return NotFound();

            var vm = await BuildFormViewModelAsync(parent);
            return View(vm);
        }

        // POST: Admin/Parents/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ParentFormViewModel vm)
        {
            if (id != vm.ParentID) return NotFound();

            if (!ModelState.IsValid)
            {
                var rebuilt = await BuildFormViewModelAsync(null);
                vm.AvailableUsers = rebuilt.AvailableUsers;
                vm.AvailableStudents = rebuilt.AvailableStudents;
                return View(vm);
            }

            var parent = await _context.Parents
                .Include(p => p.Students)
                .FirstOrDefaultAsync(p => p.ParentID == id);

            if (parent == null) return NotFound();

            parent.FullName = vm.FullName;
            parent.NationalID = vm.NationalID;
            parent.Email = vm.Email ?? string.Empty;
            parent.PhoneNumber = vm.PhoneNumber;
            parent.WhatsAppNumber = vm.WhatsAppNumber;
            parent.RelationToStudent = vm.RelationToStudent;
            parent.UserId = string.IsNullOrWhiteSpace(vm.UserId) ? null : vm.UserId;

            // Detach current students
            var currentStudents = await _context.Students
                .Where(s => s.ParentId == id)
                .ToListAsync();

            foreach (var s in currentStudents)
                s.ParentId = null;

            // Attach new selection
            if (vm.SelectedStudentIds.Any())
            {
                var newStudents = await _context.Students
                    .Where(s => vm.SelectedStudentIds.Contains(s.StudentID))
                    .ToListAsync();

                foreach (var s in newStudents)
                    s.ParentId = parent.ParentID;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ تم تحديث بيانات ولي الأمر «{parent.FullName}» بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Parents/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var parent = await _context.Parents
                .AsNoTracking()
                .Include(p => p.Students)
                .FirstOrDefaultAsync(p => p.ParentID == id);

            if (parent == null) return NotFound();

            return View(parent);
        }

        // POST: Admin/Parents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var parent = await _context.Parents
                .Include(p => p.Students)
                .FirstOrDefaultAsync(p => p.ParentID == id);

            if (parent == null) return NotFound();

            // Detach students before delete
            foreach (var student in parent.Students)
                student.ParentId = null;

            _context.Parents.Remove(parent);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ تم حذف ولي الأمر «{parent.FullName}» بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Parents/StudentReport/5?studentId=10
        public async Task<IActionResult> StudentReport(int id, int studentId)
        {
            var parent = await _context.Parents
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ParentID == id);

            if (parent == null) return NotFound();

            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId && s.ParentId == id);

            if (student == null) return NotFound();

            var performances = await _context.StudentPerformances
                .AsNoTracking()
                .Include(sp => sp.Curriculum)
                .Include(sp => sp.Section)
                .Where(sp => sp.StudentID == studentId)
                .OrderByDescending(sp => sp.ExamDate)
                .ToListAsync();

            var vm = new ParentStudentReportViewModel
            {
                ParentID = parent.ParentID,
                ParentName = parent.FullName,
                RelationToStudent = parent.RelationToStudent.ToString(),
                StudentID = student.StudentID,
                StudentName = student.FullName,
                StudentLevel = student.Level,
                School = student.School,
                Gender = student.Gender,
                Age = student.Age,
                EnrollmentStatus = student.EnrollmentStatus,
                ProfileImagePath = student.ProfileImagePath,

                HomeworkRecords = performances
                    .Where(p => p.ActivityType == PerformanceActivityType.Homework)
                    .Select(MapToRecord).ToList(),

                PlacementExams = performances
                    .Where(p => p.ActivityType == PerformanceActivityType.PlacementExam)
                    .Select(MapToRecord).ToList(),

                KpiExams = performances
                    .Where(p => p.ActivityType == PerformanceActivityType.PerformanceIndicatorExam)
                    .Select(MapToRecord).ToList(),

                GeneralExams = performances
                    .Where(p => p.ActivityType == PerformanceActivityType.FinalExam
                             || p.ActivityType == PerformanceActivityType.RemedialQuiz
                             || p.ActivityType == PerformanceActivityType.Other)
                    .Select(MapToRecord).ToList()
            };

            return View(vm);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private async Task<ParentFormViewModel> BuildFormViewModelAsync(Parent? parent)
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} — {u.Email}",
                    Selected = parent != null && u.Id == parent.UserId
                })
                .ToListAsync();

            users.Insert(0, new SelectListItem { Value = "", Text = "— بدون حساب مستخدم —" });

            var linkedStudentIds = parent?.Students.Select(s => s.StudentID).ToHashSet() ?? new HashSet<int>();

            var students = await _context.Students
                .AsNoTracking()
                .Where(s => s.ParentId == null || (parent != null && s.ParentId == parent.ParentID))
                .OrderBy(s => s.FullName)
                .Select(s => new StudentSelectItem
                {
                    StudentID = s.StudentID,
                    FullName = s.FullName,
                    NationalID = s.NationalID,
                    Level = s.Level,
                    School = s.School,
                    IsSelected = linkedStudentIds.Contains(s.StudentID)
                })
                .ToListAsync();

            return new ParentFormViewModel
            {
                ParentID = parent?.ParentID ?? 0,
                FullName = parent?.FullName ?? string.Empty,
                NationalID = parent?.NationalID ?? string.Empty,
                Email = parent?.Email,
                PhoneNumber = parent?.PhoneNumber ?? string.Empty,
                WhatsAppNumber = parent?.WhatsAppNumber,
                RelationToStudent = parent?.RelationToStudent ?? ParentRelation.أب,
                UserId = parent?.UserId,
                SelectedStudentIds = linkedStudentIds.ToList(),
                AvailableUsers = users,
                AvailableStudents = students
            };
        }

        private static PerformanceRecordVm MapToRecord(StudentPerformance p) => new()
        {
            Id = p.Id,
            Score = p.Score,
            ExamDate = p.ExamDate,
            Level = p.Level,
            CurriculumName = p.Curriculum?.Title,
            SectionName = p.Section?.Title,
            ExamType = p.ExamType,
            ActivityType = p.ActivityType
        };
    }
}
