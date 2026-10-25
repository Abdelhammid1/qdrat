using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Helpers;
using QdratNew.ViewModels.Admin.CoursePackages;

namespace QdratNew.Services.Frontend.Packages;

public class CoursePackageAdminService : ICoursePackageAdminService
{
    private const string PosterFolder = "packages";
    private const string PosterUrlPrefix = "/uploads/packages/";

    private readonly ApplicationDbContext _context;

    public CoursePackageAdminService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ─── القراءة ────────────────────────────────────────────────

    public async Task<List<CoursePackageListItemViewModel>> GetListAsync(CancellationToken ct = default)
    {
        return await _context.CoursePackages
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new CoursePackageListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                GradeLabel = x.GradeLabel,
                DiscountPercent = x.DiscountPercent,
                ItemsCount = x.Items.Count(),
                RegistrationsCount = x.Registrations.Count(),
                NewRegistrationsCount = x.Registrations.Count(r => r.Status == Enums.CoursePackageRegistrationStatus.New),
                IsActive = x.IsActive,
                ShowOnHomePage = x.ShowOnHomePage,
                DisplayOrder = x.DisplayOrder,
                PosterImagePath = x.PosterImagePath
            })
            .ToListAsync(ct);
    }

    public async Task<CoursePackageFormViewModel?> GetFormAsync(int id, CancellationToken ct = default)
    {
        var package = await _context.CoursePackages
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Tags)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (package == null) return null;

        return new CoursePackageFormViewModel
        {
            Id = package.Id,
            Name = package.Name,
            Slug = package.Slug,
            GradeLabel = package.GradeLabel,
            Subtitle = package.Subtitle,
            IntroText = package.IntroText,
            AccentColor = package.AccentColor,
            DiscountPercent = package.DiscountPercent,
            DiscountCaption = package.DiscountCaption,
            ButtonText = package.ButtonText,
            IsActive = package.IsActive,
            ShowOnHomePage = package.ShowOnHomePage,
            DisplayOrder = package.DisplayOrder,
            ExistingPosterPath = package.PosterImagePath,
            Items = package.Items.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id)
                .Select(i => new PackageItemFormViewModel
                {
                    Id = i.Id,
                    Title = i.Title,
                    IconKey = i.IconKey,
                    ColorHex = i.ColorHex,
                    ScheduleTitle = i.ScheduleTitle,
                    ScheduleText = i.ScheduleText,
                    ScheduleNote = i.ScheduleNote,
                    DisplayOrder = i.DisplayOrder
                }).ToList(),
            Tags = package.Tags.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id)
                .Select(t => new PackageTagFormViewModel
                {
                    Id = t.Id,
                    Text = t.Text,
                    IconKey = t.IconKey,
                    DisplayOrder = t.DisplayOrder
                }).ToList()
        };
    }

    // ─── الإنشاء ────────────────────────────────────────────────

    public async Task<(bool Ok, int Id, Dictionary<string, string> Errors)> CreateAsync(
        CoursePackageFormViewModel vm, string webRootPath, string? userId, CancellationToken ct = default)
    {
        var errors = Validate(vm);
        if (!errors.ContainsKey(nameof(vm.Slug)) && await SlugTakenAsync(vm.Slug.Trim(), null, ct))
            errors[nameof(vm.Slug)] = "الرابط المختصر مستخدم لباقة أخرى";
        if (errors.Count > 0) return (false, 0, errors);

        var package = new CoursePackage { CreatedAt = DateTime.UtcNow };
        ApplyScalars(package, vm, userId);
        ReconcileItems(package, vm.Items);
        ReconcileTags(package, vm.Tags);

        var uploaded = await TryUploadPosterAsync(vm.PosterFile, webRootPath, errors);
        if (errors.Count > 0) return (false, 0, errors);
        package.PosterImagePath = uploaded;

        try
        {
            _context.CoursePackages.Add(package);
            await _context.SaveChangesAsync(ct);
        }
        catch
        {
            DeletePosterFile(uploaded, webRootPath);
            throw;
        }

        return (true, package.Id, errors);
    }

    // ─── التعديل ────────────────────────────────────────────────

    public async Task<(bool Found, Dictionary<string, string> Errors)> UpdateAsync(
        CoursePackageFormViewModel vm, string webRootPath, string? userId, CancellationToken ct = default)
    {
        // استعلام واحد: الباقة مع الدورات والوسوم
        var package = await _context.CoursePackages
            .Include(x => x.Items)
            .Include(x => x.Tags)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == vm.Id, ct);

        if (package == null) return (false, new Dictionary<string, string>());

        var errors = Validate(vm);

        // كل Id قادم من النموذج يجب أن يخص هذه الباقة (منع التلاعب بين الباقات)
        var itemIds = package.Items.Select(i => i.Id).ToHashSet();
        if (vm.Items.Any(i => i.Id != 0 && !itemIds.Contains(i.Id)))
            errors[nameof(vm.Items)] = "بيانات الدورات غير صالحة، أعد تحميل الصفحة";
        var tagIds = package.Tags.Select(t => t.Id).ToHashSet();
        if (vm.Tags.Any(t => t.Id != 0 && !tagIds.Contains(t.Id)))
            errors[nameof(vm.Tags)] = "بيانات الوسوم غير صالحة، أعد تحميل الصفحة";

        if (!errors.ContainsKey(nameof(vm.Slug)) && await SlugTakenAsync(vm.Slug.Trim(), package.Id, ct))
            errors[nameof(vm.Slug)] = "الرابط المختصر مستخدم لباقة أخرى";
        if (errors.Count > 0) return (true, errors);

        // الصورة تُرفع بعد نجاح التحقق وقبل الحفظ
        var oldPoster = package.PosterImagePath;
        var uploaded = await TryUploadPosterAsync(vm.PosterFile, webRootPath, errors);
        if (errors.Count > 0) return (true, errors);

        ApplyScalars(package, vm, userId);
        ReconcileItems(package, vm.Items);
        ReconcileTags(package, vm.Tags);

        if (uploaded != null) package.PosterImagePath = uploaded;
        else if (vm.RemovePoster) package.PosterImagePath = null;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch
        {
            DeletePosterFile(uploaded, webRootPath);
            throw;
        }

        // بعد نجاح الحفظ فقط نحذف الصورة القديمة
        if (package.PosterImagePath != oldPoster)
            DeletePosterFile(oldPoster, webRootPath);

        return (true, errors);
    }

    // ─── Toggles / الحذف ────────────────────────────────────────

    public async Task<bool> ToggleActiveAsync(int id, CancellationToken ct = default)
    {
        var package = await _context.CoursePackages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (package == null) return false;
        package.IsActive = !package.IsActive;
        package.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ToggleHomeVisibilityAsync(int id, CancellationToken ct = default)
    {
        var package = await _context.CoursePackages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (package == null) return false;
        package.ShowOnHomePage = !package.ShowOnHomePage;
        package.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DeletePackageResult> DeleteAsync(int id, string webRootPath, CancellationToken ct = default)
    {
        var package = await _context.CoursePackages
            .Include(x => x.Items)
            .Include(x => x.Tags)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (package == null) return DeletePackageResult.NotFound;

        if (await _context.CoursePackageRegistrations.AnyAsync(r => r.CoursePackageId == id, ct))
            return DeletePackageResult.HasRegistrations;

        var poster = package.PosterImagePath;
        _context.CoursePackages.Remove(package);   // الأبناء Cascade
        await _context.SaveChangesAsync(ct);

        DeletePosterFile(poster, webRootPath);
        return DeletePackageResult.Deleted;
    }

    // ─── Helpers ────────────────────────────────────────────────

    private static Dictionary<string, string> Validate(CoursePackageFormViewModel vm)
    {
        var errors = new Dictionary<string, string>();

        if (!PackageStyleGuard.IsValidHex(vm.AccentColor))
            errors[nameof(vm.AccentColor)] = "لون غير صالح (#RRGGBB)";
        if (!PackageStyleGuard.IsValidSlug(vm.Slug?.Trim()))
            errors[nameof(vm.Slug)] = "الرابط: حروف إنجليزية صغيرة وأرقام وشرطات فقط";
        if (vm.DiscountPercent is < 1 or > 100)
            errors[nameof(vm.DiscountPercent)] = "نسبة الخصم بين 1 و100";
        if (vm.Items.Count is < 1 or > 8)
            errors[nameof(vm.Items)] = "عدد الدورات المشمولة بين 1 و8";
        if (vm.Tags.Count > 6)
            errors[nameof(vm.Tags)] = "الحد الأقصى 6 وسوم";

        for (var i = 0; i < vm.Items.Count; i++)
        {
            var item = vm.Items[i];
            if (string.IsNullOrWhiteSpace(item.Title))
                errors[$"Items[{i}].Title"] = "عنوان الدورة مطلوب";
            if (!PackageIconCatalog.IsValidKey(item.IconKey))
                errors[$"Items[{i}].IconKey"] = "أيقونة غير صالحة";
            if (!string.IsNullOrWhiteSpace(item.ColorHex) && !PackageStyleGuard.IsValidHex(item.ColorHex.Trim()))
                errors[$"Items[{i}].ColorHex"] = "لون غير صالح (#RRGGBB)";
        }

        for (var i = 0; i < vm.Tags.Count; i++)
        {
            var tag = vm.Tags[i];
            if (string.IsNullOrWhiteSpace(tag.Text))
                errors[$"Tags[{i}].Text"] = "نص الوسم مطلوب";
            if (!string.IsNullOrWhiteSpace(tag.IconKey) && !PackageIconCatalog.IsValidKey(tag.IconKey.Trim()))
                errors[$"Tags[{i}].IconKey"] = "أيقونة غير صالحة";
        }

        if (vm.PosterFile != null)
        {
            var ext = Path.GetExtension(vm.PosterFile.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                errors[nameof(vm.PosterFile)] = "الصورة: JPG أو PNG أو WebP فقط";
            else if (vm.PosterFile.Length > 5 * 1024 * 1024)
                errors[nameof(vm.PosterFile)] = "حجم الصورة لا يتجاوز 5 ميجابايت";
        }

        return errors;
    }

    private Task<bool> SlugTakenAsync(string slug, int? exceptId, CancellationToken ct) =>
        _context.CoursePackages.AnyAsync(x => x.Slug == slug && (exceptId == null || x.Id != exceptId), ct);

    private static void ApplyScalars(CoursePackage package, CoursePackageFormViewModel vm, string? userId)
    {
        package.Name = vm.Name.Trim();
        package.Slug = vm.Slug.Trim();
        package.GradeLabel = vm.GradeLabel.Trim();
        package.Subtitle = NullIfBlank(vm.Subtitle);
        package.IntroText = NullIfBlank(vm.IntroText);
        package.AccentColor = vm.AccentColor.Trim().ToUpperInvariant();
        package.DiscountPercent = vm.DiscountPercent;
        package.DiscountCaption = NullIfBlank(vm.DiscountCaption);
        package.ButtonText = vm.ButtonText.Trim();
        package.IsActive = vm.IsActive;
        package.ShowOnHomePage = vm.ShowOnHomePage;
        package.DisplayOrder = vm.DisplayOrder;
        package.UpdatedAt = DateTime.UtcNow;
        package.UpdatedByUserId = userId;
    }

    // Id=0 → إضافة · Id موجود → تحديث · غائب عن النموذج → حذف · الترتيب من ترتيب النموذج
    private static void ReconcileItems(CoursePackage package, List<PackageItemFormViewModel> incoming)
    {
        var existing = package.Items.ToDictionary(x => x.Id);
        var keep = new HashSet<int>();

        for (var i = 0; i < incoming.Count; i++)
        {
            var src = incoming[i];
            CoursePackageItem target;
            if (src.Id != 0 && existing.TryGetValue(src.Id, out var found))
            {
                target = found;
                keep.Add(found.Id);
            }
            else
            {
                target = new CoursePackageItem();
                package.Items.Add(target);
            }

            target.Title = src.Title.Trim();
            target.IconKey = src.IconKey.Trim();
            target.ColorHex = NullIfBlank(src.ColorHex)?.ToUpperInvariant();
            target.ScheduleTitle = NullIfBlank(src.ScheduleTitle);
            target.ScheduleText = NullIfBlank(src.ScheduleText);
            target.ScheduleNote = NullIfBlank(src.ScheduleNote);
            target.DisplayOrder = i + 1;
        }

        foreach (var item in existing.Values.Where(x => !keep.Contains(x.Id)).ToList())
            package.Items.Remove(item);
    }

    private static void ReconcileTags(CoursePackage package, List<PackageTagFormViewModel> incoming)
    {
        var existing = package.Tags.ToDictionary(x => x.Id);
        var keep = new HashSet<int>();

        for (var i = 0; i < incoming.Count; i++)
        {
            var src = incoming[i];
            CoursePackageTag target;
            if (src.Id != 0 && existing.TryGetValue(src.Id, out var found))
            {
                target = found;
                keep.Add(found.Id);
            }
            else
            {
                target = new CoursePackageTag();
                package.Tags.Add(target);
            }

            target.Text = src.Text.Trim();
            target.IconKey = NullIfBlank(src.IconKey);
            target.DisplayOrder = i + 1;
        }

        foreach (var tag in existing.Values.Where(x => !keep.Contains(x.Id)).ToList())
            package.Tags.Remove(tag);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<string?> TryUploadPosterAsync(
        Microsoft.AspNetCore.Http.IFormFile? file, string webRootPath, Dictionary<string, string> errors)
    {
        if (file == null || file.Length == 0) return null;
        try
        {
            return await ImageUploadHelper.SaveAsWebPAsync(file, webRootPath, PosterFolder, 1200);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors[nameof(CoursePackageFormViewModel.PosterFile)] = "تعذّر قراءة الصورة، تأكد أن الملف صورة صالحة";
            return null;
        }
    }

    /// <summary>يحذف الملف فقط إن كان داخل /uploads/packages/ (منع Path Traversal).</summary>
    public static void DeletePosterFile(string? posterPath, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath)) return;
        if (!posterPath.StartsWith(PosterUrlPrefix, StringComparison.Ordinal)) return;
        if (posterPath.Contains("..") || posterPath.Contains('\\')) return;

        try
        {
            var folder = Path.GetFullPath(Path.Combine(webRootPath, "uploads", PosterFolder));
            var fullPath = Path.GetFullPath(Path.Combine(webRootPath,
                posterPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            if (!fullPath.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch { /* تجاهل أخطاء الحذف */ }
    }
}
