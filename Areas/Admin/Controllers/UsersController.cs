using DocumentFormat.OpenXml.Spreadsheet;
using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Helpers;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Admin;
using QdratNew.ViewModels.Users;
using System.Text;


// ⚠️ System Critical Controller
// This controller is intentionally restricted to system-level roles only.
// Do NOT add AdminPermission or Profile-based access here.

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly QdratNew.Services.ImpersonationService _impersonation;

        public UsersController(UserManager<ApplicationUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               ApplicationDbContext db,
                               ApplicationDbContext context,
                               SignInManager<ApplicationUser> signInManager,
                               QdratNew.Services.ImpersonationService impersonation)
        {
            _userManager   = userManager;
            _roleManager   = roleManager;
            _db            = db;
            _context       = context;
            _signInManager = signInManager;
            _impersonation = impersonation;
        }

        // ======================== Index (بدون DataTables) ========================
        //        [HttpGet]
        //        public async Task<IActionResult> Index(
        //         string roleFilter = "",
        //         string isActive = "",
        //         string isStudent = "",
        //         string searchText = "",
        //         int page = 1,
        //         int pageSize = 10)
        //        {
        //            if (page <= 0) page = 1;
        //            if (pageSize <= 0) pageSize = 10;

        //            // 🟢 الفلاتر
        //            var roles = await _roleManager.Roles
        //                .OrderBy(r => r.Name)
        //                .Select(r => r.Name!)
        //                .ToListAsync();

        //            var vm = new UsersIndexViewModel
        //            {
        //                Roles = roles.Select(r => new SelectListItem { Value = r, Text = r, Selected = r == roleFilter }),
        //                ActiveStates = new[]
        //                {
        //            new SelectListItem{ Value = "", Text = "الكل", Selected = string.IsNullOrEmpty(isActive) },
        //            new SelectListItem{ Value = "true",  Text = "مفعّل", Selected = isActive == "true" },
        //            new SelectListItem{ Value = "false", Text = "موقوف", Selected = isActive == "false" },
        //        },
        //                StudentStates = new[]
        //                {
        //            new SelectListItem{ Value = "", Text = "الكل", Selected = string.IsNullOrEmpty(isStudent) },
        //            new SelectListItem{ Value = "true",  Text = "طالب", Selected = isStudent == "true" },
        //            new SelectListItem{ Value = "false", Text = "ليس طالبًا", Selected = isStudent == "false" },
        //        },
        //                RoleFilter = roleFilter,
        //                IsActive = string.IsNullOrEmpty(isActive) ? (bool?)null : (isActive == "true"),
        //                IsStudent = string.IsNullOrEmpty(isStudent) ? (bool?)null : (isStudent == "true"),
        //                SearchText = searchText,
        //                Page = page,
        //                PageSize = pageSize
        //            };

        //            var where = new StringBuilder("WHERE 1=1");
        //            var prms = new List<SqlParameter>();

        //            if (vm.IsActive.HasValue)
        //            {
        //                where.Append(" AND a.[IsActive] = @p_isActive");
        //                prms.Add(new SqlParameter("@p_isActive", vm.IsActive.Value));
        //            }

        //            if (vm.IsStudent.HasValue)
        //            {
        //                if (vm.IsStudent.Value) where.Append(" AND s.[StudentID] IS NOT NULL");
        //                else where.Append(" AND s.[StudentID] IS NULL");
        //            }

        //            // 🟩 البحث بالاسم / البريد / اسم المستخدم / الهوية (مع تحقق الأعمدة)
        //            if (!string.IsNullOrWhiteSpace(searchText))
        //            {
        //                var searchableCols = new List<string> { "a.[FullName]", "a.[Email]" };
        //                try
        //                {
        //                    var colCheckCmd = _db.Database.GetDbConnection().CreateCommand();
        //                    colCheckCmd.CommandText = @"
        //                SELECT COLUMN_NAME 
        //                FROM INFORMATION_SCHEMA.COLUMNS 
        //                WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME IN ('UserName', 'NationalID')";
        //                    if (colCheckCmd.Connection.State != System.Data.ConnectionState.Open)
        //                        await colCheckCmd.Connection.OpenAsync();

        //                    using var reader = await colCheckCmd.ExecuteReaderAsync();
        //                    while (await reader.ReadAsync())
        //                    {
        //                        var col = reader["COLUMN_NAME"].ToString();
        //                        if (col == "UserName") searchableCols.Add("a.[UserName]");
        //                        if (col == "NationalID") searchableCols.Add("a.[NationalID]");
        //                    }
        //                }
        //                catch { /* تجاهل الأخطاء */ }

        //                where.Append(" AND (" + string.Join(" OR ", searchableCols.Select(c => $"{c} LIKE @p_q")) + ")");
        //                prms.Add(new SqlParameter("@p_q", "%" + searchText.Trim() + "%"));
        //            }

        //            if (!string.IsNullOrWhiteSpace(roleFilter))
        //            {
        //                where.Append(@"
        //            AND EXISTS (
        //                SELECT 1
        //                FROM [AspNetUserRoles] ur
        //                INNER JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
        //                WHERE ur.[UserId] = a.[Id] AND r.[Name] = @p_role
        //            )");
        //                prms.Add(new SqlParameter("@p_role", roleFilter));
        //            }

        //            // 🟢 إجمالي السجلات
        //            using (var cmd0 = _db.Database.GetDbConnection().CreateCommand())
        //            {
        //                cmd0.CommandText = "SELECT COUNT(1) FROM [AspNetUsers]";
        //                if (cmd0.Connection.State != System.Data.ConnectionState.Open)
        //                    await cmd0.Connection.OpenAsync();
        //                vm.RecordsTotal = Convert.ToInt32(await cmd0.ExecuteScalarAsync());
        //            }

        //            // 🟢 إجمالي بعد الفلترة
        //            var countSql = $@"
        //        SELECT COUNT(1)
        //        FROM [AspNetUsers] a
        //        LEFT JOIN [Students] s ON a.[Id] = s.[UserId]
        //        {where}";
        //            using (var cmd1 = _db.Database.GetDbConnection().CreateCommand())
        //            {
        //                cmd1.CommandText = countSql;
        //                foreach (var p in prms) cmd1.Parameters.Add(p);
        //                if (cmd1.Connection.State != System.Data.ConnectionState.Open)
        //                    await cmd1.Connection.OpenAsync();
        //                vm.RecordsFiltered = Convert.ToInt32(await cmd1.ExecuteScalarAsync());
        //            }

        //            // 🟢 البيانات مع ROW_NUMBER()
        //            int fromRow = (page - 1) * pageSize + 1;
        //            int toRow = page * pageSize;

        //            var pageSql = new StringBuilder(@";
        //WITH base AS (
        //    SELECT
        //        a.[Id],
        //        a.[FullName],
        //        a.[Email],
        //        a.[IsActive],
        //        a.[LastLoginAt],
        //        s.[StudentID] AS StudentId,
        //        ROW_NUMBER() OVER (
        //            ORDER BY COALESCE(a.[LastLoginAt], '0001-01-01T00:00:00') DESC, a.[FullName] ASC
        //        ) AS rn
        //    FROM [AspNetUsers] a
        //    LEFT JOIN [Students] s ON a.[Id] = s.[UserId]
        //");
        //            pageSql.AppendLine(where.ToString());
        //            pageSql.AppendLine(@"),
        //page AS (
        //    SELECT *
        //    FROM base
        //    WHERE rn BETWEEN @p_from AND @p_to
        //)
        //SELECT
        //    p.[Id],
        //    p.[FullName],
        //    p.[Email],
        //    p.[IsActive],
        //    p.[LastLoginAt],
        //    CASE WHEN p.[StudentId] IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS [IsStudent],
        //    p.[StudentId],
        //    ISNULL((
        //        SELECT COUNT(1)
        //        FROM [StudentBatchEnrollments] e
        //        WHERE e.[StudentID] = p.[StudentId]
        //    ), 0) AS EnrolledBatchesCount,
        //    ISNULL((
        //        SELECT STUFF((
        //            SELECT ',' + r.[Name]
        //            FROM [AspNetUserRoles] ur
        //            INNER JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
        //            WHERE ur.[UserId] = p.[Id]
        //            FOR XML PATH(''), TYPE
        //        ).value('.','nvarchar(max)'),1,1,'')
        //    ),'') AS RolesCsv
        //FROM page p
        //ORDER BY p.rn DESC;");

        //            var pageParams = prms.ToList();
        //            pageParams.Add(new SqlParameter("@p_from", fromRow));
        //            pageParams.Add(new SqlParameter("@p_to", toRow));

        //            using (var cmd2 = _db.Database.GetDbConnection().CreateCommand())
        //            {
        //                cmd2.CommandText = pageSql.ToString();
        //                foreach (var p in pageParams) cmd2.Parameters.Add(p);
        //                if (cmd2.Connection.State != System.Data.ConnectionState.Open)
        //                    await cmd2.Connection.OpenAsync();

        //                using var rdr = await cmd2.ExecuteReaderAsync();
        //                while (await rdr.ReadAsync())
        //                {
        //                    var rolesCsv = (rdr["RolesCsv"] as string) ?? "";
        //                    vm.Users.Add(new UserRowDto
        //                    {
        //                        Id = rdr["Id"].ToString(),
        //                        FullName = rdr["FullName"] as string ?? "",
        //                        Email = rdr["Email"] as string ?? "",
        //                        IsActive = (bool)rdr["IsActive"],
        //                        LastLoginAtTicks = rdr["LastLoginAt"] == DBNull.Value ? 0L : ((DateTime)rdr["LastLoginAt"]).Ticks,
        //                        IsStudent = (bool)rdr["IsStudent"],
        //                        EnrolledBatchesCount = Convert.ToInt32(rdr["EnrolledBatchesCount"]),
        //                        Roles = rolesCsv.Length == 0 ? new List<string>() : rolesCsv.Split(',').ToList()
        //                    });
        //                }
        //            }

        //            return View(vm);
        //        }



        private async Task<bool> IsOwnerOrSuperAdminOrDeveloperAsync(IdentityUser user)
        {
            var roles = await _userManager.GetRolesAsync((ApplicationUser)user);
            return roles.Contains("Owner") || roles.Contains("SuperAdmin") || roles.Contains("Developer");
        }






        // ===================== استيراد جماعي =====================
        [HttpGet]
        public async Task<IActionResult> BulkImport()
        {
            ViewBag.Batches = await _context.Batches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            ViewBag.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkImport(IFormFile excelFile, int batchId, int branchId)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "⚠️ اختر ملف Excel";
                return RedirectToAction(nameof(BulkImport));
            }

            if (batchId <= 0)
            {
                TempData["Error"] = "⚠️ يجب اختيار الدفعة قبل رفع الملف";
                return RedirectToAction(nameof(BulkImport));
            }

            if (branchId <= 0)
            {
                TempData["Error"] = "⚠️ يجب اختيار الفرع قبل رفع الملف";
                return RedirectToAction(nameof(BulkImport));
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets.FirstOrDefault();

            if (sheet == null || sheet.Dimension == null)
            {
                TempData["Error"] = "⚠️ الملف فارغ";
                return RedirectToAction(nameof(BulkImport));
            }

            int lastRow = sheet.Dimension.End.Row;

            // =========================================================
            // 1) التحقق من الدفعة
            // =========================================================
            var selectedBatch = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => new
                {
                    b.Id,
                    b.Name
                })
                .FirstOrDefaultAsync();

            if (selectedBatch == null)
            {
                TempData["Error"] = "⚠️ الدفعة المحددة غير موجودة";
                return RedirectToAction(nameof(BulkImport));
            }

            // =========================================================
            // 2) التحقق من الفرع
            // =========================================================
            var selectedBranch = await _context.Branches
                .AsNoTracking()
                .Where(b => b.Id == branchId)
                .Select(b => new
                {
                    b.Id,
                    b.Name
                })
                .FirstOrDefaultAsync();

            if (selectedBranch == null)
            {
                TempData["Error"] = "⚠️ الفرع المحدد غير موجود";
                return RedirectToAction(nameof(BulkImport));
            }

            // =========================================================
            // 3) قراءة ملف Excel في الذاكرة
            // ترتيب الأعمدة المتوقع:
            // 1 FullName
            // 2 NationalID
            // 3 Phone
            // 4 Gender
            // 5 School اختياري
            // 6 Level اختياري
            // =========================================================
            var importedRows = new List<BulkStudentImportRow>();
            var processedNationalIds = new HashSet<string>();

            for (int row = 2; row <= lastRow; row++)
            {
                var fullName = sheet.Cells[row, 1].Text?.Trim();
                var nationalIdRaw = sheet.Cells[row, 2].Text?.Trim();
                var phone = sheet.Cells[row, 3].Text?.Trim();
                var gender = sheet.Cells[row, 4].Text?.Trim();

                var school = sheet.Dimension.End.Column >= 5
                    ? sheet.Cells[row, 5].Text?.Trim()
                    : null;

                var level = sheet.Dimension.End.Column >= 6
                    ? sheet.Cells[row, 6].Text?.Trim()
                    : null;

                if (string.IsNullOrWhiteSpace(nationalIdRaw))
                    continue;

                var nationalId = new string(nationalIdRaw.Where(char.IsDigit).ToArray());

                if (string.IsNullOrWhiteSpace(nationalId) || nationalId.Length < 9)
                    continue;

                if (processedNationalIds.Contains(nationalId))
                    continue;

                processedNationalIds.Add(nationalId);

                importedRows.Add(new BulkStudentImportRow
                {
                    FullName = string.IsNullOrWhiteSpace(fullName) ? "غير معروف" : fullName,
                    NationalId = nationalId,
                    Phone = phone,
                    Gender = NormalizeGenderForStudent(gender),
                    School = string.IsNullOrWhiteSpace(school) ? "غير محدد" : school,
                    Level = string.IsNullOrWhiteSpace(level) ? "غير محدد" : level
                });
            }

            if (!importedRows.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على بيانات صالحة داخل الملف";
                return RedirectToAction(nameof(BulkImport));
            }

            // =========================================================
            // 4) تحميل المستخدمين الموجودين
            // =========================================================
            var usersRaw = await _context.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.NationalID
                })
                .ToListAsync();

            var usersDict = usersRaw
                .Where(u => !string.IsNullOrWhiteSpace(u.NationalID))
                .Select(u => new
                {
                    u.Id,
                    CleanNationalID = new string(u.NationalID.Where(char.IsDigit).ToArray())
                })
                .Where(u => !string.IsNullOrWhiteSpace(u.CleanNationalID))
                .GroupBy(u => u.CleanNationalID)
                .ToDictionary(g => g.Key, g => g.First().Id);

            // =========================================================
            // 5) تحميل الطلاب الموجودين
            // =========================================================
            var studentsRaw = await _context.Students
                .AsNoTracking()
                .Select(s => new
                {
                    s.StudentID,
                    s.NationalID
                })
                .ToListAsync();

            var studentsDict = studentsRaw
                .Where(s => !string.IsNullOrWhiteSpace(s.NationalID))
                .Select(s => new
                {
                    s.StudentID,
                    CleanNationalID = new string(s.NationalID.Where(char.IsDigit).ToArray())
                })
                .Where(s => !string.IsNullOrWhiteSpace(s.CleanNationalID))
                .GroupBy(s => s.CleanNationalID)
                .ToDictionary(g => g.Key, g => g.First().StudentID);

            // =========================================================
            // 6) تحميل الربط الحالي للدفعة
            // =========================================================
            var existingEnrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            var enrolledStudents = existingEnrollments
                .Distinct()
                .ToDictionary(id => id, id => true);

            var newEnrollments = new List<StudentBatchEnrollment>();
            var userCreationErrors = new List<string>();

            int createdUsersCount = 0;
            int createdStudentsCount = 0;
            int skippedUsersCount = 0;

            // =========================================================
            // 7) تأكيد وجود Role الطالب
            // =========================================================
            if (!await _roleManager.RoleExistsAsync("Student"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Student"));
            }

            // =========================================================
            // 8) إنشاء المستخدمين الناقصين
            // =========================================================
            foreach (var row in importedRows)
            {
                if (usersDict.TryGetValue(row.NationalId, out _))
                    continue;

                var generatedEmail = $"{row.NationalId}@qdrat.local";

                var newUser = new ApplicationUser
                {
                    UserName = row.NationalId,
                    Email = generatedEmail,
                    NationalID = row.NationalId,
                    FullName = row.FullName,
                    PhoneNumber = row.Phone,
                    WhatsAppNumber = row.Phone,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true,
                    IsActive = true
                };

                var createResult = await _userManager.CreateAsync(newUser, row.NationalId);

                if (!createResult.Succeeded)
                {
                    skippedUsersCount++;

                    var errorsText = string.Join(" | ", createResult.Errors.Select(e => e.Description));

                    userCreationErrors.Add(
                        $"الطالب: {row.FullName} - الهوية: {row.NationalId} - سبب الفشل: {errorsText}"
                    );

                    continue;
                }

                var roleResult = await _userManager.AddToRoleAsync(newUser, "Student");

                if (!roleResult.Succeeded)
                {
                    var roleErrorsText = string.Join(" | ", roleResult.Errors.Select(e => e.Description));

                    userCreationErrors.Add(
                        $"تم إنشاء المستخدم لكن فشل ربطه بدور Student - الطالب: {row.FullName} - الهوية: {row.NationalId} - السبب: {roleErrorsText}"
                    );
                }

                usersDict[row.NationalId] = newUser.Id;
                createdUsersCount++;
            }

            // =========================================================
            // 9) إنشاء الطلاب الناقصين
            // =========================================================
            foreach (var row in importedRows)
            {
                if (studentsDict.TryGetValue(row.NationalId, out _))
                    continue;

                if (!usersDict.TryGetValue(row.NationalId, out var userId))
                    continue;

                var student = new Student
                {
                    NationalID = row.NationalId,
                    FullName = row.FullName,
                    Email = $"{row.NationalId}@qdrat.local",
                    PhoneNumber = row.Phone,
                    WhatsAppNumber = row.Phone,
                    Gender = row.Gender,

                    School = row.School,
                    Level = row.Level,

                    Age = 18,
                    BranchId = branchId,

                    UserId = userId,
                    EnrollmentStatus = "نشط",
                    RegistrationDate = DateTime.UtcNow,
                    IsRegular = true
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                studentsDict[row.NationalId] = student.StudentID;
                createdStudentsCount++;
            }

            // =========================================================
            // 10) إضافة الطلاب للدفعة
            // =========================================================
            foreach (var row in importedRows)
            {
                if (!studentsDict.TryGetValue(row.NationalId, out var studentId))
                    continue;

                if (enrolledStudents.TryGetValue(studentId, out _))
                    continue;

                newEnrollments.Add(new StudentBatchEnrollment
                {
                    StudentID = studentId,
                    BatchId = batchId,
                    EnrolledAt = DateTime.UtcNow,
                    Status = "Active"
                });

                enrolledStudents[studentId] = true;
            }

            // =========================================================
            // 11) Bulk Insert للتسجيلات الجديدة
            // =========================================================
            if (newEnrollments.Any())
            {
                await _context.BulkInsertAsync(newEnrollments);
            }

            if (userCreationErrors.Any())
            {
                TempData["Error"] = string.Join("<br/>", userCreationErrors.Take(30));
            }

            TempData["Success"] =
                $"✅ تم إنشاء {createdUsersCount} مستخدم جديد، وإنشاء {createdStudentsCount} طالب جديد، وإضافة {newEnrollments.Count} طالب للدفعة. الطلاب الذين لم يتم إنشاء مستخدم لهم: {skippedUsersCount}";

            return RedirectToAction(nameof(Index));
        }
        // =========================================================
        // Helper Class داخل نفس الكنترولر
        // =========================================================
        private sealed class BulkStudentImportRow
        {
            public string FullName { get; set; } = "غير معروف";
            public string NationalId { get; set; } = "";
            public string? Phone { get; set; }
            public string Gender { get; set; } = "غير محدد";
            public string School { get; set; } = "غير محدد";
            public string Level { get; set; } = "غير محدد";
        }

        // =========================================================
        // Helper Method داخل نفس الكنترولر
        // =========================================================
        private static string NormalizeGenderForStudent(string? gender)
        {
            if (string.IsNullOrWhiteSpace(gender))
                return "غير محدد";

            var value = gender.Trim();

            if (value == "ذكر" || value.Equals("Male", StringComparison.OrdinalIgnoreCase) || value == "M")
                return "ذكر";

            if (value == "أنثى" || value == "انثى" || value.Equals("Female", StringComparison.OrdinalIgnoreCase) || value == "F")
                return "أنثى";

            return value;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkImportStudents(IFormFile excelFile, int batchId)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "⚠️ اختر ملف Excel";
                return RedirectToAction(nameof(BulkImport));
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets.FirstOrDefault();

            if (sheet == null)
            {
                TempData["Error"] = "⚠️ الملف فارغ";
                return RedirectToAction(nameof(BulkImport));
            }

            int lastRow = sheet.Dimension.End.Row;

            // =========================
            // 1️⃣ تحميل الطلاب
            // =========================
            var allStudents = await _context.Students
                .AsNoTracking()
                .Select(s => new { s.StudentID, s.NationalID })
                .ToListAsync();

            var studentDict = allStudents
                .Where(s => !string.IsNullOrEmpty(s.NationalID))
                .GroupBy(s => s.NationalID)
                .ToDictionary(g => g.Key, g => g.First().StudentID);

            // =========================
            // 2️⃣ تحميل تسجيلات الدفعة
            // =========================
            var existingEnrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            var enrolledHash = existingEnrollments.ToHashSet();

            var newEnrollments = new List<StudentBatchEnrollment>();

            int added = 0;
            int skipped = 0;

            // =========================
            // 3️⃣ قراءة Excel
            // =========================
            for (int row = 2; row <= lastRow; row++)
            {
                var nationalIdRaw = sheet.Cells[row, 2].Text?.Trim();

                if (string.IsNullOrWhiteSpace(nationalIdRaw))
                    continue;

                var nationalId = new string(nationalIdRaw.Where(char.IsDigit).ToArray());

                if (string.IsNullOrWhiteSpace(nationalId))
                    continue;

                if (studentDict.TryGetValue(nationalId, out var studentId))
                {
                    if (!enrolledHash.Contains(studentId))
                    {
                        newEnrollments.Add(new StudentBatchEnrollment
                        {
                            StudentID = studentId,
                            BatchId = batchId,
                            EnrolledAt = DateTime.UtcNow,
                            Status = "Active"
                        });

                        added++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                else
                {
                    skipped++;
                }
            }

            // =========================
            // 4️⃣ Bulk Insert
            // =========================
            if (newEnrollments.Any())
            {
                await _context.BulkInsertAsync(newEnrollments);
            }

            TempData["Success"] = $"✅ تم إضافة {added} طالب للدفعة";
            TempData["Info"] = $"⚠️ تم تجاهل {skipped} (غير موجود أو مضاف مسبقًا)";

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkImportUsers(IFormFile excelFile, string roleName)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "⚠️ الرجاء اختيار ملف Excel صالح.";
                return RedirectToAction(nameof(BulkImport));
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                TempData["Error"] = "⚠️ الملف لا يحتوي على بيانات.";
                return RedirectToAction(nameof(BulkImport));
            }

            int lastRow = worksheet.Dimension.End.Row;
            int successCount = 0, failCount = 0;
            var failedRows = new List<string>();

            for (int row = 2; row <= lastRow; row++)
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                try
                {
                    var fullName = worksheet.Cells[row, 1].Text?.Trim();
                    var email = worksheet.Cells[row, 2].Text?.Trim().ToLower();
                    var nationalIdRaw = worksheet.Cells[row, 3].Text?.Trim();
                    var phone = worksheet.Cells[row, 5].Text?.Trim();
                    var genderText = worksheet.Cells[row, 6].Text?.Trim();

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        failCount++;
                        failedRows.Add($"⚠️ الصف {row}: الاسم فارغ.");
                        continue;
                    }

                    string nationalId = "";
                    if (!string.IsNullOrWhiteSpace(nationalIdRaw))
                        nationalId = new string(nationalIdRaw.Where(char.IsDigit).ToArray());

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        var safeName = fullName.Replace(" ", "_");
                        email = $"{safeName.ToLower()}_{Guid.NewGuid().ToString().Substring(0, 6)}@qdrat.local";
                    }

                    email = email.Trim().ToLower();

                    var existing = await db.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
                    if (existing != null)
                    {
                        failCount++;
                        failedRows.Add($"⚠️ الصف {row}: البريد {email} مستخدم مسبقًا.");
                        continue;
                    }

                    var gender = genderText switch
                    {
                        "أنثى" or "Female" => QdratNew.Enums.GenderType.Female,
                        _ => QdratNew.Enums.GenderType.Male
                    };

                    var password = !string.IsNullOrWhiteSpace(nationalId) ? nationalId : "Qdrat@123";

                    var user = new ApplicationUser
                    {
                        FullName = fullName,
                        Email = email,
                        UserName = email,
                        Gender = gender,
                        IsActive = true,
                        NationalID = nationalId,
                        PhoneNumber = phone,
                        WhatsAppNumber = phone
                    };

                    var result = await userManager.CreateAsync(user, password);
                    if (!result.Succeeded)
                    {
                        failCount++;
                        failedRows.Add($"❌ الصف {row}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                        continue;
                    }

                    // إضافة الدور العام (مثلاً "Instructor" أو "Parent")
                    if (!string.IsNullOrWhiteSpace(roleName))
                    {
                        if (!await roleManager.RoleExistsAsync(roleName))
                            await roleManager.CreateAsync(new IdentityRole(roleName));

                        await userManager.AddToRoleAsync(user, roleName);

                        // 🎓 إنشاء تلقائي لسجل مدرب عند استيراد مستخدم بدور "Instructor"
                        if (roleName == "Instructor" && !await _context.Instructors.AnyAsync(i => i.UserId == user.Id))
                        {
                            _context.Instructors.Add(new Instructor
                            {
                                FullName = user.FullName ?? user.Email ?? "مدرب جديد",
                                Email = user.Email ?? string.Empty,
                                PhoneNumber = user.PhoneNumber,
                                WhatsAppNumber = user.WhatsAppNumber,
                                Gender = user.Gender,
                                NationalID = user.NationalID,
                                UserId = user.Id,
                                IsActive = true
                            });

                            await _context.SaveChangesAsync();
                        }
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    failedRows.Add($"⚠️ الصف {row}: {ex.Message}");
                }
            }

            TempData["Success"] = $"✅ تم استيراد {successCount} مستخدم بنجاح.";
            if (failCount > 0)
                TempData["Error"] = $"⚠️ فشل {failCount} صف.\n{string.Join("\n", failedRows)}";

            return RedirectToAction(nameof(Index));
        }




        [HttpGet]
        public IActionResult DownloadStudentTemplate()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("StudentsTemplate");

            // ===============================
            // 1) عناوين الأعمدة
            // ===============================
            ws.Cells[1, 1].Value = "FullName";
            ws.Cells[1, 2].Value = "NationalID";
            ws.Cells[1, 3].Value = "Phone";
            ws.Cells[1, 4].Value = "Gender";
            ws.Cells[1, 5].Value = "School";
            ws.Cells[1, 6].Value = "Level";
            ws.Cells[1, 7].Value = "BranchId";
            ws.Cells[1, 8].Value = "ParentId";
            ws.Cells[1, 9].Value = "BatchId";

            // ===============================
            // 2) تنسيقات العناوين
            // ===============================
            using (var range = ws.Cells[1, 1, 1, 9])
            {
                range.Style.Font.Bold = true;
                range.Style.Font.Size = 12;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightSteelBlue);
            }

            // ===============================
            // 3) إضافات إيضاح للعمود الأول
            // ===============================
            ws.Cells[2, 1].Value = "مثال: أحمد محمد علي";
            ws.Cells[2, 2].Value = "1234567890";
            ws.Cells[2, 3].Value = "0501234567";
            ws.Cells[2, 4].Value = "Male/Female أو ذكر/أنثى";
            ws.Cells[2, 5].Value = "مدرسة الهدى";
            ws.Cells[2, 6].Value = "ثالث ثانوي";
            ws.Cells[2, 7].Value = "1";
            ws.Cells[2, 8].Value = "3 (اختياري)";
            ws.Cells[2, 9].Value = "12 (معرف الدفعة)";

            // تنسيقات عامة للصف الثاني
            using (var range = ws.Cells[2, 1, 2, 9])
            {
                range.Style.Font.Color.SetColor(System.Drawing.Color.DarkGray);
            }

            // ===============================
            // 4) ضبط أعرض الأعمدة تلقائيًا
            // ===============================
            ws.Cells.AutoFitColumns();

            // ===============================
            // 5) إرجاع الملف
            // ===============================
            var bytes = package.GetAsByteArray();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Students_Import_Template.xlsx");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            var currentRoles = await _userManager.GetRolesAsync(user);

            // 🛡️ حماية خاصة للمالك أو الأدمن العام الحقيقي فقط
            var immutableRoles = new[] { "Owner", "SuperAdmin" };
            if (currentRoles.Count == 1 && immutableRoles.Contains(currentRoles.First()))
            {
                TempData["Error"] = "❌ لا يمكن تعديل صلاحيات هذا المستخدم (مالك النظام أو الأدمن العام).";
                return RedirectToAction(nameof(Index));
            }



            if (string.IsNullOrWhiteSpace(id))
                return NotFound("⚠️ لم يتم تحديد المستخدم المطلوب حذفه.");

            try
            {
                // ✅ جلب المستخدم
                if (user == null)
                {
                    TempData["Error"] = "⚠️ المستخدم غير موجود.";
                    return RedirectToAction(nameof(Index));
                }

                // ✅ حذف الطالب المرتبط (إن وجد)
                var student = await _db.Students.FirstOrDefaultAsync(s => s.UserId == id);
                if (student != null)
                {
                    // حذف أي علاقات أخرى مرتبطة بالطالب أولاً (اختياري)
                    var enrollments = _db.StudentBatchEnrollments.Where(e => e.StudentID == student.StudentID);
                    _db.StudentBatchEnrollments.RemoveRange(enrollments);

                    _db.Students.Remove(student);
                }

                // ✅ حذف المستخدم نفسه
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    TempData["Success"] = $"✅ تم حذف المستخدم ({user.FullName}) بنجاح.";
                }
                else
                {
                    TempData["Error"] = "❌ لم يتم حذف المستخدم: " + string.Join(", ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "⚠️ حدث خطأ أثناء الحذف: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }




        // =================== DataTables Source (متوافق 2014) ===================
        // =================== DataTables Source (متوافق 2014) ===================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoadUsersDataDT(
            [FromForm] DTRequest req,
            [FromForm] string roleFilter,
            [FromForm] string isActive,
            [FromForm] string isStudent,
            [FromForm] string searchText,
            [FromForm] string withoutRoles,
            [FromForm] string batchId)
        {
            try
            {
                if (req.Length <= 0)
                    req.Length = 25;

                var where = new StringBuilder(@"WHERE 1=1
                AND NOT (
                    s.[StudentID] IS NOT NULL
                    AND EXISTS (
                        SELECT 1 FROM [StudentBatchEnrollments] sbe_chk
                        WHERE sbe_chk.[StudentID] = s.[StudentID]
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM [StudentBatchEnrollments] sbe_act
                        INNER JOIN [Batches] b_act ON sbe_act.[BatchId] = b_act.[Id]
                        WHERE sbe_act.[StudentID] = s.[StudentID]
                          AND (b_act.[IsArchived] = 0 OR b_act.[IsArchived] IS NULL)
                    )
                )");
                var parameters = new List<SqlParameter>();

                // =========================
                // فلتر الحالة
                // =========================
                if (!string.IsNullOrWhiteSpace(isActive))
                {
                    where.Append(" AND a.[IsActive] = @p_isActive");
                    parameters.Add(new SqlParameter("@p_isActive", isActive == "true"));
                }

                // =========================
                // فلتر الطلاب
                // =========================
                if (!string.IsNullOrWhiteSpace(isStudent))
                {
                    if (isStudent == "true")
                        where.Append(" AND s.[StudentID] IS NOT NULL");
                    else if (isStudent == "false")
                        where.Append(" AND s.[StudentID] IS NULL");
                }

                // =========================
                // فلتر بلا أدوار
                // =========================
                if (!string.IsNullOrWhiteSpace(withoutRoles) && withoutRoles == "true")
                {
                    where.Append(@"
                AND NOT EXISTS (
                    SELECT 1
                    FROM [AspNetUserRoles] ur0
                    WHERE ur0.[UserId] = a.[Id]
                )");
                }

                // =========================
                // فلتر الدور
                // =========================
                if (!string.IsNullOrWhiteSpace(roleFilter))
                {
                    where.Append(@"
                AND EXISTS (
                    SELECT 1
                    FROM [AspNetUserRoles] ur
                    INNER JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                    WHERE ur.[UserId] = a.[Id]
                    AND r.[Name] = @p_role
                )");

                    parameters.Add(new SqlParameter("@p_role", roleFilter.Trim()));
                }

                // =========================
                // فلتر الدفعة
                // =========================
                if (!string.IsNullOrWhiteSpace(batchId) && int.TryParse(batchId, out int batchIdInt) && batchIdInt > 0)
                {
                    where.Append(@"
                AND s.[StudentID] IS NOT NULL
                AND EXISTS (
                    SELECT 1 FROM [StudentBatchEnrollments] sbe_b
                    WHERE sbe_b.[StudentID] = s.[StudentID]
                    AND sbe_b.[BatchId] = @p_batchId
                )");
                    parameters.Add(new SqlParameter("@p_batchId", batchIdInt));
                }

                // =========================
                // البحث
                // مهم:
                // لا نضيف شرط البحث الرقمي إلا لو المستخدم كتب أرقام فعلاً
                // لأن LIKE '%%' كان يجعل البحث يرجع كل النتائج
                // =========================
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var cleanSearch = searchText.Trim();
                    var digitsOnly = new string(cleanSearch.Where(char.IsDigit).ToArray());

                    if (!string.IsNullOrWhiteSpace(digitsOnly))
                    {
                        where.Append(@"
                    AND (
                        a.[FullName] LIKE @p_search
                        OR a.[Email] LIKE @p_search
                        OR a.[UserName] LIKE @p_search
                        OR ISNULL(a.[NationalID], '') LIKE @p_search
                        OR REPLACE(REPLACE(REPLACE(ISNULL(a.[NationalID], ''), ' ', ''), '-', ''), '.', '') LIKE @p_digits
                    )");

                        parameters.Add(new SqlParameter("@p_search", "%" + cleanSearch + "%"));
                        parameters.Add(new SqlParameter("@p_digits", "%" + digitsOnly + "%"));
                    }
                    else
                    {
                        where.Append(@"
                    AND (
                        a.[FullName] LIKE @p_search
                        OR a.[Email] LIKE @p_search
                        OR a.[UserName] LIKE @p_search
                        OR ISNULL(a.[NationalID], '') LIKE @p_search
                    )");

                        parameters.Add(new SqlParameter("@p_search", "%" + cleanSearch + "%"));
                    }
                }

                // =========================
                // إجمالي كل المستخدمين
                // =========================
                int recordsTotal;
                using (var cmd = _db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(1) FROM [AspNetUsers]";

                    if (cmd.Connection.State != System.Data.ConnectionState.Open)
                        await cmd.Connection.OpenAsync();

                    recordsTotal = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // =========================
                // إجمالي بعد الفلترة
                // =========================
                int recordsFiltered;
                using (var cmd = _db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = $@"
                SELECT COUNT(1)
                FROM [AspNetUsers] a
                LEFT JOIN [Students] s ON a.[Id] = s.[UserId]
                {where}";

                    foreach (var p in parameters)
                        cmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));

                    if (cmd.Connection.State != System.Data.ConnectionState.Open)
                        await cmd.Connection.OpenAsync();

                    recordsFiltered = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // =========================
                // إحصائيات الكروت
                // =========================
                int totalUsers = 0;
                int activeUsers = 0;
                int inactiveUsers = 0;
                int studentsCount = 0;
                int withoutRolesCount = 0;

                using (var cmd = _db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT
                    (SELECT COUNT(1) FROM [AspNetUsers]) AS TotalUsers,
                    (SELECT COUNT(1) FROM [AspNetUsers] WHERE [IsActive] = 1) AS ActiveUsers,
                    (SELECT COUNT(1) FROM [AspNetUsers] WHERE [IsActive] = 0) AS InactiveUsers,
                    (SELECT COUNT(1) FROM [Students]) AS StudentsCount,
                    (
                        SELECT COUNT(1)
                        FROM [AspNetUsers] a
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM [AspNetUserRoles] ur
                            WHERE ur.[UserId] = a.[Id]
                        )
                    ) AS WithoutRolesCount";

                    if (cmd.Connection.State != System.Data.ConnectionState.Open)
                        await cmd.Connection.OpenAsync();

                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        totalUsers = Convert.ToInt32(reader["TotalUsers"]);
                        activeUsers = Convert.ToInt32(reader["ActiveUsers"]);
                        inactiveUsers = Convert.ToInt32(reader["InactiveUsers"]);
                        studentsCount = Convert.ToInt32(reader["StudentsCount"]);
                        withoutRolesCount = Convert.ToInt32(reader["WithoutRolesCount"]);
                    }
                }

                // =========================
                // الصفحة المطلوبة
                // =========================
                int fromRow = req.Start + 1;
                int toRow = req.Start + req.Length;

                var pageParams = parameters
                    .Select(p => new SqlParameter(p.ParameterName, p.Value))
                    .ToList();

                pageParams.Add(new SqlParameter("@p_from", fromRow));
                pageParams.Add(new SqlParameter("@p_to", toRow));

                var rows = new List<object>();

                using (var cmd = _db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = $@";
WITH base AS (
    SELECT
        a.[Id],
        ISNULL(a.[FullName], '') AS FullName,
        ISNULL(a.[Email], '') AS Email,
        ISNULL(a.[NationalID], '') AS NationalID,
        a.[IsActive],
        a.[LastLoginAt],
        s.[StudentID] AS StudentId,
        ROW_NUMBER() OVER (
            ORDER BY 
                CASE WHEN a.[LastLoginAt] IS NULL THEN 0 ELSE 1 END DESC,
                a.[LastLoginAt] DESC,
                a.[FullName] ASC
        ) AS rn
    FROM [AspNetUsers] a
    LEFT JOIN [Students] s ON a.[Id] = s.[UserId]
    {where}
)
SELECT
    b.[Id],
    b.[FullName],
    b.[Email],
    b.[NationalID],
    b.[IsActive],
    b.[LastLoginAt],
    b.[StudentId],
    CASE WHEN b.[StudentId] IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS IsStudent,

    ISNULL((
        SELECT STUFF((
            SELECT ',' + r.[Name]
            FROM [AspNetUserRoles] ur
            INNER JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
            WHERE ur.[UserId] = b.[Id]
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 1, '')
    ), '') AS RolesCsv,

    ISNULL((
        SELECT STUFF((
            SELECT '||' + bt.[Name]
            FROM [StudentBatchEnrollments] e
            INNER JOIN [Batches] bt ON e.[BatchId] = bt.[Id]
            WHERE e.[StudentID] = b.[StudentId]
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 2, '')
    ), '') AS BatchNamesCsv

FROM base b
WHERE b.rn BETWEEN @p_from AND @p_to
ORDER BY b.rn;";

                    foreach (var p in pageParams)
                        cmd.Parameters.Add(p);

                    if (cmd.Connection.State != System.Data.ConnectionState.Open)
                        await cmd.Connection.OpenAsync();

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var rolesCsv = reader["RolesCsv"] as string ?? "";
                        var batchNamesCsv = reader["BatchNamesCsv"] as string ?? "";

                        var roles = string.IsNullOrWhiteSpace(rolesCsv)
                            ? new List<string>()
                            : rolesCsv
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim())
                                .ToList();

                        var batchNames = string.IsNullOrWhiteSpace(batchNamesCsv)
                            ? new List<string>()
                            : batchNamesCsv
                                .Split("||", StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim())
                                .ToList();

                        var lastLoginTicks = 0L;

                        if (reader["LastLoginAt"] != DBNull.Value)
                        {
                            lastLoginTicks = ((DateTime)reader["LastLoginAt"]).Ticks;
                        }

                        rows.Add(new
                        {
                            id = reader["Id"].ToString(),
                            fullName = reader["FullName"].ToString(),
                            email = reader["Email"].ToString(),
                            nationalID = reader["NationalID"].ToString(),
                            isActive = Convert.ToBoolean(reader["IsActive"]),
                            isStudent = Convert.ToBoolean(reader["IsStudent"]),
                            lastLoginAtTicks = lastLoginTicks,
                            roles = roles,
                            batchNames = batchNames,
                            enrolledBatchesCount = batchNames.Count
                        });
                    }
                }

                return Json(new
                {
                    draw = req.Draw,
                    recordsTotal = recordsTotal,
                    recordsFiltered = recordsFiltered,
                    data = rows,
                    summary = new
                    {
                        totalUsers,
                        activeUsers,
                        inactiveUsers,
                        studentsCount,
                        withoutRolesCount
                    }
                });
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;

                return Json(new
                {
                    error = "⚠️ حصل خطأ أثناء تحميل المستخدمين: " + ex.GetBaseException().Message
                });
            }
        }
        // ======================== Edit (GET) ========================
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);



            if (user == null) return NotFound();

            var studentRow = await _db.Students
     .Where(s => s.UserId == id)
     .Select(s => new
     {
         s.StudentID,
         s.BranchId,
         s.ParentId,
         s.Age,
         s.School,
         s.Level
     })
     .FirstOrDefaultAsync();


            int[] selectedBatchIds = Array.Empty<int>();
            if (studentRow != null)
            {
                selectedBatchIds = await (
                    from e in _db.StudentBatchEnrollments
                    where e.StudentID == studentRow.StudentID
                    select e.BatchId
                ).ToArrayAsync();
            }

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                WhatsAppNumber = user.WhatsAppNumber,
                NationalID = user.NationalID,
                Gender = user.Gender,
                IsActive = user.IsActive,
                ExistingImagePath = user.ProfileImagePath,

                // ✅ الحقول التي كانت فارغة
                Age = studentRow?.Age,
                School = studentRow?.School,
                Level = studentRow?.Level,


                IsStudent = studentRow != null,
                BranchId = studentRow?.BranchId,
                ParentId = studentRow?.ParentId,
                SelectedBatchIds = selectedBatchIds,

                Branches = await _db.Branches.OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync(),
                Batches = await _db.Batches.OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync(),
                Parents = await _db.Parents.OrderBy(p => p.FullName)
                    .Select(p => new SelectListItem { Value = p.ParentID.ToString(), Text = p.FullName }).ToListAsync()
            };

            return View(model);
        }

        // ======================== Reset Password ========================
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var model = new ResetPasswordByAdminViewModel { UserId = user.Id, Email = user.Email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordByAdminViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            model.Email = user.Email;

            if (!ModelState.IsValid)
                return View(model);

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "تم تغيير كلمة المرور بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ======================== Edit (POST) ========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model, IFormFile? ProfileImage)
        {




            if (!ModelState.IsValid)
            {
                model.Branches = await _db.Branches.OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync();
                model.Batches = await _db.Batches.OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync();
                model.Parents = await _db.Parents.OrderBy(p => p.FullName)
                    .Select(p => new SelectListItem { Value = p.ParentID.ToString(), Text = p.FullName }).ToListAsync();
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // تحديث بيانات الحساب
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.PhoneNumber = model.PhoneNumber;
            user.WhatsAppNumber = model.WhatsAppNumber;
            user.NationalID = model.NationalID;
            user.Gender = model.Gender;
            user.IsActive = model.IsActive;

            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine("wwwroot", "images", "profiles");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ProfileImage.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ProfileImage.CopyToAsync(stream);

                user.ProfileImagePath = $"/images/profiles/{fileName}";
            }

            var updateRes = await _userManager.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                foreach (var e in updateRes.Errors) ModelState.AddModelError("", e.Description);
                model.Branches = await _db.Branches.OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync();
                model.Batches = await _db.Batches.OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync();
                model.Parents = await _db.Parents.OrderBy(p => p.FullName)
                    .Select(p => new SelectListItem { Value = p.ParentID.ToString(), Text = p.FullName }).ToListAsync();
                return View(model);
            }

            var student = await _db.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (model.IsStudent)
            {
                // ✅ تحقق إضافي: المدرسة والمستوى إلزامي فقط لو المستخدم طالب
                if (string.IsNullOrWhiteSpace(model.School))
                    ModelState.AddModelError(nameof(model.School), "اسم المدرسة مطلوب عند تفعيل الطالب.");

                if (string.IsNullOrWhiteSpace(model.Level))
                    ModelState.AddModelError(nameof(model.Level), "المستوى مطلوب عند تفعيل الطالب.");

                if (!model.BranchId.HasValue || !await _db.Branches.AnyAsync(b => b.Id == model.BranchId.Value))
                    ModelState.AddModelError(nameof(model.BranchId), "فرع غير صحيح.");

                if (model.SelectedBatchIds != null)
                {
                    foreach (var bid in model.SelectedBatchIds)
                        if (!await _db.Batches.AnyAsync(b => b.Id == bid))
                            ModelState.AddModelError(nameof(model.SelectedBatchIds), $"دفعة غير صحيحة: {bid}");
                }

                if (!ModelState.IsValid)
                {
                    model.Branches = await _db.Branches.OrderBy(b => b.Name)
                        .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync();
                    model.Batches = await _db.Batches.OrderBy(b => b.Name)
                        .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name }).ToListAsync();
                    model.Parents = await _db.Parents.OrderBy(p => p.FullName)
                        .Select(p => new SelectListItem { Value = p.ParentID.ToString(), Text = p.FullName }).ToListAsync();
                    return View(model);
                }

                // إنشاء أو تحديث الطالب
                if (student == null)
                {
                    var newStudent = new Student
                    {
                        UserId = user.Id,
                        FullName = user.FullName ?? "",
                        NationalID = user.NationalID ?? "",
                        Gender = user.Gender.ToString(),
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        WhatsAppNumber = user.WhatsAppNumber,
                        Age = model.Age ?? 6,
                        School = model.School ?? "غير محدد",
                        Level = model.Level ?? "غير محدد",
                        BranchId = model.BranchId!.Value,
                        ParentId = model.ParentId,
                        RegistrationDate = DateTime.UtcNow,
                        EnrollmentStatus = "نشط",
                        IsRegular = true
                    };

                    _db.Students.Add(newStudent);
                    await _db.SaveChangesAsync();
                    student = newStudent;
                }
                else
                {
                    if (model.BranchId.HasValue) student.BranchId = model.BranchId.Value;
                    student.ParentId = model.ParentId;
                    student.School = model.School ?? "غير محدد";
                    student.Level = model.Level ?? "غير محدد";
                    student.Age = model.Age ?? student.Age;

                    _db.Students.Update(student);
                    await _db.SaveChangesAsync();
                }

                // مزامنة عضويات الدفعات
                var current = await _db.StudentBatchEnrollments
                    .Where(e => e.StudentID == student.StudentID)
                    .Select(e => new { e.Id, e.BatchId })
                    .ToListAsync();

                var selected = (model.SelectedBatchIds ?? Array.Empty<int>()).ToHashSet();

                var removeIds = current.Where(c => !selected.Contains(c.BatchId)).Select(c => c.Id).ToArray();
                if (removeIds.Length > 0)
                {
                    var toRemove = _db.StudentBatchEnrollments.Where(e => removeIds.Contains(e.Id));
                    _db.StudentBatchEnrollments.RemoveRange(toRemove);
                }

                var currentBatches = current.Select(c => c.BatchId).ToHashSet();
                var toAdd = selected.Where(bid => !currentBatches.Contains(bid)).ToArray();
                foreach (var bid in toAdd)
                    _db.StudentBatchEnrollments.Add(new StudentBatchEnrollment
                    {
                        StudentID = student.StudentID,
                        BatchId = bid,
                        EnrolledAt = DateTime.UtcNow,
                        Status = "Active"
                    });

                await _db.SaveChangesAsync();

                // ضمان دور Student
                if (!await _roleManager.RoleExistsAsync("Student"))
                    await _roleManager.CreateAsync(new IdentityRole("Student"));
                if (!await _userManager.IsInRoleAsync(user, "Student"))
                    await _userManager.AddToRoleAsync(user, "Student");
            }

            TempData["Success"] = "تم تحديث بيانات المستخدم بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ======================== Toggle Active ========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = user.IsActive ? "تم تفعيل المستخدم." : "تم إلغاء تفعيل المستخدم.";
            return RedirectToAction(nameof(Index));
        }

        // ======================== Assign Roles ========================
        [HttpGet]
        public async Task<IActionResult> AssignRoles(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);

            // 🎯 الأدوار الحساسة فقط
            var sensitiveRoles = new[] { "Owner", "SuperAdmin", "Developer" };

            var model = new AssignRolesViewModel
            {
                UserId = user.Id,
                Email = user.Email,

                // ✅ هل نحتاج رسالة تأكيد؟
                RequiresConfirmation = userRoles.Any(r => sensitiveRoles.Contains(r)),

                Roles = RoleNamesHelper.RoleDisplay
                    .Select(r => new RoleSelectionViewModel
                    {
                        RoleName = r.Key,
                        DisplayName = r.Value,
                        IsSelected = userRoles.Contains(r.Key)
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // ============================
            // 1️⃣ تحميل جميع المستخدمين
            // ============================
            var users = await _db.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.NationalID,
                    u.IsActive,
                    u.LastLoginAt
                })
                .ToListAsync();

            // ============================
            // 2️⃣ تحميل الطلاب المرتبطين بالمستخدمين
            // ============================
            var students = await _db.Students
                .AsNoTracking()
                .Select(s => new { s.StudentID, s.UserId })
                .ToListAsync();

            var studentByUser = students
                .Where(s => s.UserId != null)
                .GroupBy(s => s.UserId!)
                .ToDictionary(g => g.Key, g => g.First().StudentID);

            // ============================
            // 3️⃣ تحميل جميع تسجيلات الدفعات
            // ============================
            var enrollments = await _db.StudentBatchEnrollments
                .AsNoTracking()
                .Select(e => new { e.StudentID, e.BatchId })
                .ToListAsync();

            // ============================
            // 4️⃣ تحميل الدفعات (مع حقل الأرشفة)
            // ============================
            var batches = await _db.Batches
                .AsNoTracking()
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.IsArchived,
                    b.ArchivedAt,
                    CourseName = b.Course != null ? b.Course.Name : null,
                    BranchName = b.Branch != null ? b.Branch.Name : null
                })
                .ToListAsync();

            var batchLookup = batches.ToDictionary(b => b.Id);
            var archivedBatchIds = batches.Where(b => b.IsArchived).Select(b => b.Id).ToHashSet();

            // ============================
            // 5️⃣ تحديد معرفات الطلاب المؤرشفين حصراً
            // (لديهم تسجيلات AND كلها في دفعات مؤرشفة)
            // ============================
            var archivedOnlyStudentIds = studentByUser.Values
                .Where(sid =>
                {
                    var studentEnrollments = enrollments.Where(e => e.StudentID == sid).ToList();
                    return studentEnrollments.Any()
                        && studentEnrollments.All(e => archivedBatchIds.Contains(e.BatchId));
                })
                .ToHashSet();

            // ============================
            // 6️⃣ تحميل الأدوار مرة واحدة
            // ============================
            var userRoles = await (
                from ur in _db.UserRoles
                join r in _db.Roles on ur.RoleId equals r.Id
                select new { ur.UserId, r.Name }
            ).ToListAsync();

            var rolesLookup = userRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

            // ============================
            // 7️⃣ بناء القائمة الرئيسية (بدون طلاب الأرشيف)
            // ============================
            var result = new List<QdratNew.ViewModels.Users.UserRowDto>();

            foreach (var u in users)
            {
                bool isStudent = studentByUser.TryGetValue(u.Id, out int studentId);

                // استثناء الطلاب المؤرشفين حصراً من الجدول الرئيسي
                if (isStudent && archivedOnlyStudentIds.Contains(studentId))
                    continue;

                List<string> userBatchNames = new();

                if (isStudent)
                {
                    foreach (var e in enrollments.Where(e => e.StudentID == studentId))
                    {
                        if (batchLookup.TryGetValue(e.BatchId, out var b))
                            userBatchNames.Add(b.Name);
                    }
                }

                result.Add(new QdratNew.ViewModels.Users.UserRowDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    NationalID = u.NationalID,
                    IsActive = u.IsActive,
                    LastLoginAtTicks = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.Ticks : 0,
                    Roles = rolesLookup.ContainsKey(u.Id) ? rolesLookup[u.Id] : new List<string>(),
                    IsStudent = isStudent,
                    EnrolledBatchesCount = userBatchNames.Count,
                    BatchNames = userBatchNames
                });
            }

            // ============================
            // 8️⃣ بناء قائمة الدفعات المؤرشفة مع عدد طلابها
            // ============================
            var archivedBatchSummaries = batches
                .Where(b => b.IsArchived)
                .Select(b =>
                {
                    var batchStudentIds = enrollments
                        .Where(e => e.BatchId == b.Id)
                        .Select(e => e.StudentID)
                        .Distinct()
                        .ToList();

                    return new ArchivedBatchSummaryDto
                    {
                        BatchId = b.Id,
                        BatchName = b.Name,
                        CourseName = b.CourseName,
                        BranchName = b.BranchName,
                        ArchivedAt = b.ArchivedAt,
                        StudentsCount = batchStudentIds.Count
                    };
                })
                .OrderByDescending(b => b.ArchivedAt)
                .ToList();

            var activeBatches = batches
                .Where(b => !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new ActiveBatchFilterDto
                {
                    Id = b.Id,
                    Name = b.Name
                })
                .ToList();

            var vm = new UsersListViewModel
            {
                Title = "📋 مستخدمو المنصة",
                FilterDescription = null,
                Users = result,
                TotalUsers = users.Count,
                ActiveUsers = users.Count(x => x.IsActive),
                InactiveUsers = users.Count(x => !x.IsActive),
                StudentsCount = students.Count,
                ActiveBatches = activeBatches,
                ArchivedBatches = archivedBatchSummaries,
                ArchivedStudentsCount = archivedOnlyStudentIds.Count
            };

            return View(vm);
        }
        [HttpGet]
        public async Task<IActionResult> UsersWithoutRoles()
        {
            var usersWithoutRoles = await (
                from u in _db.Users
                where !(from ur in _db.UserRoles select ur.UserId).Contains(u.Id)
                select new QdratNew.ViewModels.Users.UserRowDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    NationalID = u.NationalID,
                    IsActive = u.IsActive,
                    LastLoginAtTicks = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.Ticks : 0,
                    Roles = new List<string>(),
                    IsStudent = _db.Students.Any(s => s.UserId == u.Id),
                    EnrolledBatchesCount = (
                        from s in _db.Students
                        join e in _db.StudentBatchEnrollments on s.StudentID equals e.StudentID
                        where s.UserId == u.Id
                        select e.BatchId).Distinct().Count()
                }
            ).ToListAsync();

            var vm = new UsersListViewModel
            {
                Title = "⚠️ المستخدمون بلا أدوار",
                FilterDescription = $"عدد المستخدمين بدون دور: {usersWithoutRoles.Count}",
                Users = usersWithoutRoles,
                TotalUsers = await _db.Users.CountAsync(),
                ActiveUsers = await _db.Users.CountAsync(u => u.IsActive),
                InactiveUsers = await _db.Users.CountAsync(u => !u.IsActive),
                StudentsCount = await _db.Students.CountAsync()
            };

            return View("Index", vm);
        }



        // ======================== Manage Permissions ========================
        [HttpGet]
        public async Task<IActionResult> ManagePermissions(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (!await IsOwnerOrSuperAdminOrDeveloperAsync(currentUser))
            {
                TempData["Error"] = "❌ لا يمكنك الوصول إلى إدارة الصلاحيات.";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var claims = await _userManager.GetClaimsAsync(user);
            var model = new QdratNew.ViewModels.Users.ManagePermissionsViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Permissions = new List<QdratNew.ViewModels.Users.PermissionCheckboxVm>
        {
            new("CanView", "عرض البيانات", claims.Any(c => c.Value == "CanView")),
            new("CanCreate", "إضافة", claims.Any(c => c.Value == "CanCreate")),
            new("CanEdit", "تعديل", claims.Any(c => c.Value == "CanEdit")),
            new("CanDelete", "حذف", claims.Any(c => c.Value == "CanDelete")),
            new("CanPrintReports", "طباعة التقارير", claims.Any(c => c.Value == "CanPrintReports"))
        }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagePermissions(QdratNew.ViewModels.Users.ManagePermissionsViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (!await IsOwnerOrSuperAdminOrDeveloperAsync(currentUser))
            {
                TempData["Error"] = "❌ لا يمكنك تعديل صلاحيات المستخدمين.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var existingClaims = await _userManager.GetClaimsAsync(user);

            // حذف كل الصلاحيات القديمة
            foreach (var claim in existingClaims.Where(c => c.Type == "Permission"))
                await _userManager.RemoveClaimAsync(user, claim);

            // إضافة الجديدة فقط
            foreach (var perm in model.Permissions.Where(p => p.IsGranted))
                await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("Permission", perm.Value));

            TempData["Success"] = "✅ تم تحديث صلاحيات المستخدم بنجاح.";
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRoles(AssignRolesViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["Error"] = "⚠️ لم يتم العثور على المستخدم.";
                return RedirectToAction(nameof(Index));
            }

            // 🔹 الأدوار الحالية
            var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();

            // 🔹 الأدوار المختارة
            var selectedRoles = model.Roles
                .Where(r => r.IsSelected && !string.IsNullOrWhiteSpace(r.RoleName))
                .Select(r => r.RoleName!.Trim())
                .Distinct()
                .ToList();

            // 🎯 الأدوار الحساسة
            var sensitiveRoles = new[] { "Owner", "SuperAdmin", "Developer" };

            bool touchesSensitiveRoles =
                currentRoles.Any(r => sensitiveRoles.Contains(r)) ||
                selectedRoles.Any(r => sensitiveRoles.Contains(r));

            // 🔁 مهم: إعادة تعيين الفلاج حتى لا يختفي الحقل
            model.RequiresConfirmation = touchesSensitiveRoles;

            // 🛡️ تأكيد إضافي فقط عند الحاجة
            if (touchesSensitiveRoles)
            {
                if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
                {
                    ModelState.AddModelError(nameof(model.ConfirmPassword),
                        "تأكيد كلمة المرور مطلوب لتعديل أدوار حساسة.");
                    return View(model);
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null ||
                    !await _userManager.CheckPasswordAsync(currentUser, model.ConfirmPassword))
                {
                    ModelState.AddModelError(nameof(model.ConfirmPassword),
                        "كلمة المرور غير صحيحة.");
                    return View(model);
                }
            }

            // 🧹 إزالة الأدوار
            var rolesToRemove = currentRoles.Except(selectedRoles).ToList();
            foreach (var role in rolesToRemove)
                await _userManager.RemoveFromRoleAsync(user, role);

            // ➕ إضافة الأدوار
            var rolesToAdd = selectedRoles.Except(currentRoles).ToList();
            foreach (var role in rolesToAdd)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));

                await _userManager.AddToRoleAsync(user, role);
            }

            // 🎓 التأكد من وجود سجل مدرب لكل مستخدم يحمل دور "Instructor"
            if (selectedRoles.Contains("Instructor"))
            {
                bool alreadyInstructor = await _context.Instructors.AnyAsync(i => i.UserId == user.Id);
                if (!alreadyInstructor)
                {
                    _context.Instructors.Add(new Instructor
                    {
                        FullName = user.FullName ?? user.Email ?? "مدرب جديد",
                        Email = user.Email ?? string.Empty,
                        PhoneNumber = user.PhoneNumber,
                        WhatsAppNumber = user.WhatsAppNumber,
                        Gender = user.Gender,
                        NationalID = user.NationalID,
                        Specialization = "مدرب",
                        UserId = user.Id,
                        IsActive = true
                    });

                    await _context.SaveChangesAsync();
                }
            }

            // 🧹 إزالة Claims القديمة
            var existingClaims = await _userManager.GetClaimsAsync(user);
            foreach (var claim in existingClaims.Where(c => c.Type == "Permission"))
                await _userManager.RemoveClaimAsync(user, claim);

            // 🔐 خريطة الصلاحيات
            var rolePermissionsMap = new Dictionary<string, List<string>>
            {
                ["Owner"] = new() { "CanView", "CanCreate", "CanEdit", "CanDelete", "CanPrintReports", "CanAccessSettings" },
                ["SuperAdmin"] = new() { "CanView", "CanCreate", "CanEdit", "CanDelete", "CanPrintReports", "CanAccessSettings" },
                ["Developer"] = new() { "CanView", "CanCreate", "CanEdit", "CanDelete", "CanPrintReports", "CanAccessSettings" },
                ["Admin"] = new() { "CanView", "CanCreate", "CanEdit", "CanDelete" },
                ["DataEntry"] = new() { "CanView", "CanCreate", "CanEdit" },
                ["Instructor"] = new() { "CanAccessInstructorArea", "CanView", "CanEdit" },
                ["Student"] = new() { "CanAccessStudentArea", "CanView" },
                ["Partner"] = new() { "CanViewPerformance", "CanView" },
                ["Employee"] = new() { "CanViewPerformance", "CanView", "CanEdit" },
                ["Parent"] = new() { "CanAccessParentArea", "CanView" }
            };

            foreach (var role in selectedRoles)
            {
                if (rolePermissionsMap.TryGetValue(role, out var perms))
                {
                    foreach (var p in perms)
                        await _userManager.AddClaimAsync(user,
                            new System.Security.Claims.Claim("Permission", p));
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ تم حفظ الأدوار والصلاحيات بنجاح.";
            return RedirectToAction(nameof(Index));
        }



        public async Task<IActionResult> AssignPartner(int partnerId)
        {
            var partner = await _db.Partners
                .Where(p => p.Id == partnerId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync();

            if (partner == null)
                return NotFound();

            // 🔹 Role الشريك
            var partnerRoleId = await _db.Roles
                .Where(r => r.Name == "Partner")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (partnerRoleId == null)
                return NotFound("Partner role not found");

            // 🔹 جميع مستخدمي Role = Partner (للإضافة)
            var availableUsers = await _db.UserRoles
                .Where(ur => ur.RoleId == partnerRoleId)
                .OrderByDescending(ur => ur.UserId)
                .Join(
                    _db.Users,
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => new SelectListItem
                    {
                        Value = u.Id,
                        Text = $"{u.FullName} - {u.Email}"
                    }
                )
                .ToListAsync();

            // 🔹 المستخدمون المرتبطون فعليًا بهذا الشريك
            var assignedUsers = await _db.UserPartners
                .Where(up => up.PartnerId == partnerId)
                .Join(
                    _db.Users,
                    up => up.UserId,
                    u => u.Id,
                    (up, u) => new AssignedPartnerUserViewModel
                    {
                        UserId = u.Id,
                        FullName = u.FullName,
                        Email = u.Email
                    }
                )
                .ToListAsync();

            var model = new AssignPartnerViewModel
            {
                SelectedPartnerId = partner.Id,
                PartnerName = partner.Name,

                Users = availableUsers,
                AssignedUsers = assignedUsers
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPartner(AssignPartnerViewModel model)
        {
            if (string.IsNullOrEmpty(model.SelectedUserId))
            {
                ModelState.AddModelError("", "يرجى اختيار مستخدم");
                return View(model);
            }

            if (!model.SelectedPartnerId.HasValue)
            {
                ModelState.AddModelError("", "يرجى اختيار شريك صحيح");
                return View(model);
            }

            int partnerId = model.SelectedPartnerId.Value;

            var user = await _userManager.FindByIdAsync(model.SelectedUserId);
            if (user == null)
                return NotFound();

            // ✅ منع تكرار ربط نفس المستخدم بنفس الشريك
            bool exists = await _db.UserPartners.AnyAsync(up =>
                up.UserId == user.Id &&
                up.PartnerId == partnerId);

            if (exists)
            {
                TempData["Info"] = "هذا المستخدم مرتبط بالفعل بهذا الشريك.";
                return RedirectToAction(nameof(AssignPartner), new { partnerId });
            }

            _db.UserPartners.Add(new UserPartner
            {
                UserId = user.Id,
                PartnerId = partnerId
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = "تم ربط المستخدم بالشريك بنجاح";
            return RedirectToAction(nameof(AssignPartner), new { partnerId });
        }



        [HttpGet]
        public async Task<IActionResult> PartnerStudents(int partnerId)
        {
            var partner = await _db.Partners
                .Where(p => p.Id == partnerId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync();

            if (partner == null)
                return NotFound();

            var students = await _db.Students
                .Join(
                    _db.PartnerSubscriptionPeriods,
                    s => s.PartnerSubscriptionPeriodId,
                    psp => psp.Id,
                    (s, psp) => new { Student = s, Period = psp }
                )
                .Join(
                    _db.PartnerSubscriptions,
                    sp => sp.Period.PartnerSubscriptionId,
                    ps => ps.Id,
                    (sp, ps) => new { sp.Student, Subscription = ps }
                )
                .Where(x => x.Subscription.PartnerId == partnerId)
                .Select(x => new PartnerStudentListViewModel
                {
                    StudentId = x.Student.StudentID,
                    UserId = x.Student.UserId,
                    FullName = x.Student.FullName,
                    NationalID = x.Student.NationalID,   // ✅ هنا
                    Phone = x.Student.PhoneNumber,
                    School = x.Student.School,
                    Level = x.Student.Level
                })
                .OrderBy(s => s.FullName)
                .ToListAsync();

            var model = new PartnerStudentsPageViewModel
            {
                PartnerId = partner.Id,
                PartnerName = partner.Name,
                Students = students
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnassignPartnerUser(string userId, int partnerId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            var link = await _db.UserPartners
                .FirstOrDefaultAsync(up =>
                    up.UserId == userId &&
                    up.PartnerId == partnerId);

            if (link == null)
            {
                TempData["Info"] = "المستخدم غير مرتبط بهذه الشركة.";
                return RedirectToAction(nameof(AssignPartner), new { partnerId });
            }

            _db.UserPartners.Remove(link);
            await _db.SaveChangesAsync();

            TempData["Success"] = "تم استبعاد المستخدم من هذه الشركة بنجاح.";
            return RedirectToAction(nameof(AssignPartner), new { partnerId });
        }



        // ======================== DataTables DTO ========================
        public class DTRequest
        {
            public int Draw { get; set; }
            public int Start { get; set; }   // offset
            public int Length { get; set; }  // page size
        }

        // ======================== Archived Batch Students ========================
        [HttpGet]
        public async Task<IActionResult> ArchivedBatchStudents(int batchId)
        {
            var batch = await _db.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId && b.IsArchived)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.ArchivedAt,
                    CourseName = b.Course != null ? b.Course.Name : null,
                    BranchName = b.Branch != null ? b.Branch.Name : null
                })
                .FirstOrDefaultAsync();

            if (batch == null)
                return NotFound();

            var students = await _db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId)
                .Join(_db.Students,
                    e => e.StudentID,
                    s => s.StudentID,
                    (e, s) => new
                    {
                        s.StudentID,
                        s.UserId,
                        s.FullName,
                        s.NationalID,
                        s.PhoneNumber,
                        s.School,
                        s.Level,
                        e.Status,
                        e.EnrolledAt
                    })
                .OrderBy(s => s.FullName)
                .ToListAsync();

            // تحقق هل الطالب لديه تسجيل نشط في دفعة غير مؤرشفة (سيعود للرئيسي)
            var studentIds = students.Select(s => s.StudentID).ToList();

            var activeEnrollmentStudents = await _db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => studentIds.Contains(e.StudentID))
                .Join(_db.Batches,
                    e => e.BatchId,
                    b => b.Id,
                    (e, b) => new { e.StudentID, b.IsArchived })
                .Where(x => !x.IsArchived)
                .Select(x => x.StudentID)
                .Distinct()
                .ToListAsync();

            var activeSet = activeEnrollmentStudents.ToHashSet();

            ViewBag.BatchId = batch.Id;
            ViewBag.BatchName = batch.Name;
            ViewBag.CourseName = batch.CourseName;
            ViewBag.BranchName = batch.BranchName;
            ViewBag.ArchivedAt = batch.ArchivedAt;

            var rows = students.Select(s => new
            {
                s.StudentID,
                s.UserId,
                s.FullName,
                s.NationalID,
                s.PhoneNumber,
                s.School,
                s.Level,
                s.Status,
                s.EnrolledAt,
                HasActiveEnrollment = activeSet.Contains(s.StudentID)
            }).ToList();

            return View(rows.Select(s => new ArchivedStudentRowDto
            {
                StudentId = s.StudentID,
                UserId = s.UserId,
                FullName = s.FullName ?? "",
                NationalID = s.NationalID,
                Phone = s.PhoneNumber,
                School = s.School,
                Level = s.Level,
                EnrollmentStatus = s.Status,
                EnrolledAt = s.EnrolledAt,
                HasActiveEnrollment = s.HasActiveEnrollment
            }).ToList());
        }

        // ======================== Partners List ========================
        [HttpGet]
        public async Task<IActionResult> PartnersList()
        {
            var partners = await _db.Partners.AsNoTracking().ToListAsync();

            var allUserPartners = await _db.UserPartners.AsNoTracking().ToListAsync();

            var studentCounts = await _db.Students
                .Where(s => s.PartnerSubscriptionPeriodId != null)
                .Join(_db.PartnerSubscriptionPeriods,
                    s => s.PartnerSubscriptionPeriodId,
                    psp => psp.Id,
                    (s, psp) => new { s, psp })
                .Join(_db.PartnerSubscriptions,
                    x => x.psp.PartnerSubscriptionId,
                    ps => ps.Id,
                    (x, ps) => new { x.s, ps })
                .GroupBy(x => x.ps.PartnerId)
                .Select(g => new { PartnerId = g.Key, Count = g.Count() })
                .ToListAsync();

            var partnerUserIds = allUserPartners.Select(up => up.UserId).Distinct().ToList();

            var userRoles = await _db.UserRoles
                .Where(ur => partnerUserIds.Contains(ur.UserId))
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id,
                    (ur, r) => new { ur.UserId, r.Name })
                .ToListAsync();

            var rolesLookup = userRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

            var instructorRoles = new HashSet<string> { "Instructor" };
            var adminRoles = new HashSet<string> { "Partner", "Admin", "DataEntry", "SuperAdmin", "Owner" };

            var items = partners.Select(p =>
            {
                var pu = allUserPartners.Where(up => up.PartnerId == p.Id).ToList();

                int instructorsCount = pu.Count(up =>
                    rolesLookup.TryGetValue(up.UserId, out var r) && r.Any(x => instructorRoles.Contains(x)));

                int adminsCount = pu.Count(up =>
                    rolesLookup.TryGetValue(up.UserId, out var r) && r.Any(x => adminRoles.Contains(x)));

                int studentsCount = studentCounts.FirstOrDefault(sc => sc.PartnerId == p.Id)?.Count ?? 0;

                return new PartnerListItemDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    LogoPath = p.LogoPath,
                    IsActive = p.IsActive,
                    StudentsCount = studentsCount,
                    InstructorsCount = instructorsCount,
                    AdminsCount = adminsCount
                };
            }).ToList();

            return View(new PartnersListViewModel { Partners = items });
        }

        // ======================== Partner User Details ========================
        [HttpGet]
        public async Task<IActionResult> PartnerUserDetails(int partnerId)
        {
            var partner = await _db.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == partnerId);

            if (partner == null)
                return NotFound();

            var students = await _db.Students
                .Where(s => s.PartnerSubscriptionPeriodId != null)
                .Join(_db.PartnerSubscriptionPeriods,
                    s => s.PartnerSubscriptionPeriodId,
                    psp => psp.Id,
                    (s, psp) => new { s, psp })
                .Join(_db.PartnerSubscriptions,
                    x => x.psp.PartnerSubscriptionId,
                    ps => ps.Id,
                    (x, ps) => new { x.s, ps })
                .Where(x => x.ps.PartnerId == partnerId)
                .Select(x => new PartnerUserRowDto
                {
                    UserId = x.s.UserId,
                    StudentId = x.s.StudentID,
                    FullName = x.s.FullName,
                    NationalID = x.s.NationalID,
                    Phone = x.s.PhoneNumber,
                    School = x.s.School,
                    Level = x.s.Level,
                    IsActive = true
                })
                .OrderBy(s => s.FullName)
                .ToListAsync();

            var partnerUsersRaw = await _db.UserPartners
                .Where(up => up.PartnerId == partnerId)
                .Join(_db.Users, up => up.UserId, u => u.Id,
                    (up, u) => new
                    {
                        u.Id,
                        u.FullName,
                        u.Email,
                        u.NationalID,
                        u.PhoneNumber,
                        u.IsActive
                    })
                .ToListAsync();

            var partnerUserIdList = partnerUsersRaw.Select(x => x.Id).ToList();

            var userRoles = await _db.UserRoles
                .Where(ur => partnerUserIdList.Contains(ur.UserId))
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id,
                    (ur, r) => new { ur.UserId, r.Name })
                .ToListAsync();

            var rolesLookup = userRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

            var instructors = new List<PartnerUserRowDto>();
            var admins = new List<PartnerUserRowDto>();

            foreach (var u in partnerUsersRaw)
            {
                var roles = rolesLookup.TryGetValue(u.Id, out var r) ? r : new List<string>();
                var dto = new PartnerUserRowDto
                {
                    UserId = u.Id,
                    FullName = u.FullName ?? "",
                    Email = u.Email,
                    NationalID = u.NationalID,
                    Phone = u.PhoneNumber,
                    IsActive = u.IsActive,
                    Roles = roles
                };

                if (roles.Contains("Instructor"))
                    instructors.Add(dto);
                else
                    admins.Add(dto);
            }

            // ── Additional data for management tabs ──────────────────────────

            var partnerBranchIds = await _db.Branches
                .Where(b => b.PartnerId == partnerId)
                .Select(b => b.Id)
                .ToListAsync();

            var partnerBatches = await _db.Batches
                .Where(b => partnerBranchIds.Contains(b.BranchId) && !b.IsDeleted)
                .OrderBy(b => b.Name)
                .Select(b => new PartnerAdminBatchRow
                {
                    Id = b.Id,
                    Name = b.Name,
                    BranchId = b.BranchId,
                    IsActive = b.IsActive
                })
                .ToListAsync();

            var partnerBatchIds = partnerBatches.Select(b => b.Id).ToList();

            var partnerBranchRows = await _db.Branches
                .Where(b => b.PartnerId == partnerId)
                .OrderBy(b => b.Name)
                .Select(b => new PartnerAdminBranchRow { Id = b.Id, Name = b.Name })
                .ToListAsync();

            var activePeriodId = await _db.PartnerSubscriptionPeriods
                .Where(psp =>
                    psp.PartnerSubscription.PartnerId == partnerId &&
                    psp.StartDate <= DateTime.Today &&
                    psp.EndDate >= DateTime.Today)
                .Select(psp => (int?)psp.Id)
                .FirstOrDefaultAsync();

            // Homework drafts
            var homeworkDrafts = await _db.HomeworkDrafts
                .Where(hd => hd.PartnerId == partnerId && !hd.IsDeleted && !hd.IsArchived)
                .OrderByDescending(hd => hd.CreatedAt)
                .Select(hd => new PartnerAdminHomeworkDraftRow
                {
                    Id = hd.Id,
                    Title = hd.Title,
                    CreatedAt = hd.CreatedAt,
                    QuestionsCount = hd.Questions.Count()
                })
                .ToListAsync();

            // Sent HomeworkSets for partner's batches
            List<PartnerAdminHomeworkRow> homeworkSets;
            if (partnerBatchIds.Any())
            {
                var rawSets = await _db.HomeworkSets
                    .Where(hs => partnerBatchIds.Contains(hs.BatchId) && !hs.IsArchived)
                    .Select(hs => new
                    {
                        hs.Id,
                        hs.Title,
                        hs.BatchId,
                        BatchName = hs.Batch.Name,
                        hs.CreatedAt,
                        TotalStudents = hs.Students.Count(),
                        SubmittedCount = hs.Students.Count(s => s.IsSubmitted)
                    })
                    .OrderByDescending(hs => hs.CreatedAt)
                    .ToListAsync();

                homeworkSets = rawSets.Select(hs => new PartnerAdminHomeworkRow
                {
                    Id = hs.Id,
                    Title = hs.Title,
                    BatchId = hs.BatchId,
                    BatchName = hs.BatchName,
                    SentAt = hs.CreatedAt,
                    TotalStudents = hs.TotalStudents,
                    SubmittedCount = hs.SubmittedCount
                }).ToList();
            }
            else
            {
                homeworkSets = new List<PartnerAdminHomeworkRow>();
            }

            // Exam drafts
            var examDrafts = await _db.ExamDrafts
                .Where(ed => ed.PartnerId == partnerId && !ed.IsArchived)
                .OrderByDescending(ed => ed.CreatedAt)
                .Select(ed => new PartnerAdminExamDraftRow
                {
                    Id = ed.Id,
                    Title = ed.Title,
                    CreatedAt = ed.CreatedAt,
                    QuestionsCount = ed.DraftQuestions.Count()
                })
                .ToListAsync();

            // Sent Exam assignments for partner's batches
            List<PartnerAdminExamRow> examAssignments;
            if (partnerBatchIds.Any())
            {
                var rawExams = await _db.ExamAssignmentsToBatches
                    .Where(ea => partnerBatchIds.Contains(ea.BatchId) && ea.IsSentToStudents && !ea.IsArchived)
                    .Select(ea => new
                    {
                        ea.Id,
                        ea.Title,
                        ea.BatchId,
                        BatchName = ea.Batch.Name,
                        ea.AssignedAt
                    })
                    .OrderByDescending(ea => ea.AssignedAt)
                    .ToListAsync();

                // Load counts in memory to avoid complex nested queries
                var assignmentIds = rawExams.Select(e => e.Id).ToList();
                var attemptedCounts = await _db.ExamStudentStatuses
                    .Where(s => s.ExamAssignmentId.HasValue && assignmentIds.Contains(s.ExamAssignmentId.Value) && s.IsSubmitted)
                    .GroupBy(s => s.ExamAssignmentId!.Value)
                    .Select(g => new { AssignmentId = g.Key, Count = g.Count() })
                    .ToListAsync();
                var attemptedLookup = attemptedCounts.ToDictionary(x => x.AssignmentId, x => x.Count);

                var totalCounts = await _db.StudentBatchEnrollments
                    .Where(e => rawExams.Select(x => x.BatchId).Contains(e.BatchId))
                    .GroupBy(e => e.BatchId)
                    .Select(g => new { BatchId = g.Key, Count = g.Count() })
                    .ToListAsync();
                var totalLookup = totalCounts.ToDictionary(x => x.BatchId, x => x.Count);

                examAssignments = rawExams.Select(ea => new PartnerAdminExamRow
                {
                    Id = ea.Id,
                    Title = ea.Title,
                    BatchId = ea.BatchId,
                    BatchName = ea.BatchName,
                    AssignedAt = ea.AssignedAt,
                    TotalStudents = totalLookup.TryGetValue(ea.BatchId, out var t) ? t : 0,
                    AttemptedCount = attemptedLookup.TryGetValue(ea.Id, out var a) ? a : 0
                }).ToList();
            }
            else
            {
                examAssignments = new List<PartnerAdminExamRow>();
            }

            var vm = new PartnerUsersPageViewModel
            {
                PartnerId = partner.Id,
                PartnerName = partner.Name,
                LogoPath = partner.LogoPath,
                IsActive = partner.IsActive,
                ActiveSubscriptionPeriodId = activePeriodId,
                Students = students,
                Instructors = instructors,
                Admins = admins,
                HomeworkSets = homeworkSets,
                HomeworkDrafts = homeworkDrafts,
                ExamAssignments = examAssignments,
                ExamDrafts = examDrafts,
                Branches = partnerBranchRows,
                Batches = partnerBatches
            };

            return View(vm);
        }

        // ===================== تفاصيل المستخدم =====================
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var vm = new QdratNew.ViewModels.Users.UserDetailsViewModel
            {
                UserId          = user.Id,
                FullName        = user.FullName ?? user.UserName ?? "",
                Email           = user.Email ?? "",
                PhoneNumber     = user.PhoneNumber,
                WhatsAppNumber  = user.WhatsAppNumber,
                IsActive        = user.IsActive,
                ProfileImagePath= user.ProfileImagePath,
                LastLoginAt     = user.LastLoginAt,
                JoinedAt        = null, // ApplicationUser لا يحتوي على CreatedAt افتراضياً
                Roles           = roles.ToList(),
            };

            // ── بيانات الطالب ─────────────────────────────────────
            var student = await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == id);

            if (student != null)
            {
                vm.IsStudent     = true;
                vm.StudentId     = student.StudentID;
                vm.StudentStatus = student.EnrollmentStatus;
                vm.Level         = student.Level;

                // الدفعات
                var batches = await _db.StudentBatchEnrollments
                    .AsNoTracking()
                    .Where(e => e.StudentID == student.StudentID)
                    .Include(e => e.Batch)
                        .ThenInclude(b => b.Course)
                    .OrderByDescending(e => e.EnrolledAt)
                    .Select(e => new QdratNew.ViewModels.Users.UserBatchDto
                    {
                        BatchId    = e.BatchId,
                        CourseId   = e.Batch.CourseId,
                        BatchName  = e.Batch.Name,
                        CourseName = e.Batch.Course.Name,
                        StartDate  = e.Batch.StartDate,
                        EndDate    = e.Batch.EndDate,
                        Status     = e.Status,
                        EnrolledAt = e.EnrolledAt,
                        IsArchived = e.Batch.IsArchived
                    })
                    .ToListAsync();

                vm.Batches = batches;

                // الدفعة الحالية (أحدث دفعة نشطة)
                var currentBatch = batches.FirstOrDefault(b => !b.IsArchived);
                if (currentBatch != null)
                {
                    vm.CurrentBatchName = currentBatch.BatchName;
                    vm.CurrentBatchLecturesCount = await _db.Batches
                        .Where(b => b.Id == currentBatch.BatchId)
                        .SelectMany(b => b.CompletedLessons)
                        .CountAsync();
                }

                // الدورات — مشتقة من الدفعات المسجل بها الطالب
                var watchedTotal = await _db.StudentLessonCompletions
                    .CountAsync(lc => lc.StudentId == student.StudentID);

                vm.Courses = batches
                    .GroupBy(b => b.CourseId)
                    .Select(g =>
                    {
                        var first = g.First();
                        return new QdratNew.ViewModels.Users.UserCourseDto
                        {
                            CourseId     = first.CourseId,
                            CourseName   = first.CourseName,
                            BatchName    = string.Join("، ", g.Select(b => b.BatchName)),
                            IsCompleted  = false,
                            WatchedCount = watchedTotal,
                        };
                    }).ToList();

                vm.EnrolledCoursesCount  = vm.Courses.Count;
                vm.CompletedCoursesCount = vm.Courses.Count(c => c.IsCompleted);

                // الحضور والانصراف
                var attendanceRaw = await _db.AttendanceRecords
                    .AsNoTracking()
                    .Where(a => a.StudentId == student.StudentID)
                    .Include(a => a.Lecture)
                    .OrderByDescending(a => a.RecordedAt)
                    .Take(50)
                    .ToListAsync();

                vm.Attendance = attendanceRaw.Select(a => new QdratNew.ViewModels.Users.UserAttendanceDto
                {
                    Id          = a.Id,
                    Date        = a.RecordedAt,
                    LectureName = a.Lecture?.Title ?? "—",
                    CourseName  = "",
                    CheckIn     = a.ActualArrivalTime.HasValue
                                    ? a.RecordedAt.Date + a.ActualArrivalTime.Value
                                    : (DateTime?)null,
                    CheckOut    = a.ActualDepartureTime.HasValue
                                    ? a.RecordedAt.Date + a.ActualDepartureTime.Value
                                    : (DateTime?)null,
                    Status      = !a.IsPresent ? "غائب"
                                : a.IsLateArrival ? "متأخر" : "حاضر",
                    Notes       = a.Notes,
                }).ToList();

                vm.AttendedCount = vm.Attendance.Count(a => a.Status == "حاضر");
                vm.AbsentCount   = vm.Attendance.Count(a => a.Status == "غائب");
                vm.LateCount     = vm.Attendance.Count(a => a.Status == "متأخر");

                // آخر نشاط (activity logs) — يُعبّأ أسفل مع بيانات المستخدم العام

                // الواجبات
                vm.Assignments = await _db.Homeworks
                    .AsNoTracking()
                    .Where(h => h.StudentId == student.StudentID)
                    .Include(h => h.HomeworkSet)
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(30)
                    .Select(h => new QdratNew.ViewModels.Users.UserAssignmentDto
                    {
                        HomeworkSetId = h.HomeworkSetId,
                        Title         = h.HomeworkSet.Title,
                        DueDate       = h.HomeworkSet.EndAt,
                        SubmittedAt   = h.SubmittedAt,
                        Score         = h.Score,
                        Status        = h.IsCompleted ? "مسلّم"
                                        : (h.HomeworkSet.EndAt.HasValue && h.HomeworkSet.EndAt < DateTime.Now
                                            ? "متأخر" : "قيد التسليم"),
                        Correct       = h.IsCorrect == true ? 1 : 0,
                    })
                    .ToListAsync();

                vm.SubmittedAssignments = vm.Assignments.Count(a => a.Status == "مسلّم");

                // التقدير العام من الواجبات
                // التقدير العام = نسبة الإجابات الصحيحة من مجموع الواجبات المحلولة
                var hwAnswered = await _db.Homeworks
                    .Where(h => h.StudentId == student.StudentID
                             && h.IsCorrect.HasValue)
                    .Select(h => h.IsCorrect)
                    .ToListAsync();

                vm.OverallGradePercent = hwAnswered.Any()
                    ? Math.Round(hwAnswered.Count(x => x == true) * 100.0 / hwAnswered.Count, 1)
                    : 0;

                // الاختبارات — من اختبارات مؤشرات الأداء (PerformanceIndicatorExam)
                // لأن صفحة التقرير تتوقع PerformanceIndicatorExamId
                var examRaw = await _db.PerformanceIndicatorExamStudents
                    .AsNoTracking()
                    .Where(e => e.StudentId == student.StudentID && e.IsCompleted)
                    .Include(e => e.PerformanceIndicatorExam)
                    .OrderByDescending(e => e.CompletedAt)
                    .Take(30)
                    .ToListAsync();

                vm.Exams = examRaw.Select(e => new QdratNew.ViewModels.Users.UserExamDto
                {
                    ExamId       = e.PerformanceIndicatorExamId,
                    ExamTitle    = e.PerformanceIndicatorExam?.Title ?? "اختبار",
                    TakenAt      = e.CompletedAt,
                    ScorePercent = Math.Round(e.ScorePercent ?? 0, 1),
                    Total        = e.PerformanceIndicatorExam?.TotalQuestions ?? 0,
                    Correct      = (int)Math.Round((e.ScorePercent ?? 0) * (e.PerformanceIndicatorExam?.TotalQuestions ?? 0) / 100.0),
                }).ToList();

                vm.AverageExamScore = vm.Exams.Any()
                    ? Math.Round(vm.Exams.Average(e => e.ScorePercent), 1)
                    : 0;
            }

            // ── سجل الدخول والخروج (لجميع المستخدمين) ───────────────
            vm.LoginLogs = await _db.UserLoginLogs
                .AsNoTracking()
                .Where(l => l.UserId == id)
                .OrderByDescending(l => l.LoginAt)
                .Take(50)
                .Select(l => new QdratNew.ViewModels.Users.UserLoginLogDto
                {
                    LoginAt    = l.LoginAt,
                    LogoutAt   = l.LogoutAt,
                    IpAddress  = l.IpAddress,
                    DeviceInfo = l.DeviceInfo,
                })
                .ToListAsync();

            vm.TotalLoginCount = await _db.UserLoginLogs
                .CountAsync(l => l.UserId == id);

            // ── صلاحية "الدخول كمستخدم" ──────────────────────────────────
            // HasImpersonationAccess  → هل المستخدم المعروض (id) لديه صلاحية الإمبرسونيشن (للـ grant/revoke UI)
            // CurrentUserCanImpersonate → هل المستخدم الداخل الآن يملك صلاحية تشغيل الزر
            var viewedUserAccess = await _db.UserImpersonationAccesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == id);

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            var currentUserAccess = await _db.UserImpersonationAccesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == currentUserId);

            ViewBag.HasImpersonationAccess   = viewedUserAccess?.IsActive == true;
            ViewBag.IsImpersonationManager   = User.IsInRole("Owner") || User.IsInRole("Developer");
            ViewBag.CurrentUserCanImpersonate = User.IsInRole("Owner") || User.IsInRole("Developer")
                                               || currentUserAccess?.IsActive == true;

            return View(vm);
        }

        // ===================== رفع صورة الملف الشخصي =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfileImage(string userId, IFormFile photo)
        {
            if (string.IsNullOrEmpty(userId) || photo == null || photo.Length == 0)
            {
                TempData["ErrorMessage"] = "لم يتم اختيار صورة";
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext))
            {
                TempData["ErrorMessage"] = "امتداد الملف غير مدعوم، استخدم JPG أو PNG";
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            // حذف الصورة القديمة
            if (!string.IsNullOrEmpty(user.ProfileImagePath))
            {
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    user.ProfileImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
            Directory.CreateDirectory(folder);
            var fileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await photo.CopyToAsync(stream);

            user.ProfileImagePath = $"/uploads/profiles/{fileName}";
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "✅ تم تحديث صورة الملف الشخصي بنجاح";
            return RedirectToAction(nameof(Details), new { id = userId });
        }

        // ===================== تقليد حساب المستخدم (Impersonate) =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImpersonateUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var target = await _userManager.FindByIdAsync(userId);
            if (target == null)
                return NotFound();

            // الأدمن يبقى signed-in — لا sign-out أبداً
            // نحفظ بيانات التخفي في cookie مشفّر (محصّن ضد Session.Clear)
            var adminId     = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            var targetRoles = await _userManager.GetRolesAsync(target);

            // مسح أي جلسة تخفي قديمة قبل بدء جلسة جديدة
            _impersonation.Clear();

            _impersonation.Start(new QdratNew.Services.ImpersonationData
            {
                AdminId      = adminId,
                TargetUserId = target.Id,
                TargetName   = target.FullName ?? target.UserName ?? "",
                TargetEmail  = target.Email ?? "",
                TargetRoles  = targetRoles.ToList(),
                StartUtc     = DateTime.UtcNow,
            });

            // توجيه مباشر للمنطقة الصحيحة حسب دور المستخدم
            // StudentProfile مدرجة في القائمة البيضاء في StudentBaseController (لا تحتاج سياق دورة)
            if (targetRoles.Contains("Student"))
                return RedirectToAction("Index", "StudentProfile", new { area = "Students" });
            if (targetRoles.Contains("Employee"))
                return RedirectToAction("Index", "EmployeeDashboard", new { area = "Admin" });

            if (targetRoles.Any(r => r == "Instructor" || r == "Teacher" || r == "PartnerInstructor"))
                return RedirectToAction("Index", "InstructorDashboard", new { area = "Instructors" });

            // أدوار الأدمن بجميع مستوياتها → لوحة الأدمن
            if (targetRoles.Any(r => r is "SuperAdmin" or "Owner" or "Developer" or "Admin"
                                      or "DataEntry"))
                return RedirectToAction("Index", "AdminOperationsDashboard", new { area = "Admin" });

            // احتياطي إذا لم يكن للمستخدم دور محدد
            return RedirectToAction("Index", "StudentProfile", new { area = "Students" });
        }

        // ===================== صفحة الصلاحيات المتخصصة (Owner/Developer only) =====================
        [HttpGet]
        public async Task<IActionResult> SpecialPermissions(string userId)
        {
            if (!User.IsInRole("Owner") && !User.IsInRole("Developer"))
                return Forbid();

            if (string.IsNullOrWhiteSpace(userId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var impersonationAccess = await _db.UserImpersonationAccesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            var vm = new QdratNew.ViewModels.Users.UserSpecialPermissionsVM
            {
                UserId       = user.Id,
                UserName     = user.FullName ?? user.UserName ?? user.Email ?? user.Id,
                UserEmail    = user.Email ?? string.Empty,
                UserRole     = string.Join("، ", roles),
                CanImpersonate = impersonationAccess?.IsActive == true
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SpecialPermissions(QdratNew.ViewModels.Users.UserSpecialPermissionsVM model)
        {
            if (!User.IsInRole("Owner") && !User.IsInRole("Developer"))
                return Forbid();

            if (string.IsNullOrWhiteSpace(model.UserId))
                return NotFound();

            var granterId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

            // ── إدارة صلاحية الدخول كمستخدم ──────────────────────────
            var impersonationAccess = await _db.UserImpersonationAccesses
                .FirstOrDefaultAsync(x => x.UserId == model.UserId);

            if (model.CanImpersonate)
            {
                if (impersonationAccess == null)
                {
                    _db.UserImpersonationAccesses.Add(new QdratNew.Entities.UserImpersonationAccess
                    {
                        UserId          = model.UserId,
                        GrantedByUserId = granterId,
                        GrantedAt       = DateTime.UtcNow,
                        IsActive        = true
                    });
                }
                else
                {
                    impersonationAccess.IsActive        = true;
                    impersonationAccess.GrantedByUserId = granterId;
                    impersonationAccess.GrantedAt       = DateTime.UtcNow;
                }
            }
            else if (impersonationAccess != null)
            {
                impersonationAccess.IsActive = false;
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم حفظ الصلاحيات المتخصصة.";
            return RedirectToAction(nameof(SpecialPermissions), new { userId = model.UserId });
        }

        // ===================== إدارة صلاحية "الدخول كمستخدم" (Owner/Developer only) =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantImpersonationAccess(string targetUserId)
        {
            if (!User.IsInRole("Owner") && !User.IsInRole("Developer"))
                return Forbid();

            if (string.IsNullOrWhiteSpace(targetUserId))
                return BadRequest();

            var granterId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

            var existing = await _db.UserImpersonationAccesses
                .FirstOrDefaultAsync(x => x.UserId == targetUserId);

            if (existing == null)
            {
                _db.UserImpersonationAccesses.Add(new QdratNew.Entities.UserImpersonationAccess
                {
                    UserId          = targetUserId,
                    GrantedByUserId = granterId,
                    GrantedAt       = DateTime.UtcNow,
                    IsActive        = true
                });
            }
            else
            {
                existing.IsActive        = true;
                existing.GrantedByUserId = granterId;
                existing.GrantedAt       = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ تم منح صلاحية الدخول كمستخدم.";
            return RedirectToAction(nameof(Details), new { id = targetUserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeImpersonationAccess(string targetUserId)
        {
            if (!User.IsInRole("Owner") && !User.IsInRole("Developer"))
                return Forbid();

            if (string.IsNullOrWhiteSpace(targetUserId))
                return BadRequest();

            var access = await _db.UserImpersonationAccesses
                .FirstOrDefaultAsync(x => x.UserId == targetUserId);

            if (access != null)
            {
                access.IsActive = false;
                await _db.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "✅ تم سحب صلاحية الدخول كمستخدم.";
            return RedirectToAction(nameof(Details), new { id = targetUserId });
        }
    }
}
