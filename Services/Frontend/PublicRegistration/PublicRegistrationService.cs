using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.Frontend.Register;

namespace QdratNew.Services.Frontend.PublicRegistration;

public class PublicRegistrationService : IPublicRegistrationService
{
    private const string DefaultIcon = "fa-graduation-cap";
    private const string DefaultColor = "#1B5EAE";
    private const int MaxCoursesPerLead = 10;
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);

    private static readonly Regex IconRegex = new(@"^fa-[a-z0-9-]{1,45}$", RegexOptions.Compiled);
    private static readonly Regex ColorRegex = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^05\d{8}$", RegexOptions.Compiled);

    private static readonly HashSet<string> SchoolStages = new()
    {
        "أول ثانوي", "ثاني ثانوي", "ثالث ثانوي", "خريج"
    };

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public PublicRegistrationService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public void InvalidateCatalog() => RegisterCatalogCache.Invalidate(_cache);

    public async Task<List<RegisterProgramCardVM>> GetCatalogAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(RegisterCatalogCache.Key, out List<RegisterProgramCardVM>? cached) && cached != null)
            return cached;

        var rows = await _context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive && c.ShowOnRegisterPage
                     && c.ProjectId != null
                     && c.Project.IsActive && c.Project.ShowOnRegisterPage)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.PublicDescription,
                c.StartDate,
                c.RegisterDisplayOrder,
                ProjectId = c.ProjectId!.Value,
                ProjectName = c.Project.Name,
                ProjectDesc = c.Project.PublicDescription,
                ProjectIcon = c.Project.RegisterIcon,
                ProjectColor = c.Project.RegisterAccentColor,
                ProjectOrder = c.Project.RegisterDisplayOrder
            })
            .ToListAsync(ct);

        var catalog = rows
            .GroupBy(r => new { r.ProjectId, r.ProjectName, r.ProjectDesc, r.ProjectIcon, r.ProjectColor, r.ProjectOrder })
            .OrderBy(g => g.Key.ProjectOrder).ThenBy(g => g.Key.ProjectName)
            .Select(g => new RegisterProgramCardVM
            {
                ProjectId = g.Key.ProjectId,
                Name = g.Key.ProjectName,
                Description = g.Key.ProjectDesc,
                Icon = !string.IsNullOrWhiteSpace(g.Key.ProjectIcon) && IconRegex.IsMatch(g.Key.ProjectIcon.Trim())
                    ? g.Key.ProjectIcon.Trim() : DefaultIcon,
                AccentColor = !string.IsNullOrWhiteSpace(g.Key.ProjectColor) && ColorRegex.IsMatch(g.Key.ProjectColor.Trim())
                    ? g.Key.ProjectColor.Trim() : DefaultColor,
                Courses = g.OrderBy(c => c.RegisterDisplayOrder).ThenBy(c => c.Name)
                           .Select(c => new RegisterCourseItemVM
                           {
                               CourseId = c.Id,
                               ProjectId = c.ProjectId,
                               Name = c.Name,
                               Description = c.PublicDescription,
                               StartDate = c.StartDate
                           }).ToList()
            })
            .ToList();

        _cache.Set(RegisterCatalogCache.Key, catalog, CatalogTtl);
        return catalog;
    }

    public async Task<SubmitLeadResult> SubmitLeadAsync(RegisterLeadFormVM form, string? ip, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string>();

        var studentName = (form.StudentName ?? string.Empty).Trim();
        var phone = NumberHelper.NormalizeDigits(form.PhoneNumber);
        var parentName = string.IsNullOrWhiteSpace(form.ParentName) ? null : form.ParentName.Trim();
        var parentPhone = string.IsNullOrWhiteSpace(form.ParentPhone) ? null : NumberHelper.NormalizeDigits(form.ParentPhone);
        var notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();

        if (studentName.Length < 3) errors["StudentName"] = "اكتب الاسم بشكل صحيح";
        if (!PhoneRegex.IsMatch(phone)) errors["PhoneNumber"] = "رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام";
        if (parentPhone != null && !PhoneRegex.IsMatch(parentPhone))
            errors["ParentPhone"] = "رقم جوال ولي الأمر يجب أن يبدأ بـ 05 ويتكون من 10 أرقام";

        if (form.ApplicantType == LeadApplicantType.Parent)
        {
            if (parentName == null) errors["ParentName"] = "اسم ولي الأمر مطلوب";
            if (parentPhone == null) errors["ParentPhone"] = "رقم جوال ولي الأمر مطلوب";
        }

        // التحقق من الدورات في الذاكرة مقابل الكتالوج (SQL Server 2014: لا Contains على قوائم داخل EF)
        var catalog = await GetCatalogAsync(ct);
        var allowed = catalog
            .SelectMany(p => p.Courses.Select(c => new { Course = c, Program = p }))
            .ToDictionary(x => x.Course.CourseId);

        var picked = (form.SelectedCourseIds ?? new List<int>()).Distinct().ToList();

        if (allowed.Count == 0)
        {
            if (notes == null) errors["Notes"] = "اكتب في الملاحظات البرنامج أو الدورة التي تهمك";
        }
        else if (picked.Count == 0)
        {
            errors["SelectedCourseIds"] = "اختر دورة واحدة على الأقل";
        }
        else if (picked.Count > MaxCoursesPerLead)
        {
            errors["SelectedCourseIds"] = $"الحد الأقصى {MaxCoursesPerLead} دورات في الطلب الواحد";
        }
        else if (picked.Any(id => !allowed.ContainsKey(id)))
        {
            errors["SelectedCourseIds"] = "بعض الدورات المختارة لم تعد متاحة، حدّث الصفحة وأعد الاختيار";
        }

        if (errors.Count > 0) return new SubmitLeadResult(false, null, errors);

        // منع التكرار: نفس الرقم خلال 10 دقائق ← نجاح صامت (Idempotent)
        var cutoff = DateTime.UtcNow - DuplicateWindow;
        var duplicate = await _context.FrontendLeads
            .AsNoTracking()
            .AnyAsync(l => l.PhoneNumber == phone && l.CreatedAt >= cutoff, ct);
        if (duplicate) return new SubmitLeadResult(true, null, errors);

        var now = DateTime.UtcNow;
        var lead = new FrontendLead
        {
            StudentName = studentName,
            PhoneNumber = phone,
            ApplicantType = form.ApplicantType,
            ParentName = parentName,
            ParentPhone = parentPhone,
            Gender = form.Gender,
            SchoolStage = form.SchoolStage != null && SchoolStages.Contains(form.SchoolStage) ? form.SchoolStage : null,
            City = string.IsNullOrWhiteSpace(form.City) ? null : form.City.Trim(),
            Notes = notes,
            Status = FrontendLeadStatus.New,
            IsContacted = false,
            SourceIp = ip != null && ip.Length > 45 ? ip[..45] : ip,
            CreatedAt = now,
            SelectedProgram = BuildProgramSummary(picked, allowed.ToDictionary(k => k.Key, v => v.Value.Program)),
            SelectedCourses = picked.Select(id => new FrontendLeadCourse
            {
                CourseId = id,
                ProjectId = allowed[id].Program.ProjectId,
                CourseNameSnapshot = Truncate(allowed[id].Course.Name, 200),
                ProjectNameSnapshot = Truncate(allowed[id].Program.Name, 200),
                CreatedAt = now
            }).ToList()
        };

        _context.FrontendLeads.Add(lead);
        await _context.SaveChangesAsync(ct); // حفظ واحد للطلب والدورات

        return new SubmitLeadResult(true, lead.Id, errors);
    }

    // "القدرات (2) · التحصيلي (1)" — يُبقي التصدير القديم (SelectedProgram) صالحًا
    private static string BuildProgramSummary(List<int> picked, Dictionary<int, RegisterProgramCardVM> programByCourse)
    {
        if (picked.Count == 0) return "بدون دورة محددة";

        var summary = string.Join(" · ", picked
            .GroupBy(id => programByCourse[id].ProjectId)
            .Select(g => $"{programByCourse[g.First()].Name} ({g.Count()})"));

        return Truncate(summary, 200);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
