namespace QdratNew.ViewModels.Frontend.Packages;

/// <summary>دورة مشمولة في الباقة — للعرض العام فقط.</summary>
public class PackageItemViewModel
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string IconBiClass { get; set; } = "bi bi-book";
    /// <summary>#RRGGBB موثّق مسبقًا أو null.</summary>
    public string? ColorHex { get; set; }
    /// <summary>لون نص مقروء (WCAG) فوق ColorHex — #FFFFFF أو #1F2937.</summary>
    public string OnColorHex { get; set; } = "#FFFFFF";
    public string ScheduleTitle { get; set; } = string.Empty;
    public string? ScheduleText { get; set; }
    public string? ScheduleNote { get; set; }
}

public class PackageTagViewModel
{
    public string Text { get; set; } = string.Empty;
    public string? IconBiClass { get; set; }
}

/// <summary>بطاقة الباقة (الرئيسية و/packages).</summary>
public class PackageCardViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string GradeLabel { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string AccentColor { get; set; } = "#14A8AE";
    public string OnAccentColor { get; set; } = "#FFFFFF";
    public byte? DiscountPercent { get; set; }
    public string? DiscountCaption { get; set; }
    public string ButtonText { get; set; } = string.Empty;
    public string? PosterImagePath { get; set; }
    public List<PackageItemViewModel> Items { get; set; } = new();
    public List<PackageTagViewModel> Tags { get; set; } = new();
}

/// <summary>صفحة تفاصيل الباقة.</summary>
public class PackageDetailsViewModel : PackageCardViewModel
{
    public string? IntroText { get; set; }
}
