using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Helpers;
using QdratNew.Services.Frontend.HomePage;
using QdratNew.ViewModels.Frontend.Packages;

namespace QdratNew.Services.Frontend.Packages;

public class CoursePackagePublicService : ICoursePackagePublicService
{
    private const int HomeMaxPackages = 6;

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public CoursePackagePublicService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public Task<List<PackageCardViewModel>> GetHomeSectionAsync() =>
        HomePageCache.GetOrLoadAsync(_cache, HomePageCache.PackagesSectionKey,
                                     HomePageCache.ContentTtl, LoadHomeSectionAsync);

    private async Task<List<PackageCardViewModel>> LoadHomeSectionAsync()
    {
        var packages = await _context.CoursePackages
            .AsNoTracking()
            .Where(p => p.IsActive && p.ShowOnHomePage)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
            .Take(HomeMaxPackages)
            .Select(p => new PackageRow
            {
                Id = p.Id, Name = p.Name, Slug = p.Slug, GradeLabel = p.GradeLabel, Subtitle = p.Subtitle,
                AccentColor = p.AccentColor, DiscountPercent = p.DiscountPercent,
                DiscountCaption = p.DiscountCaption, ButtonText = p.ButtonText,
                PosterImagePath = p.PosterImagePath
            })
            .ToListAsync();

        if (packages.Count == 0) return new List<PackageCardViewModel>();

        // استعلامان للأبناء بدل Query لكل باقة؛ الفلترة بالعلاقة (بدون Contains/OPENJSON)
        var items = await _context.CoursePackageItems
            .AsNoTracking()
            .Where(i => i.CoursePackage!.IsActive && i.CoursePackage.ShowOnHomePage)
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id)
            .Select(i => new ItemRow
            {
                PackageId = i.CoursePackageId, Title = i.Title, IconKey = i.IconKey, ColorHex = i.ColorHex,
                ScheduleTitle = i.ScheduleTitle, ScheduleText = i.ScheduleText, ScheduleNote = i.ScheduleNote
            })
            .ToListAsync();

