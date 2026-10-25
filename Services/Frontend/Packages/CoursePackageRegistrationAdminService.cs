using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewComponents;
using QdratNew.ViewModels.Admin.CoursePackages;

namespace QdratNew.Services.Frontend.Packages;

public class CoursePackageRegistrationAdminService : ICoursePackageRegistrationAdminService
{
    public const int PageSize = 20;

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public CoursePackageRegistrationAdminService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<CoursePackageRegistrationsIndexViewModel> GetIndexAsync(
        CoursePackageRegistrationFilter filter, CancellationToken ct = default)
    {
        var query = _context.CoursePackageRegistrations.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue && Enum.IsDefined(typeof(CoursePackageRegistrationStatus), filter.Status.Value))
            query = query.Where(r => r.Status == filter.Status.Value);
        else
            filter.Status = null;

        if (filter.PackageId.HasValue)
            query = query.Where(r => r.CoursePackageId == filter.PackageId.Value);

        var search = (filter.Search ?? string.Empty).Trim();
        if (search.Length > 100) search = search[..100];
        filter.Search = search.Length == 0 ? null : search;
        if (search.Length > 0)
        {
            var digits = NumberHelper.NormalizeDigits(search);
            query = query.Where(r => r.FullName.Contains(search) || r.PhoneNumber.Contains(digits));
        }

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        filter.Page = Math.Min(Math.Max(filter.Page, 1), totalPages);

        var rows = await query
            .OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id)
            .Skip((filter.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(r => new CoursePackageRegistrationRowViewModel
            {
                Id = r.Id,
                PackageNameSnapshot = r.PackageNameSnapshot,
                FullName = r.FullName,
                PhoneNumber = r.PhoneNumber,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt
            })
            .ToListAsync(ct);

        var packages = await _context.CoursePackages
            .AsNoTracking()
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .Select(p => new CoursePackageOptionViewModel { Id = p.Id, Name = p.Name })
            .ToListAsync(ct);

        return new CoursePackageRegistrationsIndexViewModel
        {
            Filter = filter,
            Counts = await GetCountsAsync(ct),
            Packages = packages,
            Rows = rows,
            PageSize = PageSize,
            TotalItems = total
        };
    }

    public Task<CoursePackageRegistrationDetailsViewModel?> GetDetailsAsync(int id, CancellationToken ct = default)
        => _context.CoursePackageRegistrations
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new CoursePackageRegistrationDetailsViewModel
            {
                Id = r.Id,
                CoursePackageId = r.CoursePackageId,
                PackageNameSnapshot = r.PackageNameSnapshot,
                FullName = r.FullName,
                PhoneNumber = r.PhoneNumber,
                IpAddress = r.IpAddress,
                SubmittedAt = r.SubmittedAt,
                ContactedAt = r.ContactedAt,
                LastUpdatedAt = r.LastUpdatedAt,
                Status = r.Status,
                AdminNotes = r.AdminNotes
            })
            .FirstOrDefaultAsync(ct);

    public async Task<bool> UpdateAsync(UpdateCoursePackageRegistrationRequest request, string? userId, CancellationToken ct = default)
    {
        var reg = await _context.CoursePackageRegistrations.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (reg == null) return false;

        var now = DateTime.UtcNow;

        // أول انتقال إلى Contacted/Converted يسجّل وقت التواصل ومن قام به
        if ((request.Status == CoursePackageRegistrationStatus.Contacted
             || request.Status == CoursePackageRegistrationStatus.Converted) && reg.ContactedAt == null)
        {
            reg.ContactedAt = now;
            reg.HandledByUserId = userId;
        }

        reg.Status = request.Status;
        reg.AdminNotes = string.IsNullOrWhiteSpace(request.AdminNotes) ? null : request.AdminNotes.Trim();
        reg.LastUpdatedAt = now;

        await _context.SaveChangesAsync(ct);
        _cache.Remove(NewPackageRegistrationsBadgeViewComponent.CacheKey);
        return true;
    }

    private async Task<CoursePackageRegistrationCountsViewModel> GetCountsAsync(CancellationToken ct)
    {
        var grouped = await _context.CoursePackageRegistrations
            .AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Of(CoursePackageRegistrationStatus s) => grouped.Where(g => g.Status == s).Select(g => g.Count).FirstOrDefault();

        return new CoursePackageRegistrationCountsViewModel
        {
            TotalCount = grouped.Sum(g => g.Count),
            NewCount = Of(CoursePackageRegistrationStatus.New),
            ContactedCount = Of(CoursePackageRegistrationStatus.Contacted),
            NeedFollowUpCount = Of(CoursePackageRegistrationStatus.NeedFollowUp),
            RejectedCount = Of(CoursePackageRegistrationStatus.Rejected),
            ConvertedCount = Of(CoursePackageRegistrationStatus.Converted)
        };
    }
}
