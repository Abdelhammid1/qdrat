using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Partner;

namespace QdratNew.Services.Implementations
{
    public class PartnerStudentImportService : IPartnerStudentImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PartnerStudentImportService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =====================================================
        // 📥 استيراد طلاب من Excel (مرتبط بدورة + دفعة)
        // =====================================================
        public async Task<PartnerStudentImportResult> ImportFromExcelAsync(
            IFormFile file,
            int partnerId,
            int subscriptionPeriodId,
            int courseId,
            int batchId)
        {
            var result = new PartnerStudentImportResult();

            if (file == null || file.Length == 0)
            {
                result.Errors.Add("ملف الإكسل غير صالح.");
                return result;
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // ===============================
            // 🔒 تحقق من الشريك
            // ===============================
            var partner = await _context.Partners.FindAsync(partnerId);
            if (partner == null)
            {
                result.Errors.Add("الشريك غير موجود.");
                return result;
            }

            // ===============================
            // 🔒 تحقق من الدفعة + الدورة
            // ===============================
            var batch = await _context.Batches
                .Include(b => b.Branch)
                .FirstOrDefaultAsync(b =>
                    b.Id == batchId &&
                    b.CourseId == courseId &&
                    b.Branch.PartnerId == partnerId);

            if (batch == null)
            {
                result.Errors.Add("الدفعة غير مرتبطة بالدورة أو بالشريك.");
                return result;
            }

            // ===============================
            // 📖 قراءة ملف Excel
            // ===============================
            using var package = new ExcelPackage(file.OpenReadStream());
            var sheet = package.Workbook.Worksheets.FirstOrDefault();

            if (sheet == null)
            {
                result.Errors.Add("ملف الإكسل لا يحتوي على ورقة بيانات.");
                return result;
            }

            int rowCount = sheet.Dimension.Rows;

            // ===============================
            // 🔁 معالجة الصفوف
            // ===============================
            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    var fullName = sheet.Cells[row, 1].Text?.Trim();
                    var nationalId = sheet.Cells[row, 2].Text?.Trim();
                    var phone = sheet.Cells[row, 3].Text?.Trim();
                    var gender = sheet.Cells[row, 4].Text?.Trim();
                    var level = sheet.Cells[row, 5].Text?.Trim();

                    // 🔴 التحقق من الحقول الإلزامية
                    if (string.IsNullOrWhiteSpace(fullName) ||
                        string.IsNullOrWhiteSpace(nationalId) ||
                        string.IsNullOrWhiteSpace(gender) ||
                        string.IsNullOrWhiteSpace(level))
                    {
                        result.Errors.Add($"الصف {row}: بيانات ناقصة.");
                        continue;
                    }

                    // 🔴 منع التكرار
                    bool exists = await _context.Users
                        .AnyAsync(u => u.UserName == nationalId);

                    if (exists)
                    {
                        result.Errors.Add($"الصف {row}: الطالب مسجل مسبقًا.");
                        continue;
                    }

                    // ===============================
                    // 👤 إنشاء المستخدم
                    // ===============================
                    var email = $"{nationalId}@{partner.Code}.edu.sa";

                    var user = new ApplicationUser
                    {
                        UserName = nationalId,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phone
                    };

                    var createUser = await _userManager.CreateAsync(user, nationalId);
                    if (!createUser.Succeeded)
                    {
                        var msg = string.Join(" | ",
                            createUser.Errors.Select(e => e.Description));

                        result.Errors.Add($"الصف {row}: فشل إنشاء المستخدم - {msg}");
                        continue;
                    }

                    await _userManager.AddToRoleAsync(user, "Student");

                    // ===============================
                    // 🎓 إنشاء الطالب
                    // ===============================
                    var student = new Student
                    {
                        UserId = user.Id,
                        FullName = fullName,
                        NationalID = nationalId,
                        Gender = gender,
                        Level = level,
                        School = partner.Name,
                        BranchId = batch.BranchId,
                        IsActiveForLearning = true,
                        PartnerSubscriptionPeriodId = subscriptionPeriodId
                    };

                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();

                    // ===============================
                    // 🔗 ربط الطالب بالدفعة
                    // ===============================
                    _context.StudentBatchEnrollments.Add(new StudentBatchEnrollment
                    {
                        StudentID = student.StudentID,
                        BatchId = batchId,
                        Status = "Active"
                    });

                    await _context.SaveChangesAsync();

                    result.InsertedCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"الصف {row}: {ex.Message}");
                }
            }

            result.Success = result.InsertedCount > 0;
            return result;
        }
    }
}
