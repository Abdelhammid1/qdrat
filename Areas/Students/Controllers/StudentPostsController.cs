using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Route("Students/[controller]/[action]")]
    public class StudentPostsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentPostsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        [HttpGet]
        public async Task<IActionResult> GetByBatch(int batchId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student == null) return BadRequest("الطالب غير موجود.");

            // تحقّق أن الطالب عضو في الدفعة المطلوبة
            var isInBatch = await _context.StudentBatchEnrollments
                .AnyAsync(e => e.StudentID == student.StudentID && e.BatchId == batchId);
            if (!isInBatch) return BadRequest("الطالب غير مرتبط بهذه الدفعة.");

            var posts = await _context.StudentPosts
                .Include(p => p.Student).ThenInclude(s => s.User)
                .Where(p => p.BatchId == batchId)
                .OrderByDescending(p => p.PostedAt)
                .Select(p => new
                {
                    studentName = p.Student.FullName,
                    studentImage = string.IsNullOrEmpty(p.Student.User.ProfileImagePath)
                        ? "/images/profiles/avatar-male.png"
                        : p.Student.User.ProfileImagePath,
                    content = p.Content,
                    imagePath = string.IsNullOrEmpty(p.ImagePath) ? null : $"/uploads/posts/{p.ImagePath}",
                    postedAt = p.PostedAt.ToString("yyyy-MM-dd HH:mm"),
                    likes = p.Likes,
                    shares = p.Shares
                })
                .ToListAsync();

            return Ok(posts);
        }



        [HttpPost]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Create(IFormCollection form)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (student == null) return BadRequest("الطالب غير معروف.");

                // حدّد الدفعة المستهدفة: إمّا من form أو أول دفعة للطالب
                int? batchIdFromForm = int.TryParse(form["BatchId"], out var bid) ? bid : (int?)null;

                int targetBatchId;
                if (batchIdFromForm.HasValue)
                {
                    // تحقّق أن الطالب ضمن هذه الدفعة
                    var inThisBatch = await _context.StudentBatchEnrollments
                        .AnyAsync(e => e.StudentID == student.StudentID && e.BatchId == batchIdFromForm.Value);
                    if (!inThisBatch) return BadRequest("⚠️ الطالب غير مسجّل في الدفعة المحددة.");
                    targetBatchId = batchIdFromForm.Value;
                }
                else
                {
                    // خُذ أول دفعة للطالب
                    targetBatchId = await _context.StudentBatchEnrollments
                        .Where(e => e.StudentID == student.StudentID)
                        .Select(e => e.BatchId)
                        .FirstOrDefaultAsync();

                    if (targetBatchId == 0) // لو مافيش دفعات
                        return BadRequest("⚠️ الطالب غير مرتبط بأي دفعة.");
                }

                var content = form["Content"].ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(content))
                    return BadRequest("⚠️ لا يمكن نشر منشور بدون محتوى.");

                string? imagePath = null;
                var file = form.Files.GetFile("Image");
                if (file != null && file.Length > 0)
                {
                    var extension = Path.GetExtension(file.FileName).ToLower();
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    if (!allowedExtensions.Contains(extension))
                        return BadRequest("⚠️ صيغة الصورة غير مدعومة. يُسمح فقط بـ JPG و PNG و GIF.");

                    var fileName = Guid.NewGuid() + extension;
                    var savePath = Path.Combine("wwwroot/uploads/posts", fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                    using var stream = new FileStream(savePath, FileMode.Create);
                    await file.CopyToAsync(stream);
                    imagePath = fileName;
                }

                var post = new StudentPost
                {
                    StudentID = student.StudentID,
                    BatchId = targetBatchId,
                    Content = content,
                    ImagePath = imagePath ?? string.Empty,
                    PostedAt = DateTime.Now
                };

                _context.StudentPosts.Add(post);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (DbUpdateException ex)
            {
                var detailedMessage = ex.InnerException?.Message ?? ex.Message;
                return BadRequest("⚠️ خطأ أثناء الحفظ في قاعدة البيانات: " + detailedMessage);
            }
            catch (Exception ex)
            {
                return BadRequest("⚠️ خطأ غير متوقع: " + ex.Message);
            }
        }


    }


}
