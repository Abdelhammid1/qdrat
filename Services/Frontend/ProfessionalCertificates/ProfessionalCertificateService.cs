using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.Admin.ProfessionalCertificates;
using QdratNew.ViewModels.Frontend.ProfessionalCertificates;

namespace QdratNew.Services.Frontend.ProfessionalCertificates;

public class ProfessionalCertificateService : IProfessionalCertificateService
{
    private readonly ApplicationDbContext _context;

    public ProfessionalCertificateService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ─── Frontend ───────────────────────────────────────────────

    public async Task<ProfessionalCertificateHomeSectionViewModel> GetHomeSectionAsync()
    {
        var setting = await _context.ProfessionalCertificateSectionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1);

        var vm = new ProfessionalCertificateHomeSectionViewModel
        {
            IsEnabled = setting?.IsEnabled ?? false,
            Title = setting?.Title ?? string.Empty,
            Subtitle = setting?.Subtitle,
            Description = setting?.Description,
            ButtonText = setting?.ButtonText ?? "سجل اهتمامك"
        };

        if (!vm.IsEnabled) return vm;

        var courses = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .Where(x => x.IsActive && x.ShowOnHomePage)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        if (!courses.Any()) { vm.IsEnabled = false; return vm; }

        var courseIds = new List<int>();
        foreach (var c in courses) courseIds.Add(c.Id);

        // جلب عدد الطلبات الفعالة لكل دورة دفعة واحدة
        var activeCounts = await _context.ProfessionalCertificateRegistrations
            .AsNoTracking()
            .Where(x =>
                (x.Status == ProfessionalCertificateRegistrationStatus.New ||
                 x.Status == ProfessionalCertificateRegistrationStatus.Contacted ||
                 x.Status == ProfessionalCertificateRegistrationStatus.NeedFollowUp ||
                 x.Status == ProfessionalCertificateRegistrationStatus.Converted) &&
                (x.ProfessionalCertificateCourseId == courseIds[0] ||
                 (courseIds.Count > 1 && x.ProfessionalCertificateCourseId == courseIds[1]) ||
                 (courseIds.Count > 2 && x.ProfessionalCertificateCourseId == courseIds[2]) ||
                 (courseIds.Count > 3 && x.ProfessionalCertificateCourseId == courseIds[3]) ||
                 (courseIds.Count > 4 && x.ProfessionalCertificateCourseId == courseIds[4]) ||
                 (courseIds.Count > 5 && x.ProfessionalCertificateCourseId == courseIds[5]) ||
                 (courseIds.Count > 6 && x.ProfessionalCertificateCourseId == courseIds[6]) ||
                 (courseIds.Count > 7 && x.ProfessionalCertificateCourseId == courseIds[7]) ||
                 (courseIds.Count > 8 && x.ProfessionalCertificateCourseId == courseIds[8]) ||
                 (courseIds.Count > 9 && x.ProfessionalCertificateCourseId == courseIds[9])))
            .GroupBy(x => x.ProfessionalCertificateCourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countDict = activeCounts.ToDictionary(x => x.CourseId, x => x.Count);

        foreach (var course in courses)
        {
            var activeCount = countDict.TryGetValue(course.Id, out var cnt) ? cnt : 0;
            var isMaxReached = course.MaxRequests.HasValue && activeCount >= course.MaxRequests.Value;

            vm.Courses.Add(new ProfessionalCertificateCourseCardViewModel
            {
                Id = course.Id,
                TitleAr = course.TitleAr,
                StandardCode = course.StandardCode,
                Slug = course.Slug,
                ShortDescription = course.ShortDescription,
                IconClass = course.IconClass,
                BadgeText = course.BadgeText,
                IsRegistrationOpen = course.IsRegistrationOpen,
                IsMaxReached = isMaxReached,
                ActiveRegistrationCount = activeCount,
                MaxRequests = course.MaxRequests,
                LogoPath = course.LogoPath
            });
        }

        return vm;
    }

    public async Task<ProfessionalCertificateRegisterViewModel?> GetRegisterViewModelAsync(string slug)
    {
        var course = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive);

        if (course == null) return null;

