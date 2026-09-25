using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Services.Frontend.HomePage;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.Admin.CourseCollections;
using QdratNew.ViewModels.Frontend.CourseCollections;

namespace QdratNew.Services.Frontend.CourseCollections;

public class CourseCollectionService : ICourseCollectionService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private const string LogoFolder = "course-collections";

    public CourseCollectionService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // ─── Frontend (Public) ──────────────────────────────────────

    public Task<CourseCollectionHomeSectionViewModel> GetHomeSectionAsync() =>
        HomePageCache.GetOrLoadAsync(_cache, HomePageCache.CourseCollectionsSectionKey,
                                     HomePageCache.CountersTtl, LoadHomeSectionAsync);

    private async Task<CourseCollectionHomeSectionViewModel> LoadHomeSectionAsync()
    {
        var vm = new CourseCollectionHomeSectionViewModel();

        var collections = await _context.CourseCollections
            .AsNoTracking()
            .Where(x => x.IsActive && x.ShowOnHomePage)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        if (!collections.Any()) return vm;

        var courses = await _context.CourseCollectionCourses
            .AsNoTracking()
            .Where(x => x.IsActive && x.ShowOnHomePage)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var sponsors = await _context.CourseCollectionSponsors
            .AsNoTracking()
            .Where(x => x.ShowOnHomePage)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        // عدد الطلبات الفعالة لكل دورة دفعة واحدة (بدون Query داخل Loop)
        var activeCounts = await _context.CourseCollectionRegistrations
            .AsNoTracking()
            .Where(x =>
                x.Status == CourseCollectionRegistrationStatus.New ||
                x.Status == CourseCollectionRegistrationStatus.Contacted ||
                x.Status == CourseCollectionRegistrationStatus.NeedFollowUp ||
                x.Status == CourseCollectionRegistrationStatus.Converted)
            .GroupBy(x => x.CourseCollectionCourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countDict = activeCounts.ToDictionary(x => x.CourseId, x => x.Count);

        foreach (var collection in collections)
        {
            var collectionCourses = courses.Where(c => c.CourseCollectionId == collection.Id).ToList();
            if (!collectionCourses.Any()) continue; // لا تُعرض المجموعة كاملة إن لم توجد دورات فعّالة

            var card = new CourseCollectionCardViewModel
            {
                Id = collection.Id,
                Name = collection.Name,
                Slug = collection.Slug,
                Subtitle = collection.Subtitle,
                Description = collection.Description,
                ButtonText = collection.ButtonText,
                LogoPath = collection.LogoPath,
                BackgroundImagePath = collection.BackgroundImagePath,
                DisplayStyle = collection.DisplayStyle
            };

            foreach (var course in collectionCourses)
            {
                var activeCount = countDict.TryGetValue(course.Id, out var cnt) ? cnt : 0;
                var isMaxReached = course.MaxRequests.HasValue && activeCount >= course.MaxRequests.Value;

                card.Courses.Add(new CourseCollectionCourseCardViewModel
                {
                    Id = course.Id,
                    CollectionSlug = collection.Slug,
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

            foreach (var sponsor in sponsors.Where(s => s.CourseCollectionId == collection.Id))
            {
                card.Sponsors.Add(new CourseCollectionSponsorViewModel
                {
                    Id = sponsor.Id,
                    Name = sponsor.Name,
                    LogoPath = sponsor.LogoPath,
                    LinkUrl = sponsor.LinkUrl
                });
            }

            vm.Collections.Add(card);
        }

        return vm;
    }

    public async Task<CourseCollectionRegisterViewModel?> GetRegisterViewModelAsync(string collectionSlug, string courseSlug)
    {
        var collection = await _context.CourseCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == collectionSlug && x.IsActive);

        if (collection == null) return null;

        var course = await _context.CourseCollectionCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CourseCollectionId == collection.Id && x.Slug == courseSlug && x.IsActive);

        if (course == null) return null;

        return new CourseCollectionRegisterViewModel
        {
            CollectionSlug = collection.Slug,
            CollectionName = collection.Name,
            CourseId = course.Id,
            CourseTitleAr = course.TitleAr,
            StandardCode = course.StandardCode,
            CourseShortDescription = course.ShortDescription,
            CourseFullDescription = course.FullDescription,
            LogoPath = course.LogoPath
        };
    }

    public async Task<(bool Success, int? RegistrationId, string? ErrorMessage)> CreateRegistrationAsync(
        CourseCollectionRegisterViewModel model, string? ip, string? userAgent)
    {
        var course = await _context.CourseCollectionCourses
            .AsNoTracking()
            .Include(x => x.CourseCollection)
            .FirstOrDefaultAsync(x => x.Id == model.CourseId);

        if (course == null || !course.IsActive)
            return (false, null, "الدورة غير موجودة أو غير متاحة.");

        var collection = course.CourseCollection;
        if (!collection.IsActive)
            return (false, null, "المجموعة غير متاحة حالياً.");

        if (!course.IsRegistrationOpen)
            return (false, null, "التسجيل في هذه الدورة مغلق حالياً.");

        // فحص MaxRequests للدورة
        if (course.MaxRequests.HasValue)
        {
            var activeCount = await _context.CourseCollectionRegistrations
                .CountAsync(x =>
                    x.CourseCollectionCourseId == course.Id &&
                    x.Status != CourseCollectionRegistrationStatus.Rejected);

            if (activeCount >= course.MaxRequests.Value)
                return (false, null, "اكتمل العدد المتاح لهذه الدورة.");
        }

        // فحص GlobalMaxRequests للمجموعة
        if (collection.AutoCloseWhenMaxReached && collection.GlobalMaxRequests.HasValue)
        {
            var globalCount = await _context.CourseCollectionRegistrations
                .Where(x =>
                    x.CourseCollectionCourse.CourseCollectionId == collection.Id &&
                    x.Status != CourseCollectionRegistrationStatus.Rejected)
                .CountAsync();

            if (globalCount >= collection.GlobalMaxRequests.Value)
                return (false, null, "اكتمل العدد الإجمالي المتاح للتسجيل في هذه المجموعة.");
        }

        // منع التكرار
        var duplicate = await _context.CourseCollectionRegistrations
            .AsNoTracking()
            .AnyAsync(x =>
                x.CourseCollectionCourseId == model.CourseId &&
                x.NationalId == model.NationalId &&
                (x.Status == CourseCollectionRegistrationStatus.New ||
                 x.Status == CourseCollectionRegistrationStatus.Contacted ||
                 x.Status == CourseCollectionRegistrationStatus.NeedFollowUp ||
                 x.Status == CourseCollectionRegistrationStatus.Converted));

        if (duplicate)
            return (false, null, "لديك طلب تسجيل سابق لهذه الدورة قيد المعالجة.");

        var registration = new CourseCollectionRegistration
        {
            CourseCollectionCourseId = model.CourseId,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            NationalId = model.NationalId,
            Email = model.Email,
            City = model.City,
            Notes = model.Notes,
            Status = CourseCollectionRegistrationStatus.New,
            SubmittedAt = DateTime.Now,
            IpAddress = ip,
            UserAgent = userAgent
        };

        _context.CourseCollectionRegistrations.Add(registration);
        await _context.SaveChangesAsync();

        return (true, registration.Id, null);
    }

    public async Task<CourseCollectionThanksViewModel?> GetThanksAsync(int registrationId)
    {
        var reg = await _context.CourseCollectionRegistrations
            .AsNoTracking()
            .Include(x => x.CourseCollectionCourse)
                .ThenInclude(c => c.CourseCollection)
            .FirstOrDefaultAsync(x => x.Id == registrationId);

        if (reg == null) return null;

        return new CourseCollectionThanksViewModel
        {
            RegistrationId = reg.Id,
            CollectionSlug = reg.CourseCollectionCourse.CourseCollection.Slug,
            CollectionName = reg.CourseCollectionCourse.CourseCollection.Name,
            CourseTitleAr = reg.CourseCollectionCourse.TitleAr,
            StandardCode = reg.CourseCollectionCourse.StandardCode,
            SubmittedAt = reg.SubmittedAt
        };
    }

    public async Task<CourseCollectionMultiRegisterViewModel?> GetMultiRegisterViewModelAsync(string collectionSlug)
    {
        var collection = await _context.CourseCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == collectionSlug && x.IsActive);

        if (collection == null) return null;

        var courses = await _context.CourseCollectionCourses
            .AsNoTracking()
            .Where(x => x.CourseCollectionId == collection.Id && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var items = new List<CourseCollectionCourseSelectItem>();

        foreach (var c in courses)
        {
            int activeCount = 0;
            if (c.MaxRequests.HasValue)
            {
                activeCount = await _context.CourseCollectionRegistrations
                    .CountAsync(x =>
                        x.CourseCollectionCourseId == c.Id &&
                        x.Status != CourseCollectionRegistrationStatus.Rejected);
            }

            items.Add(new CourseCollectionCourseSelectItem
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

        return new CourseCollectionMultiRegisterViewModel
        {
            CollectionSlug = collection.Slug,
            CollectionName = collection.Name,
            Courses = items
        };
    }

    public async Task<(bool Success, string? ErrorMessage, List<string> RegisteredCourses)> CreateMultiRegistrationAsync(
        string collectionSlug, CourseCollectionMultiRegisterViewModel model, string? ip, string? userAgent)
    {
        if (model.SelectedCourseIds == null || !model.SelectedCourseIds.Any())
            return (false, "يجب اختيار دورة واحدة على الأقل.", new List<string>());

        var collection = await _context.CourseCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == collectionSlug && x.IsActive);

        if (collection == null)
            return (false, "المجموعة غير متاحة حالياً.", new List<string>());

        // جلب الدورات الفعالة في هذه المجموعة إلى الذاكرة (بدون Contains() في SQL)
        var allCourses = await _context.CourseCollectionCourses
            .AsNoTracking()
            .Where(x => x.CourseCollectionId == collection.Id && x.IsActive)
            .ToListAsync();

        var selectedIds = model.SelectedCourseIds.Distinct().ToList();
        var selectedCourses = allCourses.Where(c => selectedIds.Contains(c.Id)).ToList();

        if (!selectedCourses.Any())
            return (false, "لم يتم العثور على الدورات المختارة.", new List<string>());

        if (collection.AutoCloseWhenMaxReached && collection.GlobalMaxRequests.HasValue)
        {
            var globalCount = await _context.CourseCollectionRegistrations
                .Where(x =>
                    x.CourseCollectionCourse.CourseCollectionId == collection.Id &&
                    x.Status != CourseCollectionRegistrationStatus.Rejected)
                .CountAsync();

            if (globalCount >= collection.GlobalMaxRequests.Value)
                return (false, "اكتمل العدد الإجمالي المتاح للتسجيل في هذه المجموعة.", new List<string>());
        }

        var batchId = Guid.NewGuid().ToString();
        var toAdd = new List<CourseCollectionRegistration>();
        var registeredNames = new List<string>();

        foreach (var course in selectedCourses)
        {
            if (!course.IsRegistrationOpen) continue;

            if (course.MaxRequests.HasValue)
            {
                var activeCount = await _context.CourseCollectionRegistrations
                    .CountAsync(x =>
                        x.CourseCollectionCourseId == course.Id &&
                        x.Status != CourseCollectionRegistrationStatus.Rejected);
                if (activeCount >= course.MaxRequests.Value) continue;
            }

            var duplicate = await _context.CourseCollectionRegistrations
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CourseCollectionCourseId == course.Id &&
                    x.NationalId == model.NationalId &&
                    (x.Status == CourseCollectionRegistrationStatus.New ||
                     x.Status == CourseCollectionRegistrationStatus.Contacted ||
                     x.Status == CourseCollectionRegistrationStatus.NeedFollowUp ||
                     x.Status == CourseCollectionRegistrationStatus.Converted));

            if (duplicate) continue;

            toAdd.Add(new CourseCollectionRegistration
            {
                CourseCollectionCourseId = course.Id,
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                NationalId = model.NationalId.Trim(),
                Email = model.Email?.Trim(),
                City = model.City?.Trim(),
                Notes = model.Notes?.Trim(),
                Status = CourseCollectionRegistrationStatus.New,
                SubmittedAt = DateTime.Now,
                IpAddress = ip,
                UserAgent = userAgent,
                RegistrationBatchId = batchId
            });

            registeredNames.Add(course.TitleAr);
        }

        if (!toAdd.Any())
            return (false, "جميع الدورات المختارة إما مغلقة أو لديك تسجيل سابق بها.", new List<string>());

        _context.CourseCollectionRegistrations.AddRange(toAdd);
        await _context.SaveChangesAsync();

        return (true, null, registeredNames);
    }

    // ─── Admin - Collections ───────────────────────────────────────

    public async Task<IReadOnlyList<CourseCollectionListItemViewModel>> GetAdminCollectionsAsync()
    {
        var collections = await _context.CourseCollections
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var collectionIds = new List<int>();
        foreach (var c in collections) collectionIds.Add(c.Id);

        var courseCounts = new Dictionary<int, int>();
        var sponsorCounts = new Dictionary<int, int>();

        if (collectionIds.Any())
        {
            var courseCountRows = await _context.CourseCollectionCourses
                .AsNoTracking()
                .Where(x =>
                    x.CourseCollectionId == collectionIds[0] ||
                    (collectionIds.Count > 1 && x.CourseCollectionId == collectionIds[1]) ||
                    (collectionIds.Count > 2 && x.CourseCollectionId == collectionIds[2]) ||
                    (collectionIds.Count > 3 && x.CourseCollectionId == collectionIds[3]) ||
                    (collectionIds.Count > 4 && x.CourseCollectionId == collectionIds[4]) ||
                    (collectionIds.Count > 5 && x.CourseCollectionId == collectionIds[5]) ||
                    (collectionIds.Count > 6 && x.CourseCollectionId == collectionIds[6]) ||
                    (collectionIds.Count > 7 && x.CourseCollectionId == collectionIds[7]) ||
                    (collectionIds.Count > 8 && x.CourseCollectionId == collectionIds[8]) ||
                    (collectionIds.Count > 9 && x.CourseCollectionId == collectionIds[9]))
                .GroupBy(x => x.CourseCollectionId)
                .Select(g => new { CollectionId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var row in courseCountRows) courseCounts[row.CollectionId] = row.Count;

            var sponsorCountRows = await _context.CourseCollectionSponsors
                .AsNoTracking()
                .Where(x =>
                    x.CourseCollectionId == collectionIds[0] ||
                    (collectionIds.Count > 1 && x.CourseCollectionId == collectionIds[1]) ||
                    (collectionIds.Count > 2 && x.CourseCollectionId == collectionIds[2]) ||
                    (collectionIds.Count > 3 && x.CourseCollectionId == collectionIds[3]) ||
                    (collectionIds.Count > 4 && x.CourseCollectionId == collectionIds[4]) ||
                    (collectionIds.Count > 5 && x.CourseCollectionId == collectionIds[5]) ||
                    (collectionIds.Count > 6 && x.CourseCollectionId == collectionIds[6]) ||
                    (collectionIds.Count > 7 && x.CourseCollectionId == collectionIds[7]) ||
                    (collectionIds.Count > 8 && x.CourseCollectionId == collectionIds[8]) ||
                    (collectionIds.Count > 9 && x.CourseCollectionId == collectionIds[9]))
                .GroupBy(x => x.CourseCollectionId)
                .Select(g => new { CollectionId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var row in sponsorCountRows) sponsorCounts[row.CollectionId] = row.Count;
        }

        var result = new List<CourseCollectionListItemViewModel>();
        foreach (var c in collections)
        {
            result.Add(new CourseCollectionListItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                LogoPath = c.LogoPath,
                DisplayStyle = c.DisplayStyle,
                CourseCount = courseCounts.TryGetValue(c.Id, out var cc) ? cc : 0,
                SponsorCount = sponsorCounts.TryGetValue(c.Id, out var sc) ? sc : 0,
                IsActive = c.IsActive,
                ShowOnHomePage = c.ShowOnHomePage,
                DisplayOrder = c.DisplayOrder
            });
        }

        return result;
    }

    public async Task<CourseCollectionFormViewModel?> GetCollectionFormAsync(int id)
    {
        var collection = await _context.CourseCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (collection == null) return null;

        return new CourseCollectionFormViewModel
        {
            Id = collection.Id,
            Name = collection.Name,
            Slug = collection.Slug,
            Subtitle = collection.Subtitle,
            Description = collection.Description,
            ButtonText = collection.ButtonText,
            DisplayStyle = collection.DisplayStyle,
            IsActive = collection.IsActive,
            ShowOnHomePage = collection.ShowOnHomePage,
            DisplayOrder = collection.DisplayOrder,
            AutoCloseWhenMaxReached = collection.AutoCloseWhenMaxReached,
            GlobalMaxRequests = collection.GlobalMaxRequests,
            ClosedMessage = collection.ClosedMessage,
            ExistingLogoPath = collection.LogoPath,
            ExistingBackgroundImagePath = collection.BackgroundImagePath
        };
    }

    public async Task<int> CreateCollectionAsync(CourseCollectionFormViewModel model, string webRootPath)
    {
        string? logoPath = null;
        if (model.LogoFile != null && model.LogoFile.Length > 0)
            logoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, LogoFolder, 600);

        string? backgroundImagePath = null;
        if (model.BackgroundImageFile != null && model.BackgroundImageFile.Length > 0)
            backgroundImagePath = await ImageUploadHelper.SaveAsWebPAsync(model.BackgroundImageFile, webRootPath, LogoFolder, 1920);

        var collection = new CourseCollection
        {
            Name = model.Name,
            Slug = model.Slug.ToLower().Trim(),
            Subtitle = model.Subtitle,
            Description = model.Description,
            ButtonText = model.ButtonText,
            LogoPath = logoPath,
            BackgroundImagePath = backgroundImagePath,
            DisplayStyle = model.DisplayStyle,
            IsActive = model.IsActive,
            ShowOnHomePage = model.ShowOnHomePage,
            DisplayOrder = model.DisplayOrder,
            AutoCloseWhenMaxReached = model.AutoCloseWhenMaxReached,
            GlobalMaxRequests = model.GlobalMaxRequests,
            ClosedMessage = model.ClosedMessage,
            CreatedAt = DateTime.Now
        };

        _context.CourseCollections.Add(collection);
        await _context.SaveChangesAsync();
        return collection.Id;
    }

    public async Task<bool> UpdateCollectionAsync(CourseCollectionFormViewModel model, string webRootPath)
    {
        var collection = await _context.CourseCollections.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (collection == null) return false;

        if (model.RemoveLogo && !string.IsNullOrEmpty(collection.LogoPath))
        {
            DeleteLogoFile(collection.LogoPath, webRootPath);
            collection.LogoPath = null;
        }
        else if (model.LogoFile != null && model.LogoFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(collection.LogoPath))
                DeleteLogoFile(collection.LogoPath, webRootPath);

            collection.LogoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, LogoFolder, 600);
        }

        if (model.RemoveBackgroundImage && !string.IsNullOrEmpty(collection.BackgroundImagePath))
        {
            DeleteLogoFile(collection.BackgroundImagePath, webRootPath);
            collection.BackgroundImagePath = null;
        }
        else if (model.BackgroundImageFile != null && model.BackgroundImageFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(collection.BackgroundImagePath))
                DeleteLogoFile(collection.BackgroundImagePath, webRootPath);

            collection.BackgroundImagePath = await ImageUploadHelper.SaveAsWebPAsync(model.BackgroundImageFile, webRootPath, LogoFolder, 1920);
        }

        collection.Name = model.Name;
        collection.Slug = model.Slug.ToLower().Trim();
        collection.Subtitle = model.Subtitle;
        collection.Description = model.Description;
        collection.ButtonText = model.ButtonText;
        collection.DisplayStyle = model.DisplayStyle;
        collection.IsActive = model.IsActive;
        collection.ShowOnHomePage = model.ShowOnHomePage;
        collection.DisplayOrder = model.DisplayOrder;
        collection.AutoCloseWhenMaxReached = model.AutoCloseWhenMaxReached;
        collection.GlobalMaxRequests = model.GlobalMaxRequests;
        collection.ClosedMessage = model.ClosedMessage;
        collection.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCollectionActiveAsync(int id)
    {
        var collection = await _context.CourseCollections.FirstOrDefaultAsync(x => x.Id == id);
        if (collection == null) return false;
        collection.IsActive = !collection.IsActive;
        collection.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCollectionHomeVisibilityAsync(int id)
    {
        var collection = await _context.CourseCollections.FirstOrDefaultAsync(x => x.Id == id);
        if (collection == null) return false;
        collection.ShowOnHomePage = !collection.ShowOnHomePage;
        collection.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Admin - Courses within a Collection ───────────────────────

    public async Task<IReadOnlyList<CourseCollectionCourseListItemViewModel>> GetAdminCoursesAsync(int collectionId)
    {
        var courses = await _context.CourseCollectionCourses
            .AsNoTracking()
            .Where(x => x.CourseCollectionId == collectionId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var courseIds = new List<int>();
        foreach (var c in courses) courseIds.Add(c.Id);

        var counts = new Dictionary<int, int>();
        if (courseIds.Any())
        {
            var regCounts = await _context.CourseCollectionRegistrations
                .AsNoTracking()
                .Where(x =>
                    x.CourseCollectionCourseId == courseIds[0] ||
                    (courseIds.Count > 1 && x.CourseCollectionCourseId == courseIds[1]) ||
                    (courseIds.Count > 2 && x.CourseCollectionCourseId == courseIds[2]) ||
                    (courseIds.Count > 3 && x.CourseCollectionCourseId == courseIds[3]) ||
                    (courseIds.Count > 4 && x.CourseCollectionCourseId == courseIds[4]) ||
                    (courseIds.Count > 5 && x.CourseCollectionCourseId == courseIds[5]) ||
                    (courseIds.Count > 6 && x.CourseCollectionCourseId == courseIds[6]) ||
                    (courseIds.Count > 7 && x.CourseCollectionCourseId == courseIds[7]) ||
                    (courseIds.Count > 8 && x.CourseCollectionCourseId == courseIds[8]) ||
                    (courseIds.Count > 9 && x.CourseCollectionCourseId == courseIds[9]))
                .GroupBy(x => x.CourseCollectionCourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var rc in regCounts) counts[rc.CourseId] = rc.Count;
        }

        var result = new List<CourseCollectionCourseListItemViewModel>();
        foreach (var c in courses)
        {
            result.Add(new CourseCollectionCourseListItemViewModel
            {
                Id = c.Id,
                CourseCollectionId = c.CourseCollectionId,
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

    public async Task<CourseCollectionCourseFormViewModel?> GetCourseFormAsync(int id)
    {
        var course = await _context.CourseCollectionCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (course == null) return null;

        return new CourseCollectionCourseFormViewModel
        {
            Id = course.Id,
            CourseCollectionId = course.CourseCollectionId,
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

    public async Task<int> CreateCourseAsync(CourseCollectionCourseFormViewModel model, string webRootPath)
    {
        string? logoPath = null;
        if (model.LogoFile != null && model.LogoFile.Length > 0)
            logoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, LogoFolder, 600);

        var course = new CourseCollectionCourse
        {
            CourseCollectionId = model.CourseCollectionId,
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

        _context.CourseCollectionCourses.Add(course);
        await _context.SaveChangesAsync();
        return course.Id;
    }

    public async Task<bool> UpdateCourseAsync(CourseCollectionCourseFormViewModel model, string webRootPath)
    {
        var course = await _context.CourseCollectionCourses.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (course == null) return false;

        if (model.RemoveLogo && !string.IsNullOrEmpty(course.LogoPath))
        {
            DeleteLogoFile(course.LogoPath, webRootPath);
            course.LogoPath = null;
        }
        else if (model.LogoFile != null && model.LogoFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(course.LogoPath))
                DeleteLogoFile(course.LogoPath, webRootPath);

            course.LogoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, LogoFolder, 600);
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

    public async Task<bool> ToggleCourseActiveAsync(int id)
    {
        var course = await _context.CourseCollectionCourses.FirstOrDefaultAsync(x => x.Id == id);
        if (course == null) return false;
        course.IsActive = !course.IsActive;
        course.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCourseHomeVisibilityAsync(int id)
    {
        var course = await _context.CourseCollectionCourses.FirstOrDefaultAsync(x => x.Id == id);
        if (course == null) return false;
        course.ShowOnHomePage = !course.ShowOnHomePage;
        course.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCourseRegistrationOpenAsync(int id)
    {
        var course = await _context.CourseCollectionCourses.FirstOrDefaultAsync(x => x.Id == id);
        if (course == null) return false;
        course.IsRegistrationOpen = !course.IsRegistrationOpen;
        course.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Admin - Sponsors within a Collection ──────────────────────

    public async Task<IReadOnlyList<CourseCollectionSponsorListItemViewModel>> GetAdminSponsorsAsync(int collectionId)
    {
        return await _context.CourseCollectionSponsors
            .AsNoTracking()
            .Where(x => x.CourseCollectionId == collectionId)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CourseCollectionSponsorListItemViewModel
            {
                Id = x.Id,
                CourseCollectionId = x.CourseCollectionId,
                Name = x.Name,
                LogoPath = x.LogoPath,
                LinkUrl = x.LinkUrl,
                ShowOnHomePage = x.ShowOnHomePage,
                DisplayOrder = x.DisplayOrder
            })
            .ToListAsync();
    }

    public async Task<int> CreateSponsorAsync(CourseCollectionSponsorFormViewModel model, string webRootPath)
    {
        if (model.LogoFile == null || model.LogoFile.Length == 0)
            throw new InvalidOperationException("لوجو الراعي مطلوب.");

        var logoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, LogoFolder, 400);

        var sponsor = new CourseCollectionSponsor
        {
            CourseCollectionId = model.CourseCollectionId,
            Name = model.Name,
            LogoPath = logoPath,
            LinkUrl = model.LinkUrl,
            ShowOnHomePage = model.ShowOnHomePage,
            DisplayOrder = model.DisplayOrder,
            CreatedAt = DateTime.Now
        };

        _context.CourseCollectionSponsors.Add(sponsor);
        await _context.SaveChangesAsync();
        return sponsor.Id;
    }

    public async Task<bool> UpdateSponsorAsync(CourseCollectionSponsorFormViewModel model, string webRootPath)
    {
        var sponsor = await _context.CourseCollectionSponsors.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (sponsor == null) return false;

        if (model.LogoFile != null && model.LogoFile.Length > 0)
        {
            DeleteLogoFile(sponsor.LogoPath, webRootPath);
            sponsor.LogoPath = await ImageUploadHelper.SaveAsWebPAsync(model.LogoFile, webRootPath, LogoFolder, 400);
        }

        sponsor.Name = model.Name;
        sponsor.LinkUrl = model.LinkUrl;
        sponsor.ShowOnHomePage = model.ShowOnHomePage;
        sponsor.DisplayOrder = model.DisplayOrder;
        sponsor.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleSponsorHomeVisibilityAsync(int id)
    {
        var sponsor = await _context.CourseCollectionSponsors.FirstOrDefaultAsync(x => x.Id == id);
        if (sponsor == null) return false;
        sponsor.ShowOnHomePage = !sponsor.ShowOnHomePage;
        sponsor.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteSponsorAsync(int id, string webRootPath)
    {
        var sponsor = await _context.CourseCollectionSponsors.FirstOrDefaultAsync(x => x.Id == id);
        if (sponsor == null) return false;

        DeleteLogoFile(sponsor.LogoPath, webRootPath);

        _context.CourseCollectionSponsors.Remove(sponsor);
        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Admin - Registrations ──────────────────────────────────────

    public async Task<CourseCollectionDashboardViewModel> GetAdminDashboardAsync(CourseCollectionRegistrationFilterViewModel filter)
    {
        var query = _context.CourseCollectionRegistrations
            .AsNoTracking()
            .Include(x => x.CourseCollectionCourse)
                .ThenInclude(c => c.CourseCollection);

        // إحصائيات الحالات
        var allForStats = await query.Select(x => x.Status).ToListAsync();
        var totalCount = allForStats.Count;
        var newCount = allForStats.Count(s => s == CourseCollectionRegistrationStatus.New);
        var contactedCount = allForStats.Count(s => s == CourseCollectionRegistrationStatus.Contacted);
        var needFollowUpCount = allForStats.Count(s => s == CourseCollectionRegistrationStatus.NeedFollowUp);
        var rejectedCount = allForStats.Count(s => s == CourseCollectionRegistrationStatus.Rejected);
        var convertedCount = allForStats.Count(s => s == CourseCollectionRegistrationStatus.Converted);

        // الفلترة
        var filteredQuery = _context.CourseCollectionRegistrations
            .AsNoTracking()
            .Include(x => x.CourseCollectionCourse)
                .ThenInclude(c => c.CourseCollection)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            filteredQuery = filteredQuery.Where(x =>
                x.FullName.Contains(search) ||
                x.PhoneNumber.Contains(search) ||
                x.NationalId.Contains(search));
        }

        if (filter.CourseCollectionId.HasValue)
            filteredQuery = filteredQuery.Where(x => x.CourseCollectionCourse.CourseCollectionId == filter.CourseCollectionId.Value);

        if (filter.CourseId.HasValue)
            filteredQuery = filteredQuery.Where(x => x.CourseCollectionCourseId == filter.CourseId.Value);

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

        // جلب كل الصفوف المفلترة إلى الذاكرة ثم التجميع حسب BatchId
        var rawItems = await filteredQuery
            .OrderByDescending(x => x.SubmittedAt)
            .Select(x => new
            {
                x.Id,
                x.RegistrationBatchId,
                x.FullName,
                x.PhoneNumber,
                x.NationalId,
                CollectionName = x.CourseCollectionCourse.CourseCollection.Name,
                CourseTitleAr = x.CourseCollectionCourse.TitleAr,
                StandardCode = x.CourseCollectionCourse.StandardCode,
                x.Status,
                x.SubmittedAt,
                x.LastUpdatedAt,
                x.HandledByName
            })
            .ToListAsync();

        // التجميع: تسجيلات الاختيار المتعدد تشترك بنفس BatchId
        var grouped = rawItems
            .GroupBy(x => x.RegistrationBatchId ?? ("solo_" + x.Id))
            .Select(g =>
            {
                var rep = g.OrderBy(x => x.Id).First();
                return new CourseCollectionRegistrationListItemViewModel
                {
                    Id = rep.Id,
                    RegistrationBatchId = rep.RegistrationBatchId,
                    CollectionName = rep.CollectionName,
                    FullName = rep.FullName,
                    PhoneNumber = rep.PhoneNumber,
                    NationalId = rep.NationalId,
                    Courses = g.Select(x => new CcrCourseBadge
                    {
                        RegistrationId = x.Id,
                        TitleAr = x.CourseTitleAr,
                        StandardCode = x.StandardCode
                    }).ToList(),
                    Status = rep.Status,
                    SubmittedAt = rep.SubmittedAt,
                    LastUpdatedAt = g.Max(x => x.LastUpdatedAt),
                    HandledByName = rep.HandledByName
                };
            })
            .OrderByDescending(x => x.SubmittedAt)
            .ToList();

        var filteredCount = grouped.Count;
        var totalPages = (int)Math.Ceiling((double)filteredCount / pageSize);
        var items = grouped.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var collectionsForFilter = await _context.CourseCollections
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CourseCollectionListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug
            })
            .ToListAsync();

        var coursesQuery = _context.CourseCollectionCourses.AsNoTracking();
        if (filter.CourseCollectionId.HasValue)
            coursesQuery = coursesQuery.Where(x => x.CourseCollectionId == filter.CourseCollectionId.Value);

        var coursesForFilter = await coursesQuery
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CourseCollectionCourseListItemViewModel
            {
                Id = x.Id,
                CourseCollectionId = x.CourseCollectionId,
                TitleAr = x.TitleAr,
                StandardCode = x.StandardCode
            })
            .ToListAsync();

        return new CourseCollectionDashboardViewModel
        {
            TotalCount = totalCount,
            NewCount = newCount,
            ContactedCount = contactedCount,
            NeedFollowUpCount = needFollowUpCount,
            RejectedCount = rejectedCount,
            ConvertedCount = convertedCount,
            Items = items,
            Filter = filter,
            CollectionsForFilter = collectionsForFilter,
            CoursesForFilter = coursesForFilter,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<CourseCollectionRegistrationDetailsViewModel?> GetRegistrationDetailsAsync(int id)
    {
        var reg = await _context.CourseCollectionRegistrations
            .AsNoTracking()
            .Include(x => x.CourseCollectionCourse)
                .ThenInclude(c => c.CourseCollection)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (reg == null) return null;

        // تحميل كل الدورات في هذا الطلب (Batch)
        List<CcrCourseBadge> allCourses;
        if (!string.IsNullOrEmpty(reg.RegistrationBatchId))
        {
            var siblings = await _context.CourseCollectionRegistrations
                .AsNoTracking()
                .Include(x => x.CourseCollectionCourse)
                .Where(x => x.RegistrationBatchId == reg.RegistrationBatchId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            allCourses = siblings.Select(x => new CcrCourseBadge
            {
                RegistrationId = x.Id,
                TitleAr = x.CourseCollectionCourse.TitleAr,
                StandardCode = x.CourseCollectionCourse.StandardCode
            }).ToList();
        }
        else
        {
            allCourses = new List<CcrCourseBadge>
            {
                new CcrCourseBadge
                {
                    RegistrationId = reg.Id,
                    TitleAr = reg.CourseCollectionCourse.TitleAr,
                    StandardCode = reg.CourseCollectionCourse.StandardCode
                }
            };
        }

        return new CourseCollectionRegistrationDetailsViewModel
        {
            Id = reg.Id,
            RegistrationBatchId = reg.RegistrationBatchId,
            CollectionName = reg.CourseCollectionCourse.CourseCollection.Name,
            CollectionSlug = reg.CourseCollectionCourse.CourseCollection.Slug,
            FullName = reg.FullName,
            PhoneNumber = reg.PhoneNumber,
            NationalId = reg.NationalId,
            Email = reg.Email,
            City = reg.City,
            Notes = reg.Notes,
            Status = reg.Status,
            AdminNotes = reg.AdminNotes,
            RejectionReason = reg.RejectionReason,
            IsContacted = reg.IsContacted,
            ContactedAt = reg.ContactedAt,
            FollowUpAt = reg.FollowUpAt,
            HandledByName = reg.HandledByName,
            SubmittedAt = reg.SubmittedAt,
            LastUpdatedAt = reg.LastUpdatedAt,
            IpAddress = reg.IpAddress,
            UserAgent = reg.UserAgent,
            AllCourses = allCourses,
            CourseId = reg.CourseCollectionCourseId,
            CourseTitleAr = reg.CourseCollectionCourse.TitleAr,
            StandardCode = reg.CourseCollectionCourse.StandardCode
        };
    }

    public async Task<bool> UpdateRegistrationStatusAsync(CourseCollectionRegistrationUpdateStatusViewModel model, string userId, string userName)
    {
        var reg = await _context.CourseCollectionRegistrations
            .FirstOrDefaultAsync(x => x.Id == model.RegistrationId);

        if (reg == null) return false;

        // جمع كل السجلات في نفس الـ Batch (أو هذا السجل فقط)
        var toUpdate = new List<CourseCollectionRegistration> { reg };
        if (!string.IsNullOrEmpty(reg.RegistrationBatchId))
        {
            var bId = reg.RegistrationBatchId;
            var siblings = await _context.CourseCollectionRegistrations
                .Where(x => x.RegistrationBatchId == bId && x.Id != reg.Id)
                .ToListAsync();
            toUpdate.AddRange(siblings);
        }

        var now = DateTime.Now;
        foreach (var r in toUpdate)
        {
            r.Status = model.NewStatus;
            r.AdminNotes = model.AdminNotes;
            r.LastUpdatedAt = now;
            r.HandledByUserId = userId;
            r.HandledByName = userName;

            if (!string.IsNullOrEmpty(model.RejectionReason))
                r.RejectionReason = model.RejectionReason;

            if (model.NewStatus == CourseCollectionRegistrationStatus.Contacted)
            {
                r.IsContacted = true;
                r.ContactedAt = now;
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Helpers ────────────────────────────────────────────────────

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
}
