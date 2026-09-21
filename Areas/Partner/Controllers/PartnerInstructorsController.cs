using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner.Instructor;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Roles = "Partner,PartnerAdmin")]
    public class PartnerInstructorsController : PartnerBaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public PartnerInstructorsController(
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
            : base(subscriptionService, context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ============================================================
        // 🔹 Helper: Get Active Subscription
        // ============================================================
        private PartnerSubscription? GetActiveSubscription()
        {
            var now = DateTime.Now;

            return _context.PartnerSubscriptions
                .FirstOrDefault(s =>
                    s.PartnerId == ActivePartnerId &&
                    s.StartDate <= now &&
                    s.EndDate >= now);
        }

        // ============================================================
        // 1️⃣ Index
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var subscription = GetActiveSubscription();

            if (subscription == null)
            {
                ViewBag.MaxInstructors = 0;
                ViewBag.ActiveCount = 0;
                ViewBag.AvailableSlots = 0;
                ViewBag.UsagePercentage = 0;

                return View(new List<PartnerInstructorListVm>());
            }

            var instructors = await _context.Instructors
                .Where(x =>
                    x.PartnerSubscriptionId == subscription.Id &&
                    !x.IsDeleted)
                .Select(x => new PartnerInstructorListVm
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Email = x.Email,
                    PhoneNumber = x.PhoneNumber,
                    NationalID = x.NationalID,
                    IsActive = x.IsActive,

                    Curriculums = _context.InstructorCurriculumBatches
                        .Where(icb => icb.InstructorId == x.Id)
                        .Select(icb => icb.Curriculum.Title)
                        .Distinct()
                        .ToList()
                })
                .AsNoTracking()
                .ToListAsync();

            var activeCount = instructors.Count(x => x.IsActive);
            var max = subscription.MaxInstructors ?? 0;

            ViewBag.MaxInstructors = subscription.MaxInstructors ?? 0;
            ViewBag.ActiveCount = activeCount;
            ViewBag.AvailableSlots = subscription.MaxInstructors.HasValue
                ? max - activeCount
                : -1;

            ViewBag.UsagePercentage =
                subscription.MaxInstructors.HasValue && max > 0
                    ? (int)Math.Round((double)activeCount / max * 100)
                    : -1;

            return View(instructors);
        }

        // ============================================================
        // 2️⃣ Create
        // ============================================================
        public IActionResult Create()
        {
            var subscription = GetActiveSubscription();

            if (subscription == null)
            {
                TempData["Error"] = "لا يوجد عقد نشط.";
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PartnerInstructorCreateVm model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var subscription = GetActiveSubscription();

            if (subscription == null)
            {
                ModelState.AddModelError("", "لا يوجد عقد نشط.");
                return View(model);
            }

            if (DateTime.Now > subscription.EndDate)
            {
                ModelState.AddModelError("", "انتهى العقد.");
                return View(model);
            }

            if (subscription.MaxInstructors.HasValue &&
                subscription.MaxInstructors > 0)
            {
                var currentCount = await _context.Instructors
                    .CountAsync(x =>
                        x.PartnerSubscriptionId == subscription.Id &&
                        !x.IsDeleted);

                if (currentCount >= subscription.MaxInstructors.Value)
                {
                    ModelState.AddModelError("", "تم الوصول للحد الأقصى للمدربين.");
                    return View(model);
                }
            }

            var emailExists = await _userManager.FindByEmailAsync(model.Email);
            if (emailExists != null)
            {
                ModelState.AddModelError("", "البريد الإلكتروني مستخدم بالفعل.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.NationalID,     // 🔴 اسم المستخدم = رقم الهوية
                Email = model.Email,
                NationalID = model.NationalID,   // 🔴 مهم جداً
                FullName = model.FullName,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.NationalID);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync("PartnerInstructor"))
                await _roleManager.CreateAsync(new IdentityRole("PartnerInstructor"));

            await _userManager.AddToRoleAsync(user, "PartnerInstructor");

            var instructor = new Instructor
            {
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                NationalID = model.NationalID,
                Gender = model.Gender,
                Specialization = model.Specialization,
                UserId = user.Id,
                PartnerId = ActivePartnerId,
                PartnerSubscriptionId = subscription.Id,
                IsPartnerInstructor = true,
                IsActive = true
            };

            _context.Instructors.Add(instructor);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 3️⃣ Edit
        // ============================================================
        public async Task<IActionResult> Edit(int id)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .Where(x =>
                    x.Id == id &&
                    x.PartnerSubscriptionId == subscription.Id)
                .Select(x => new PartnerInstructorEditVm
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Email = x.Email,
                    PhoneNumber = x.PhoneNumber,
                    Specialization = x.Specialization,
                    Gender = x.Gender,
                    IsActive = x.IsActive
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (instructor == null)
                return NotFound();

            return View(instructor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PartnerInstructorEditVm model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x =>
                    x.Id == model.Id &&
                    x.PartnerSubscriptionId == subscription.Id);

            if (instructor == null)
                return NotFound();

            instructor.FullName = model.FullName;
            instructor.PhoneNumber = model.PhoneNumber;
            instructor.Specialization = model.Specialization;
            instructor.Gender = model.Gender;
            instructor.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 4️⃣ Toggle Status
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PartnerSubscriptionId == subscription.Id);

            if (instructor == null || instructor.IsDeleted)
                return NotFound();

            // لو سيتم تنشيطه
            if (!instructor.IsActive)
            {
                if (subscription.MaxInstructors.HasValue &&
                    subscription.MaxInstructors > 0)
                {
                    var activeCount = await _context.Instructors
                        .CountAsync(x =>
                            x.PartnerSubscriptionId == subscription.Id &&
                            x.IsActive &&
                            !x.IsDeleted);

                    if (activeCount >= subscription.MaxInstructors.Value)
                    {
                        TempData["Error"] =
                            "لا يمكن تنشيط المدرب. تم استهلاك الحد الأقصى للمدربين حسب العقد. يرجى التواصل مع إدارة المنصة لتعديل بنود التعاقد.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            instructor.IsActive = !instructor.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }




        public async Task<IActionResult> Assign(int id)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PartnerSubscriptionId == subscription.Id &&
                    !x.IsDeleted);

            if (instructor == null)
                return NotFound();

            // ====== الدفعات ======
            var batches = await (
                from b in _context.Batches
                join br in _context.Branches on b.BranchId equals br.Id
                where br.PartnerId == ActivePartnerId
                select new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }
            ).AsNoTracking().ToListAsync();

            // ====== المناهج ======
            var curriculums = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                })
                .AsNoTracking()
                .ToListAsync();

            // ====== الربط الحالي ======
            var teaching = await _context.InstructorCurriculumBatches
                .Where(x => x.InstructorId == id)
                .ToListAsync();

            var roles = await _context.InstructorBatchRoles
                .Where(x => x.InstructorId == id)
                .ToListAsync();

            // الدفعات المرتبطة
            var selectedBatchIds = teaching
                .Select(x => x.BatchId)
                .Union(roles.Select(x => x.BatchId))
                .Distinct()
                .ToList();

            // أول منهج مرتبط (لأن الفورم يسمح بواحد فقط حالياً)
            int? selectedCurriculumId = teaching
                .Select(x => (int?)x.CurriculumId)
                .FirstOrDefault();

            // الأدوار الحالية
            var selectedRoles = roles
                .Select(x => x.RoleType)
                .ToList();

            // إضافة Teaching كدور إذا وجد
            if (teaching.Any())
                selectedRoles.Add(InstructorBatchRoleType.Teaching);

            // ====== جدول العرض ======
            var currentAssignments = new List<CurrentAssignmentVm>();

            foreach (var t in teaching)
            {
                var batchName = batches.FirstOrDefault(b => b.Value == t.BatchId.ToString())?.Text;
                var curriculumName = curriculums.FirstOrDefault(c => c.Value == t.CurriculumId.ToString())?.Text;

                currentAssignments.Add(new CurrentAssignmentVm
                {
                    BatchId = t.BatchId,
                    BatchName = batchName,
                    CurriculumName = curriculumName,
                    RoleName = EnumHelper.GetDisplayName(InstructorBatchRoleType.Teaching)
                });
            }

            foreach (var r in roles)
            {
                var batchName = batches.FirstOrDefault(b => b.Value == r.BatchId.ToString())?.Text;

                currentAssignments.Add(new CurrentAssignmentVm
                {
                    BatchId = r.BatchId,
                    BatchName = batchName,
                    CurriculumName = "-",
                    RoleName = EnumHelper.GetDisplayName(r.RoleType)
                });
            }

            var vm = new InstructorAssignVm
            {
                InstructorId = id,
                Batches = batches,
                Curriculums = curriculums,
                CurrentAssignments = currentAssignments,
                SelectedBatchIds = selectedBatchIds,
                SelectedCurriculumId = selectedCurriculumId,
                SelectedRoles = selectedRoles
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(InstructorAssignVm model)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x =>
                    x.Id == model.InstructorId &&
                    x.PartnerSubscriptionId == subscription.Id &&
                    !x.IsDeleted);

            if (instructor == null)
                return NotFound();

            model.SelectedBatchIds ??= new List<int>();
            model.SelectedRoles ??= new List<InstructorBatchRoleType>();

            var existingTeachings = await _context.InstructorCurriculumBatches
                .Where(x => x.InstructorId == model.InstructorId)
                .ToListAsync();

            var existingRoles = await _context.InstructorBatchRoles
                .Where(x => x.InstructorId == model.InstructorId)
                .ToListAsync();

            // ==============================
            // 1️⃣ حذف الربط غير المحدد
            // ==============================

            // حذف Teaching غير المحدد
            foreach (var teaching in existingTeachings)
            {
                if (!model.SelectedBatchIds.Contains(teaching.BatchId) ||
                    !model.SelectedRoles.Contains(InstructorBatchRoleType.Teaching) ||
                    model.SelectedCurriculumId != teaching.CurriculumId)
                {
                    _context.InstructorCurriculumBatches.Remove(teaching);
                }
            }

            // حذف Roles غير المحددة
            foreach (var role in existingRoles)
            {
                if (!model.SelectedBatchIds.Contains(role.BatchId) ||
                    !model.SelectedRoles.Contains(role.RoleType))
                {
                    _context.InstructorBatchRoles.Remove(role);
                }
            }

            // ==============================
            // 2️⃣ إضافة الجديد
            // ==============================

            foreach (var batchId in model.SelectedBatchIds)
            {
                foreach (var role in model.SelectedRoles)
                {
                    if (role == InstructorBatchRoleType.Teaching)
                    {
                        if (!model.SelectedCurriculumId.HasValue)
                            continue;

                        bool exists = existingTeachings.Any(x =>
                            x.BatchId == batchId &&
                            x.CurriculumId == model.SelectedCurriculumId.Value);

                        if (!exists)
                        {
                            _context.InstructorCurriculumBatches.Add(
                                new InstructorCurriculumBatch
                                {
                                    InstructorId = model.InstructorId,
                                    UserId = instructor.UserId,
                                    BatchId = batchId,
                                    CurriculumId = model.SelectedCurriculumId.Value
                                });
                        }
                    }
                    else
                    {
                        bool exists = existingRoles.Any(x =>
                            x.BatchId == batchId &&
                            x.RoleType == role);

                        if (!exists)
                        {
                            _context.InstructorBatchRoles.Add(
                                new InstructorBatchRole
                                {
                                    InstructorId = model.InstructorId,
                                    BatchId = batchId,
                                    RoleType = role
                                });
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Assign), new { id = model.InstructorId });
        }


        public async Task<IActionResult> ResetPassword(int id)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PartnerSubscriptionId == subscription.Id &&
                    !x.IsDeleted);

            if (instructor == null || string.IsNullOrEmpty(instructor.UserId))
                return NotFound();

            ViewBag.InstructorId = id;
            ViewBag.InstructorName = instructor.FullName;

            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int instructorId, string newPassword)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x =>
                    x.Id == instructorId &&
                    x.PartnerSubscriptionId == subscription.Id &&
                    !x.IsDeleted);

            if (instructor == null || string.IsNullOrEmpty(instructor.UserId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(instructor.UserId);
            if (user == null)
                return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                ViewBag.InstructorId = instructorId;
                ViewBag.InstructorName = instructor.FullName;
                return View();
            }

            TempData["Success"] = "تم إعادة تعيين كلمة المرور بنجاح.";

            return RedirectToAction(nameof(Index));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAssignment(
    int instructorId,
    int batchId,
    string type,
    int? curriculumId)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(x =>
                    x.Id == instructorId &&
                    x.PartnerSubscriptionId == subscription.Id &&
                    !x.IsDeleted);

            if (instructor == null)
                return NotFound();

            if (type == "Teaching" && curriculumId.HasValue)
            {
                var teaching = await _context.InstructorCurriculumBatches
                    .FirstOrDefaultAsync(x =>
                        x.InstructorId == instructorId &&
                        x.BatchId == batchId &&
                        x.CurriculumId == curriculumId.Value);

                if (teaching != null)
                    _context.InstructorCurriculumBatches.Remove(teaching);
            }
            else
            {
                if (Enum.TryParse<InstructorBatchRoleType>(type, out var roleType))
                {
                    var role = await _context.InstructorBatchRoles
                        .FirstOrDefaultAsync(x =>
                            x.InstructorId == instructorId &&
                            x.BatchId == batchId &&
                            x.RoleType == roleType);

                    if (role != null)
                        _context.InstructorBatchRoles.Remove(role);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Assign), new { id = instructorId });
        }





        // ============================================================
        // 5️⃣ Delete (Soft)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var subscription = GetActiveSubscription();
            if (subscription == null)
                return NotFound();

            var instructor = await _context.Instructors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PartnerSubscriptionId == subscription.Id);

            if (instructor == null)
                return NotFound();

            instructor.IsDeleted = true;
            instructor.DeletedAt = DateTime.UtcNow;
            instructor.IsActive = false;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}