        return new ProfessionalCertificateRegisterViewModel
        {
            CourseId = course.Id,
            CourseTitleAr = course.TitleAr,
            StandardCode = course.StandardCode,
            CourseShortDescription = course.ShortDescription,
            CourseFullDescription = course.FullDescription,
            LogoPath = course.LogoPath
        };
    }

    public async Task<(bool Success, int? RegistrationId, string? ErrorMessage)> CreateRegistrationAsync(
        ProfessionalCertificateRegisterViewModel model, string? ip, string? userAgent)
    {
        var setting = await _context.ProfessionalCertificateSectionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1);

        if (setting == null || !setting.IsEnabled)
            return (false, null, "خدمة التسجيل غير متاحة حالياً.");

        var course = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == model.CourseId && x.IsActive);

        if (course == null)
            return (false, null, "الدورة غير موجودة أو غير متاحة.");

        if (!course.IsRegistrationOpen)
            return (false, null, "التسجيل في هذه الدورة مغلق حالياً.");

        // فحص MaxRequests للدورة
        if (course.MaxRequests.HasValue)
        {
            var activeCount = await _context.ProfessionalCertificateRegistrations
                .CountAsync(x =>
                    x.ProfessionalCertificateCourseId == course.Id &&
                    x.Status != ProfessionalCertificateRegistrationStatus.Rejected);

            if (activeCount >= course.MaxRequests.Value)
                return (false, null, "اكتمل العدد المتاح لهذه الدورة.");
        }

        // فحص GlobalMaxRequests
        if (setting.AutoCloseWhenMaxReached && setting.GlobalMaxRequests.HasValue)
        {
            var globalCount = await _context.ProfessionalCertificateRegistrations
                .CountAsync(x => x.Status != ProfessionalCertificateRegistrationStatus.Rejected);

            if (globalCount >= setting.GlobalMaxRequests.Value)
                return (false, null, "اكتمل العدد الإجمالي المتاح للتسجيل.");
        }

        // منع التكرار
        var duplicate = await _context.ProfessionalCertificateRegistrations
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProfessionalCertificateCourseId == model.CourseId &&
                x.NationalId == model.NationalId &&
                (x.Status == ProfessionalCertificateRegistrationStatus.New ||
                 x.Status == ProfessionalCertificateRegistrationStatus.Contacted ||
                 x.Status == ProfessionalCertificateRegistrationStatus.NeedFollowUp ||
                 x.Status == ProfessionalCertificateRegistrationStatus.Converted));

        if (duplicate)
            return (false, null, "لديك طلب تسجيل سابق لهذه الدورة قيد المعالجة.");

        var registration = new ProfessionalCertificateRegistration
        {
            ProfessionalCertificateCourseId = model.CourseId,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            NationalId = model.NationalId,
            Email = model.Email,
            City = model.City,
            Notes = model.Notes,
            Status = ProfessionalCertificateRegistrationStatus.New,
            SubmittedAt = DateTime.Now,
            IpAddress = ip,
            UserAgent = userAgent
        };

        _context.ProfessionalCertificateRegistrations.Add(registration);
        await _context.SaveChangesAsync();

        return (true, registration.Id, null);
    }

    public async Task<ProfessionalCertificateThanksViewModel?> GetThanksAsync(int registrationId)
    {
        var reg = await _context.ProfessionalCertificateRegistrations
            .AsNoTracking()
            .Include(x => x.ProfessionalCertificateCourse)
            .FirstOrDefaultAsync(x => x.Id == registrationId);

        if (reg == null) return null;

        return new ProfessionalCertificateThanksViewModel
        {
            RegistrationId = reg.Id,
            CourseTitleAr = reg.ProfessionalCertificateCourse.TitleAr,
            StandardCode = reg.ProfessionalCertificateCourse.StandardCode,
            SubmittedAt = reg.SubmittedAt
        };
    }

    // ─── Admin - Registrations ───────────────────────────────────

    public async Task<ProfessionalCertificateDashboardViewModel> GetAdminDashboardAsync(
        ProfessionalCertificateRegistrationFilterViewModel filter)
    {
        var query = _context.ProfessionalCertificateRegistrations
            .AsNoTracking()
            .Include(x => x.ProfessionalCertificateCourse);

        // إحصائيات الحالات
        var allForStats = await query.Select(x => x.Status).ToListAsync();
        var totalCount = allForStats.Count;
        var newCount = allForStats.Count(s => s == ProfessionalCertificateRegistrationStatus.New);
        var contactedCount = allForStats.Count(s => s == ProfessionalCertificateRegistrationStatus.Contacted);
        var needFollowUpCount = allForStats.Count(s => s == ProfessionalCertificateRegistrationStatus.NeedFollowUp);
        var rejectedCount = allForStats.Count(s => s == ProfessionalCertificateRegistrationStatus.Rejected);
        var convertedCount = allForStats.Count(s => s == ProfessionalCertificateRegistrationStatus.Converted);

        // الفلترة
        var filteredQuery = _context.ProfessionalCertificateRegistrations
            .AsNoTracking()
            .Include(x => x.ProfessionalCertificateCourse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            filteredQuery = filteredQuery.Where(x =>
                x.FullName.Contains(search) ||
                x.PhoneNumber.Contains(search) ||
                x.NationalId.Contains(search));
        }

        if (filter.CourseId.HasValue)
            filteredQuery = filteredQuery.Where(x => x.ProfessionalCertificateCourseId == filter.CourseId.Value);

        if (filter.Status.HasValue)
            filteredQuery = filteredQuery.Where(x => x.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            filteredQuery = filteredQuery.Where(x => x.SubmittedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
        {
            var toDateEnd = filter.ToDate.Value.AddDays(1);
            filteredQuery = filteredQuery.Where(x => x.SubmittedAt < toDateEnd);
        }

        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;
        var page = filter.Page > 0 ? filter.Page : 1;

        // Fetch all filtered rows to memory, then group by BatchId
        var rawItems = await filteredQuery
            .OrderByDescending(x => x.SubmittedAt)
            .Select(x => new
            {
                x.Id,
                x.RegistrationBatchId,
                x.FullName,
                x.PhoneNumber,
                x.NationalId,
                CourseTitleAr = x.ProfessionalCertificateCourse.TitleAr,
                StandardCode  = x.ProfessionalCertificateCourse.StandardCode,
                x.Status,
                x.SubmittedAt,
                x.LastUpdatedAt,
                x.HandledByName
            })
            .ToListAsync();

        // Group: multi-course registrations share the same BatchId
        var grouped = rawItems
            .GroupBy(x => x.RegistrationBatchId ?? ("solo_" + x.Id))
            .Select(g =>
            {
                var rep = g.OrderBy(x => x.Id).First();
                return new ProfessionalCertificateRegistrationListItemViewModel
                {
                    Id                  = rep.Id,
                    RegistrationBatchId = rep.RegistrationBatchId,
                    FullName            = rep.FullName,
                    PhoneNumber         = rep.PhoneNumber,
                    NationalId          = rep.NationalId,
                    Courses             = g.Select(x => new PcrCourseBadge
                    {
                        RegistrationId = x.Id,
                        TitleAr        = x.CourseTitleAr,
                        StandardCode   = x.StandardCode
                    }).ToList(),
                    Status          = rep.Status,
                    SubmittedAt     = rep.SubmittedAt,
                    LastUpdatedAt   = g.Max(x => x.LastUpdatedAt),
                    HandledByName   = rep.HandledByName
                };
            })
            .OrderByDescending(x => x.SubmittedAt)
            .ToList();

        var filteredCount = grouped.Count;
        var totalPages = (int)Math.Ceiling((double)filteredCount / pageSize);
        var items = grouped.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var courses = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new ProfessionalCertificateCourseListItemViewModel
            {
                Id = x.Id,
                TitleAr = x.TitleAr,
                StandardCode = x.StandardCode
            })
            .ToListAsync();

        return new ProfessionalCertificateDashboardViewModel
        {
            TotalCount = totalCount,
            NewCount = newCount,
            ContactedCount = contactedCount,
            NeedFollowUpCount = needFollowUpCount,
            RejectedCount = rejectedCount,
            ConvertedCount = convertedCount,
            Items = items,
            Filter = filter,
            CoursesForFilter = courses,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<ProfessionalCertificateRegistrationDetailsViewModel?> GetRegistrationDetailsAsync(int id)
    {
        var reg = await _context.ProfessionalCertificateRegistrations
            .AsNoTracking()
            .Include(x => x.ProfessionalCertificateCourse)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (reg == null) return null;

        // Load all courses in this submission (batch)
        List<PcrCourseBadge> allCourses;
        if (!string.IsNullOrEmpty(reg.RegistrationBatchId))
        {
            var siblings = await _context.ProfessionalCertificateRegistrations
                .AsNoTracking()
                .Include(x => x.ProfessionalCertificateCourse)
                .Where(x => x.RegistrationBatchId == reg.RegistrationBatchId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            allCourses = siblings.Select(x => new PcrCourseBadge
            {
                RegistrationId = x.Id,
                TitleAr        = x.ProfessionalCertificateCourse.TitleAr,
                StandardCode   = x.ProfessionalCertificateCourse.StandardCode
            }).ToList();
        }
        else
        {
            allCourses = new List<PcrCourseBadge>
            {
                new PcrCourseBadge
                {
                    RegistrationId = reg.Id,
                    TitleAr        = reg.ProfessionalCertificateCourse.TitleAr,
                    StandardCode   = reg.ProfessionalCertificateCourse.StandardCode
                }
            };
        }

        return new ProfessionalCertificateRegistrationDetailsViewModel
        {
            Id                  = reg.Id,
            RegistrationBatchId = reg.RegistrationBatchId,
            FullName            = reg.FullName,
            PhoneNumber         = reg.PhoneNumber,
            NationalId          = reg.NationalId,
            Email               = reg.Email,
            City                = reg.City,
            Notes               = reg.Notes,
            Status              = reg.Status,
            AdminNotes          = reg.AdminNotes,
            RejectionReason     = reg.RejectionReason,
            IsContacted         = reg.IsContacted,
            ContactedAt         = reg.ContactedAt,
            FollowUpAt          = reg.FollowUpAt,
            HandledByName       = reg.HandledByName,
            SubmittedAt         = reg.SubmittedAt,
            LastUpdatedAt       = reg.LastUpdatedAt,
            IpAddress           = reg.IpAddress,
            UserAgent           = reg.UserAgent,
            AllCourses          = allCourses,
            CourseId            = reg.ProfessionalCertificateCourseId,
            CourseTitleAr       = reg.ProfessionalCertificateCourse.TitleAr,
            StandardCode        = reg.ProfessionalCertificateCourse.StandardCode
        };
    }

    public async Task<bool> UpdateRegistrationStatusAsync(
        ProfessionalCertificateRegistrationUpdateStatusViewModel model, string userId, string userName)
    {
        var reg = await _context.ProfessionalCertificateRegistrations
            .FirstOrDefaultAsync(x => x.Id == model.RegistrationId);

        if (reg == null) return false;

        // Collect all records in the same batch (or just this one)
        var toUpdate = new List<ProfessionalCertificateRegistration> { reg };
        if (!string.IsNullOrEmpty(reg.RegistrationBatchId))
        {
            var bId = reg.RegistrationBatchId;
            var siblings = await _context.ProfessionalCertificateRegistrations
                .Where(x => x.RegistrationBatchId == bId && x.Id != reg.Id)
                .ToListAsync();
            toUpdate.AddRange(siblings);
        }

        var now = DateTime.Now;
        foreach (var r in toUpdate)
        {
            r.Status            = model.NewStatus;
            r.AdminNotes        = model.AdminNotes;
            r.LastUpdatedAt     = now;
            r.HandledByUserId   = userId;
            r.HandledByName     = userName;

            if (!string.IsNullOrEmpty(model.RejectionReason))
                r.RejectionReason = model.RejectionReason;

            if (model.NewStatus == ProfessionalCertificateRegistrationStatus.Contacted)
            {
                r.IsContacted  = true;
                r.ContactedAt  = now;
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Admin - Courses ─────────────────────────────────────────

    public async Task<IReadOnlyList<ProfessionalCertificateCourseListItemViewModel>> GetAdminCoursesAsync()
    {
        var courses = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var courseIds = new List<int>();
        foreach (var c in courses) courseIds.Add(c.Id);

        var counts = new Dictionary<int, int>();
        if (courseIds.Any())
        {
            var regCounts = await _context.ProfessionalCertificateRegistrations
                .AsNoTracking()
                .Where(x =>
                    x.ProfessionalCertificateCourseId == courseIds[0] ||
                    (courseIds.Count > 1 && x.ProfessionalCertificateCourseId == courseIds[1]) ||
                    (courseIds.Count > 2 && x.ProfessionalCertificateCourseId == courseIds[2]) ||
                    (courseIds.Count > 3 && x.ProfessionalCertificateCourseId == courseIds[3]) ||
                    (courseIds.Count > 4 && x.ProfessionalCertificateCourseId == courseIds[4]) ||
                    (courseIds.Count > 5 && x.ProfessionalCertificateCourseId == courseIds[5]) ||
                    (courseIds.Count > 6 && x.ProfessionalCertificateCourseId == courseIds[6]) ||
                    (courseIds.Count > 7 && x.ProfessionalCertificateCourseId == courseIds[7]) ||
                    (courseIds.Count > 8 && x.ProfessionalCertificateCourseId == courseIds[8]) ||
                    (courseIds.Count > 9 && x.ProfessionalCertificateCourseId == courseIds[9]))
                .GroupBy(x => x.ProfessionalCertificateCourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var rc in regCounts)
                counts[rc.CourseId] = rc.Count;
        }

        var result = new List<ProfessionalCertificateCourseListItemViewModel>();
        foreach (var c in courses)
        {
            result.Add(new ProfessionalCertificateCourseListItemViewModel
            {
                Id = c.Id,
                TitleAr = c.TitleAr,
                StandardCode = c.StandardCode,
                Slug = c.Slug,
                IsActive = c.IsActive,
                ShowOnHomePage = c.ShowOnHomePage,
                IsRegistrationOpen = c.IsRegistrationOpen,
                RegistrationCount = counts.TryGetValue(c.Id, out var cnt) ? cnt : 0,
                DisplayOrder = c.DisplayOrder,
                LogoPath = c.LogoPath
            });
        }

        return result;
    }

    public async Task<ProfessionalCertificateCourseFormViewModel?> GetCourseFormAsync(int id)
    {
        var course = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (course == null) return null;

        return new ProfessionalCertificateCourseFormViewModel
        {
            Id = course.Id,
            TitleAr = course.TitleAr,
            StandardCode = course.StandardCode,
            Slug = course.Slug,
            ShortDescription = course.ShortDescription,
            FullDescription = course.FullDescription,
            IconClass = course.IconClass,
            BadgeText = course.BadgeText,
            IsActive = course.IsActive,
            ShowOnHomePage = course.ShowOnHomePage,
            IsRegistrationOpen = course.IsRegistrationOpen,
            MaxRequests = course.MaxRequests,
            DisplayOrder = course.DisplayOrder,
            ExistingLogoPath = course.LogoPath
        };
    }

    public async Task<int> CreateCourseAsync(ProfessionalCertificateCourseFormViewModel model, string webRootPath)
    {
        string? logoPath = null;
        if (model.LogoFile != null && model.LogoFile.Length > 0)
            logoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, "prof-certificates", 600);

        var course = new ProfessionalCertificateCourse
        {
            TitleAr = model.TitleAr,
            StandardCode = model.StandardCode,
            Slug = model.Slug.ToLower().Trim(),
            ShortDescription = model.ShortDescription,
            FullDescription = model.FullDescription,
            IconClass = model.IconClass,
            BadgeText = model.BadgeText,
            LogoPath = logoPath,
            IsActive = model.IsActive,
            ShowOnHomePage = model.ShowOnHomePage,
            IsRegistrationOpen = model.IsRegistrationOpen,
            MaxRequests = model.MaxRequests,
            DisplayOrder = model.DisplayOrder,
            CreatedAt = DateTime.Now
        };

        _context.ProfessionalCertificateCourses.Add(course);
        await _context.SaveChangesAsync();
        return course.Id;
    }

    public async Task<bool> UpdateCourseAsync(ProfessionalCertificateCourseFormViewModel model, string webRootPath)
    {
        var course = await _context.ProfessionalCertificateCourses.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (course == null) return false;

        // معالجة اللوجو
        if (model.RemoveLogo && !string.IsNullOrEmpty(course.LogoPath))
        {
            DeleteLogoFile(course.LogoPath, webRootPath);
            course.LogoPath = null;
        }
        else if (model.LogoFile != null && model.LogoFile.Length > 0)
        {
            // حذف القديم أولاً
            if (!string.IsNullOrEmpty(course.LogoPath))
                DeleteLogoFile(course.LogoPath, webRootPath);

            course.LogoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, "prof-certificates", 600);
        }

        course.TitleAr = model.TitleAr;
        course.StandardCode = model.StandardCode;
        course.Slug = model.Slug.ToLower().Trim();
        course.ShortDescription = model.ShortDescription;
        course.FullDescription = model.FullDescription;
        course.IconClass = model.IconClass;
        course.BadgeText = model.BadgeText;
        course.IsActive = model.IsActive;
        course.ShowOnHomePage = model.ShowOnHomePage;
        course.IsRegistrationOpen = model.IsRegistrationOpen;
        course.MaxRequests = model.MaxRequests;
        course.DisplayOrder = model.DisplayOrder;
        course.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    private static void DeleteLogoFile(string logoPath, string webRootPath)
    {
        try
        {
            var fullPath = Path.Combine(webRootPath, logoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch { /* تجاهل أخطاء الحذف */ }
    }

    public async Task<bool> ToggleCourseActiveAsync(int id)
    {
        var course = await _context.ProfessionalCertificateCourses.FirstOrDefaultAsync(x => x.Id == id);
        if (course == null) return false;
        course.IsActive = !course.IsActive;
        course.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCourseHomeVisibilityAsync(int id)
    {
        var course = await _context.ProfessionalCertificateCourses.FirstOrDefaultAsync(x => x.Id == id);
        if (course == null) return false;
        course.ShowOnHomePage = !course.ShowOnHomePage;
        course.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleRegistrationOpenAsync(int id)
    {
        var course = await _context.ProfessionalCertificateCourses.FirstOrDefaultAsync(x => x.Id == id);
        if (course == null) return false;
        course.IsRegistrationOpen = !course.IsRegistrationOpen;
        course.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Admin - Settings ────────────────────────────────────────

    public async Task<ProfessionalCertificateSectionSettingViewModel> GetSectionSettingAsync()
    {
        var setting = await _context.ProfessionalCertificateSectionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1);

        if (setting == null)
            return new ProfessionalCertificateSectionSettingViewModel { Id = 1 };

        return new ProfessionalCertificateSectionSettingViewModel
        {
            Id = setting.Id,
            IsEnabled = setting.IsEnabled,
            Title = setting.Title,
            Subtitle = setting.Subtitle,
            Description = setting.Description,
            ButtonText = setting.ButtonText,
            AutoCloseWhenMaxReached = setting.AutoCloseWhenMaxReached,
            GlobalMaxRequests = setting.GlobalMaxRequests,
            ClosedMessage = setting.ClosedMessage
        };
    }

    public async Task<bool> UpdateSectionSettingAsync(ProfessionalCertificateSectionSettingViewModel model, string userId)
    {
        var setting = await _context.ProfessionalCertificateSectionSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (setting == null) return false;

        setting.IsEnabled = model.IsEnabled;
        setting.Title = model.Title;
        setting.Subtitle = model.Subtitle;
        setting.Description = model.Description;
        setting.ButtonText = model.ButtonText;
        setting.AutoCloseWhenMaxReached = model.AutoCloseWhenMaxReached;
        setting.GlobalMaxRequests = model.GlobalMaxRequests;
        setting.ClosedMessage = model.ClosedMessage;
        setting.UpdatedAt = DateTime.Now;
        setting.UpdatedByUserId = userId;

        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Multi-course Registration ───────────────────────────────

    public async Task<ProfessionalCertificateMultiRegisterViewModel> GetMultiRegisterViewModelAsync()
    {
        var courses = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var items = new List<ProfessionalCertificateCourseSelectItem>();

        foreach (var c in courses)
        {
            int activeCount = 0;
            if (c.MaxRequests.HasValue)
            {
                activeCount = await _context.ProfessionalCertificateRegistrations
                    .CountAsync(x =>
                        x.ProfessionalCertificateCourseId == c.Id &&
                        x.Status != ProfessionalCertificateRegistrationStatus.Rejected);
            }

            items.Add(new ProfessionalCertificateCourseSelectItem
            {
                Id = c.Id,
                TitleAr = c.TitleAr,
                StandardCode = c.StandardCode,
                LogoPath = c.LogoPath,
                IconClass = c.IconClass,
                BadgeText = c.BadgeText,
                ShortDescription = c.ShortDescription,
                IsRegistrationOpen = c.IsRegistrationOpen,
                IsMaxReached = c.MaxRequests.HasValue && activeCount >= c.MaxRequests.Value
            });
        }

        return new ProfessionalCertificateMultiRegisterViewModel { Courses = items };
    }

    public async Task<(bool Success, string? ErrorMessage, List<string> RegisteredCourses)> CreateMultiRegistrationAsync(
        ProfessionalCertificateMultiRegisterViewModel model, string? ip, string? userAgent)
    {
        if (model.SelectedCourseIds == null || !model.SelectedCourseIds.Any())
            return (false, "يجب اختيار دورة واحدة على الأقل.", new List<string>());

        var setting = await _context.ProfessionalCertificateSectionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1);

        if (setting == null || !setting.IsEnabled)
            return (false, "خدمة التسجيل غير متاحة حالياً.", new List<string>());

        // Load all active open courses in memory to avoid Contains() in SQL
        var allCourses = await _context.ProfessionalCertificateCourses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();

        var selectedIds = model.SelectedCourseIds.Distinct().ToList();
        var selectedCourses = allCourses.Where(c => selectedIds.Contains(c.Id)).ToList();

        if (!selectedCourses.Any())
            return (false, "لم يتم العثور على الدورات المختارة.", new List<string>());

        var batchId = Guid.NewGuid().ToString();
        var toAdd = new List<ProfessionalCertificateRegistration>();
        var registeredNames = new List<string>();

        foreach (var course in selectedCourses)
        {
            if (!course.IsRegistrationOpen) continue;

            if (course.MaxRequests.HasValue)
            {
                var activeCount = await _context.ProfessionalCertificateRegistrations
                    .CountAsync(x =>
                        x.ProfessionalCertificateCourseId == course.Id &&
                        x.Status != ProfessionalCertificateRegistrationStatus.Rejected);
                if (activeCount >= course.MaxRequests.Value) continue;
            }

            var duplicate = await _context.ProfessionalCertificateRegistrations
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ProfessionalCertificateCourseId == course.Id &&
                    x.NationalId == model.NationalId &&
                    (x.Status == ProfessionalCertificateRegistrationStatus.New ||
                     x.Status == ProfessionalCertificateRegistrationStatus.Contacted ||
                     x.Status == ProfessionalCertificateRegistrationStatus.NeedFollowUp ||
                     x.Status == ProfessionalCertificateRegistrationStatus.Converted));

            if (duplicate) continue;

            toAdd.Add(new ProfessionalCertificateRegistration
            {
                ProfessionalCertificateCourseId = course.Id,
                FullName               = model.FullName.Trim(),
                PhoneNumber            = model.PhoneNumber.Trim(),
                NationalId             = model.NationalId.Trim(),
                Email                  = model.Email?.Trim(),
                City                   = model.City?.Trim(),
                Notes                  = model.Notes?.Trim(),
                Status                 = ProfessionalCertificateRegistrationStatus.New,
                SubmittedAt            = DateTime.UtcNow,
                IpAddress              = ip,
                UserAgent              = userAgent,
                RegistrationBatchId    = batchId
            });

            registeredNames.Add(course.TitleAr);
        }

        if (!toAdd.Any())
            return (false, "جميع الدورات المختارة إما مغلقة أو لديك تسجيل سابق بها.", new List<string>());

        _context.ProfessionalCertificateRegistrations.AddRange(toAdd);
        await _context.SaveChangesAsync();

        return (true, null, registeredNames);
    }
}