        var tags = await _context.CoursePackageTags
            .AsNoTracking()
            .Where(t => t.CoursePackage!.IsActive && t.CoursePackage.ShowOnHomePage)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id)
            .Select(t => new TagRow { PackageId = t.CoursePackageId, Text = t.Text, IconKey = t.IconKey })
            .ToListAsync();

        return BuildCards(packages, items, tags);
    }

    public async Task<List<PackageCardViewModel>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var packages = await _context.CoursePackages
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
            .Select(p => new PackageRow
            {
                Id = p.Id, Name = p.Name, Slug = p.Slug, GradeLabel = p.GradeLabel, Subtitle = p.Subtitle,
                AccentColor = p.AccentColor, DiscountPercent = p.DiscountPercent,
                DiscountCaption = p.DiscountCaption, ButtonText = p.ButtonText,
                PosterImagePath = p.PosterImagePath
            })
            .ToListAsync(ct);

        if (packages.Count == 0) return new List<PackageCardViewModel>();

        var items = await _context.CoursePackageItems
            .AsNoTracking()
            .Where(i => i.CoursePackage!.IsActive)
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id)
            .Select(i => new ItemRow
            {
                PackageId = i.CoursePackageId, Title = i.Title, IconKey = i.IconKey, ColorHex = i.ColorHex,
                ScheduleTitle = i.ScheduleTitle, ScheduleText = i.ScheduleText, ScheduleNote = i.ScheduleNote
            })
            .ToListAsync(ct);

        var tags = await _context.CoursePackageTags
            .AsNoTracking()
            .Where(t => t.CoursePackage!.IsActive)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id)
            .Select(t => new TagRow { PackageId = t.CoursePackageId, Text = t.Text, IconKey = t.IconKey })
            .ToListAsync(ct);

        return BuildCards(packages, items, tags);
    }

    public async Task<PackageDetailsViewModel?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var p = await _context.CoursePackages
            .AsNoTracking()
            .Where(x => x.Slug == slug && x.IsActive)
            .Select(x => new
            {
                x.Id, x.Name, x.Slug, x.GradeLabel, x.Subtitle, x.IntroText, x.AccentColor,
                x.DiscountPercent, x.DiscountCaption, x.ButtonText, x.PosterImagePath
            })
            .FirstOrDefaultAsync(ct);

        if (p == null) return null;

        var items = await _context.CoursePackageItems
            .AsNoTracking()
            .Where(i => i.CoursePackageId == p.Id)
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id)
            .Select(i => new ItemRow
            {
                PackageId = i.CoursePackageId, Title = i.Title, IconKey = i.IconKey, ColorHex = i.ColorHex,
                ScheduleTitle = i.ScheduleTitle, ScheduleText = i.ScheduleText, ScheduleNote = i.ScheduleNote
            })
            .ToListAsync(ct);

        var tags = await _context.CoursePackageTags
            .AsNoTracking()
            .Where(t => t.CoursePackageId == p.Id)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id)
            .Select(t => new TagRow { PackageId = t.CoursePackageId, Text = t.Text, IconKey = t.IconKey })
            .ToListAsync(ct);

        var accent = SafeAccent(p.AccentColor);
        return new PackageDetailsViewModel
        {
            Name = p.Name, Slug = p.Slug, GradeLabel = p.GradeLabel, Subtitle = p.Subtitle,
            IntroText = p.IntroText, AccentColor = accent, OnAccentColor = PackageStyleGuard.ReadableTextColor(accent),
            DiscountPercent = p.DiscountPercent, DiscountCaption = p.DiscountCaption, ButtonText = p.ButtonText,
            PosterImagePath = p.PosterImagePath,
            Items = MapItems(items), Tags = MapTags(tags)
        };
    }

    public async Task<SubmitPackageResult> SubmitAsync(
        string slug, PackageRegisterFormViewModel form, string? ip, CancellationToken ct = default)
    {
        var result = new SubmitPackageResult();

        var package = string.IsNullOrWhiteSpace(slug)
            ? null
            : await _context.CoursePackages
                .AsNoTracking()
                .Where(p => p.Slug == slug && p.IsActive)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(ct);

        if (package == null)
        {
            result.NotFound = true;
            return result;
        }

        var name = (form.FullName ?? string.Empty).Trim();
        var phone = NumberHelper.NormalizeDigits(form.PhoneNumber);

        if (name.Length < 3 || name.Length > 150)
            result.Errors[nameof(form.FullName)] = "الاسم بين 3 و150 حرفًا";
        if (!PhoneRx.IsMatch(phone))
            result.Errors[nameof(form.PhoneNumber)] = "رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام";
        if (result.Errors.Count > 0) return result;

        // منع التكرار: نفس (الباقة + الجوال) خلال 10 دقائق ⇒ نجاح صامت دون إضافة
        var since = DateTime.UtcNow.AddMinutes(-DuplicateWindowMinutes);
        var duplicate = await _context.CoursePackageRegistrations
            .AsNoTracking()
            .AnyAsync(r => r.CoursePackageId == package.Id && r.PhoneNumber == phone && r.SubmittedAt >= since, ct);

        if (!duplicate)
        {
            _context.CoursePackageRegistrations.Add(new CoursePackageRegistration
            {
                CoursePackageId = package.Id,
                PackageNameSnapshot = package.Name,
                FullName = name,
                PhoneNumber = phone,
                Status = CoursePackageRegistrationStatus.New,
                SubmittedAt = DateTime.UtcNow,
                IpAddress = ip is { Length: > 45 } ? ip[..45] : ip
            });
            await _context.SaveChangesAsync(ct);
            _cache.Remove(QdratNew.ViewComponents.NewPackageRegistrationsBadgeViewComponent.CacheKey);
        }

        result.Success = true;
        return result;
    }

    // ─── Helpers ────────────────────────────────────────────────

    private const int DuplicateWindowMinutes = 10;
    private static readonly Regex PhoneRx = new(@"^05\d{8}$", RegexOptions.Compiled);

    private static List<PackageCardViewModel> BuildCards(
        List<PackageRow> packages, List<ItemRow> items, List<TagRow> tags)
    {
        var itemsByPackage = items.ToLookup(i => i.PackageId);
        var tagsByPackage = tags.ToLookup(t => t.PackageId);

        var result = new List<PackageCardViewModel>(packages.Count);
        foreach (var p in packages)
        {
            var accent = SafeAccent(p.AccentColor);
            result.Add(new PackageCardViewModel
            {
                Name = p.Name, Slug = p.Slug, GradeLabel = p.GradeLabel, Subtitle = p.Subtitle,
                AccentColor = accent, OnAccentColor = PackageStyleGuard.ReadableTextColor(accent),
                DiscountPercent = p.DiscountPercent, DiscountCaption = p.DiscountCaption, ButtonText = p.ButtonText,
                PosterImagePath = p.PosterImagePath,
                Items = MapItems(itemsByPackage[p.Id]), Tags = MapTags(tagsByPackage[p.Id])
            });
        }
        return result;
    }

    private static List<PackageItemViewModel> MapItems(IEnumerable<ItemRow> rows)
    {
        var list = new List<PackageItemViewModel>();
        var n = 1;
        foreach (var i in rows)
        {
            list.Add(new PackageItemViewModel
            {
                Number = n++,
                Title = i.Title,
                IconBiClass = PackageIconCatalog.Resolve(i.IconKey).BiClass,
                ColorHex = PackageStyleGuard.IsValidHex(i.ColorHex) ? i.ColorHex : null,
                ScheduleTitle = string.IsNullOrWhiteSpace(i.ScheduleTitle) ? i.Title : i.ScheduleTitle!,
                ScheduleText = i.ScheduleText,
                ScheduleNote = i.ScheduleNote
            });
        }
        return list;
    }

    private static List<PackageTagViewModel> MapTags(IEnumerable<TagRow> rows)
    {
        var list = new List<PackageTagViewModel>();
        foreach (var t in rows)
        {
            list.Add(new PackageTagViewModel
            {
                Text = t.Text,
                IconBiClass = PackageIconCatalog.IsValidKey(t.IconKey) ? PackageIconCatalog.Resolve(t.IconKey).BiClass : null
            });
        }
        return list;
    }

    /// <summary>حماية إضافية: لون غير صالح في القاعدة لا يصل إلى style.</summary>
    private static string SafeAccent(string? hex) => PackageStyleGuard.IsValidHex(hex) ? hex! : "#14A8AE";

    private sealed class PackageRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string GradeLabel { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string AccentColor { get; set; } = string.Empty;
        public byte? DiscountPercent { get; set; }
        public string? DiscountCaption { get; set; }
        public string ButtonText { get; set; } = string.Empty;
        public string? PosterImagePath { get; set; }
    }

    private sealed class ItemRow
    {
        public int PackageId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public string? ScheduleTitle { get; set; }
        public string? ScheduleText { get; set; }
        public string? ScheduleNote { get; set; }
    }

    private sealed class TagRow
    {
        public int PackageId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? IconKey { get; set; }
    }
}
