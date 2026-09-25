using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using QdratNew.ViewComponents;
using QdratNew.ViewModels.Admin.FrontendLeads;

namespace QdratNew.Services.Frontend.Leads;

public class FrontendLeadAdminService : IFrontendLeadAdminService
{
    // حد أمان للتحميل Client-side في DataTables — إن تجاوزت الطلبات 2000 يُنقل الجدول إلى Server-side (Tech debt)
    private const int MaxRows = 2000;
    private static readonly TimeSpan RiyadhOffset = TimeSpan.FromHours(3);

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public FrontendLeadAdminService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<FrontendLeadCountsVM> GetCountsAsync(CancellationToken ct = default)
    {
        // بداية اليوم بتوقيت الرياض (UTC+3 بلا توقيت صيفي) مُحوَّلة إلى UTC
        var todayStartUtc = (DateTime.UtcNow + RiyadhOffset).Date - RiyadhOffset;
        var weekStartUtc = todayStartUtc.AddDays(-6);

        // استعلام واحد: GroupBy على ثابت ← COUNT شرطي في SQL
        var counts = await _context.FrontendLeads
            .AsNoTracking()
            .GroupBy(l => 1)
            .Select(g => new FrontendLeadCountsVM
            {
                TotalCount = g.Count(),
                NewCount = g.Count(l => l.Status == FrontendLeadStatus.New),
                ContactedCount = g.Count(l => l.Status == FrontendLeadStatus.Contacted),
                NeedFollowUpCount = g.Count(l => l.Status == FrontendLeadStatus.NeedFollowUp),
                RejectedCount = g.Count(l => l.Status == FrontendLeadStatus.Rejected),
                ConvertedCount = g.Count(l => l.Status == FrontendLeadStatus.Converted),
                TodayCount = g.Count(l => l.CreatedAt >= todayStartUtc),
                Last7DaysCount = g.Count(l => l.CreatedAt >= weekStartUtc)
            })
            .FirstOrDefaultAsync(ct);

        return counts ?? new FrontendLeadCountsVM();
    }

    public async Task<FrontendLeadsDashboardVM> GetDashboardAsync(CancellationToken ct = default)
    {
        var counts = await GetCountsAsync(ct);

        var projectRows = await _context.FrontendLeadCourses
            .AsNoTracking()
            .Where(c => c.ProjectId != null)
            .GroupBy(c => new { c.ProjectId, c.ProjectNameSnapshot })
            .Select(g => new
            {
                g.Key.ProjectId,
                g.Key.ProjectNameSnapshot,
                Count = g.Select(x => x.FrontendLeadId).Distinct().Count()
            })
            .ToListAsync(ct);

        // نفس البرنامج قد يظهر باسمين (إعادة تسمية) ← ندمج بالمعرّف ونأخذ الاسم الأكثر استخدامًا
        var projectCounts = projectRows
            .GroupBy(r => r.ProjectId)
            .Select(g => new LeadProjectCountVM
            {
                ProjectId = g.Key,
                Name = g.OrderByDescending(x => x.Count).First().ProjectNameSnapshot ?? "برنامج",
                Count = g.Max(x => x.Count)
            })
            .OrderByDescending(p => p.Count)
            .ThenBy(p => p.Name)
            .ToList();

        var rows = await _context.FrontendLeads
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(MaxRows)
            .Select(l => new FrontendLeadRowVM
            {
                Id = l.Id,
                StudentName = l.StudentName,
                PhoneNumber = l.PhoneNumber,
                ApplicantType = l.ApplicantType,
                ParentName = l.ParentName,
                ParentPhone = l.ParentPhone,
                City = l.City,
                SchoolStage = l.SchoolStage,
                Notes = l.Notes,
                AdminNotes = l.AdminNotes,
                Status = l.Status,
                CreatedAt = l.CreatedAt,
                ContactedAt = l.ContactedAt,
                LegacyProgram = l.SelectedProgram,
                Courses = l.SelectedCourses
                    .OrderBy(c => c.Id)
                    .Select(c => new LeadCourseChipVM
                    {
                        ProjectId = c.ProjectId,
                        ProjectName = c.ProjectNameSnapshot ?? "",
                        CourseName = c.CourseNameSnapshot
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            // RL-S5.5 — طلب قديم: نص برنامج بلا دورات مرتبطة
            row.IsLegacy = row.Courses.Count == 0 && !string.IsNullOrWhiteSpace(row.LegacyProgram);
            if (!row.IsLegacy) row.LegacyProgram = null;
        }

        return new FrontendLeadsDashboardVM
        {
            Counts = counts,
            ProjectCounts = projectCounts,
            Leads = rows
        };
    }

    public async Task<UpdateLeadStatusResult> UpdateStatusAsync(UpdateLeadStatusRequest req, string? userId, CancellationToken ct = default)
    {
        var lead = await _context.FrontendLeads.FirstOrDefaultAsync(l => l.Id == req.Id, ct);
        if (lead == null) return new UpdateLeadStatusResult { Found = false };

        lead.AdminNotes = string.IsNullOrWhiteSpace(req.AdminNotes) ? null : req.AdminNotes.Trim();
        ApplyStatus(lead, req.Status, userId);

        await _context.SaveChangesAsync(ct);
        _cache.Remove(NewLeadsBadgeViewComponent.CacheKey);

        return new UpdateLeadStatusResult
        {
            Found = true,
            ContactedAt = lead.ContactedAt,
            Counts = await GetCountsAsync(ct)
        };
    }

    public async Task<bool> MarkContactedAsync(int id, string? userId, CancellationToken ct = default)
    {
        var lead = await _context.FrontendLeads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null) return false;

        ApplyStatus(lead, FrontendLeadStatus.Contacted, userId);

        await _context.SaveChangesAsync(ct);
        _cache.Remove(NewLeadsBadgeViewComponent.CacheKey);
        return true;
    }

    private static void ApplyStatus(FrontendLead lead, FrontendLeadStatus status, string? userId)
    {
        var now = DateTime.UtcNow;

        // أول انتقال إلى Contacted/Converted يسجّل وقت التواصل ومن قام به
        if ((status == FrontendLeadStatus.Contacted || status == FrontendLeadStatus.Converted) && lead.ContactedAt == null)
        {
            lead.ContactedAt = now;
            lead.ContactedByUserId = userId;
        }

        lead.Status = status;
        lead.IsContacted = status != FrontendLeadStatus.New; // توافق مع الشارة والتصدير القديم
        lead.LastUpdatedAt = now;
    }
}
