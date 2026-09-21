using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using System.Security.Claims;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    [Authorize(Roles = "Instructor,PartnerInstructor,SuperAdmin,Owner,Developer")]
    public abstract class BaseInstructorController : Controller
    {
        protected readonly UserManager<ApplicationUser> _userManager;
        protected readonly IInstructorScopeService _scopeService;

        protected int CurrentInstructorId;
        protected Instructor CurrentInstructor;

        protected BaseInstructorController(
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
        {
            _userManager = userManager;
            _scopeService = scopeService;
        }

        // ======================================================
        // تحميل المدرب الحالي
        // ======================================================
        protected async Task<int> RequireInstructorAsync()
        {
            var userId = _userManager.GetUserId(User);

            Console.WriteLine("USER ID FROM LOGIN: " + userId);

            var instructor = await _scopeService.GetInstructorByUserIdAsync(userId);

            if (instructor == null)
            {
                Console.WriteLine("❌ Instructor NOT FOUND");
                return 0;
            }

            Console.WriteLine("✅ Instructor FOUND: " + instructor.Id);

            CurrentInstructor = instructor;
            CurrentInstructorId = instructor.Id;

            return CurrentInstructorId;
        }

        protected Task<List<int>> GetInstructorAllowedCourseIdsAsync(int instructorId)
        {
            return _scopeService.GetAllowedCourseIdsAsync(instructorId);
        }

        protected Task<bool> InstructorHasAccessToCourseAsync(int instructorId, int courseId)
        {
            return _scopeService.CanAccessCourseAsync(instructorId, courseId);
        }

        protected Task<List<int>> GetInstructorAllowedCurriculumIdsAsync(int instructorId)
        {
            return _scopeService.GetAllowedCurriculumIdsAsync(instructorId);
        }

        protected Task<bool> InstructorHasAccessToCurriculumAsync(int instructorId, int curriculumId)
        {
            return _scopeService.CanAccessCurriculumAsync(instructorId, curriculumId);
        }

        protected Task<List<int>> GetInstructorAllowedBatchIdsAsync(int instructorId)
        {
            return _scopeService.GetAllowedBatchIdsAsync(instructorId);
        }

        protected Task<bool> InstructorHasAccessToBatchAsync(int instructorId, int batchId)
        {
            return _scopeService.CanAccessBatchAsync(instructorId, batchId);
        }

        /// <summary>
        /// الدفعات المخصصة للمدرب مباشرة عبر InstructorCurriculumBatches فقط.
        /// استخدم هذه في الداشبورد والإحصاءات بدلاً من GetInstructorAllowedBatchIdsAsync.
        /// </summary>
        protected Task<List<int>> GetInstructorDirectBatchIdsAsync(int instructorId)
            => _scopeService.GetDirectBatchIdsAsync(instructorId);

        /// <summary>
        /// المناهج المخصصة للمدرب مباشرة عبر InstructorCurriculumBatches فقط.
        /// </summary>
        protected Task<List<int>> GetInstructorDirectCurriculumIdsAsync(int instructorId)
            => _scopeService.GetDirectCurriculumIdsAsync(instructorId);
    }
}
