using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.CoursePackages;

/// <summary>فلاتر صندوق طلبات الباقات (تأتي من الـ Query String).</summary>
public class CoursePackageRegistrationFilter
{
    public CoursePackageRegistrationStatus? Status { get; set; }
    public int? PackageId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
}

public class CoursePackageRegistrationCountsViewModel
{
    public int TotalCount { get; set; }
    public int NewCount { get; set; }
    public int ContactedCount { get; set; }
    public int NeedFollowUpCount { get; set; }
    public int RejectedCount { get; set; }
    public int ConvertedCount { get; set; }
}

public class CoursePackageOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CoursePackageRegistrationRowViewModel
{
    public int Id { get; set; }
    public string PackageNameSnapshot { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public CoursePackageRegistrationStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }

    /// <summary>05xxxxxxxx → 9665xxxxxxxx لروابط واتساب.</summary>
    public string WhatsAppNumber => PhoneNumber.Length == 10 && PhoneNumber.StartsWith("05")
        ? "966" + PhoneNumber[1..]
        : PhoneNumber;
}

public class CoursePackageRegistrationsIndexViewModel
{
    public CoursePackageRegistrationFilter Filter { get; set; } = new();
    public CoursePackageRegistrationCountsViewModel Counts { get; set; } = new();
    public List<CoursePackageOptionViewModel> Packages { get; set; } = new();
    public List<CoursePackageRegistrationRowViewModel> Rows { get; set; } = new();

    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public class CoursePackageRegistrationDetailsViewModel
{
    public int Id { get; set; }
    public int CoursePackageId { get; set; }
    public string PackageNameSnapshot { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ContactedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    public CoursePackageRegistrationStatus Status { get; set; }

    [StringLength(1000, ErrorMessage = "الملاحظات حتى 1000 حرف")]
    public string? AdminNotes { get; set; }

    public string WhatsAppNumber => PhoneNumber.Length == 10 && PhoneNumber.StartsWith("05")
        ? "966" + PhoneNumber[1..]
        : PhoneNumber;
}

/// <summary>ما يقبله الأدمن في التعديل فقط (حماية من Over-posting).</summary>
public class UpdateCoursePackageRegistrationRequest
{
    [Required] public int Id { get; set; }
    [Required] public CoursePackageRegistrationStatus Status { get; set; }
    [StringLength(1000, ErrorMessage = "الملاحظات حتى 1000 حرف")] public string? AdminNotes { get; set; }
}
