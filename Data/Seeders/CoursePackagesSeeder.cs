using QdratNew.Entities.Frontend;

namespace QdratNew.Data.Seeders;

/// <summary>
/// يزرع الباقات الثلاث مرة واحدة فقط (عند خلو الجدول). الباقات تُزرع IsActive=false
/// ولا تظهر للزوار حتى يرفع الأدمن البوسترات ويفعّلها من لوحة التحكم.
/// </summary>
public static class CoursePackagesSeeder
{
    public static void Seed(ApplicationDbContext context)
    {
        if (context.CoursePackages.Any()) return;

        context.CoursePackages.AddRange(Package1(), Package2(), Package3());
        context.SaveChanges();
    }

    private static CoursePackage Package1() => new()
    {
        Name = "البكج الأول",
        Slug = "package-1",
        GradeLabel = "للصف الثالث الثانوي",
        AccentColor = "#F59E0B",
        DiscountPercent = 51,
        DiscountCaption = "على البكج الكامل",
        ButtonText = "احجز الآن",
        IsActive = false,
        ShowOnHomePage = true,
        DisplayOrder = 1,
        Items = new List<CoursePackageItem>
        {
            Item(1, "دورة القدرات التأسيسية", "bullseye", "#F97316",
                 "دورة القدرات التأسيسية", "من 8 نوفمبر إلى 3 ديسمبر 2026م."),
            Item(2, "الخطة العلاجية (عن بعد)", "bullhorn", "#84CC16",
                 "الخطة العلاجية (عن بعد)", "من 6 إلى 12 ديسمبر 2026م.", "( عبر منصة المعهد)"),
            Item(3, "دورة النماذج الاحترافية المكثفة", "trophy", "#06B6D4",
                 "النماذج الاحترافية (مكثفة ومتواصلة)", "من 13 ديسمبر إلى 1 يناير 2027م."),
            Item(4, "دورة التحصيلي", "lightbulb", "#2563EB",
                 "التحصيلي", "يبدأ في 17 يناير 2027م."),
        }
    };

    private static CoursePackage Package2() => new()
    {
        Name = "البكج الثاني",
        Slug = "package-2",
        GradeLabel = "للصف الثالث الثانوي",
        IntroText = "لطلاب الصف الثالث الثانوي المتأسسين والمسجلين في اختبار القدرات الورقي، ويحتاجون مراجعة النماذج بشكل مكثف قبل اختبارهم، ويقومون بالاستعداد للتحصيلي في نفس الوقت.",
        AccentColor = "#14A8AE",
        DiscountPercent = 57,
        DiscountCaption = "على البكج",
        ButtonText = "احجز الآن",
        IsActive = false,
        ShowOnHomePage = true,
        DisplayOrder = 2,
        Items = new List<CoursePackageItem>
        {
            Item(1, "دورة النماذج الاحترافية المكثفة", "gears", "#5B3FA0",
                 "النماذج الاحترافية (14 يوم مكثفة ومتواصلة)", "من 22 نوفمبر إلى 5 ديسمبر 2026م."),
            Item(2, "دورة التحصيلي", "lightbulb", "#BE2B55",
                 "التحصيلي", "يبدأ في 17 يناير 2027م."),
        },
        Tags = new List<CoursePackageTag>
        {
            new() { Text = "7 أيام كمي", DisplayOrder = 1 },
            new() { Text = "7 أيام لفظي", DisplayOrder = 2 },
        }
    };

    private static CoursePackage Package3() => new()
    {
        Name = "البكج الثالث",
        Slug = "package-3",
        GradeLabel = "للصف الثالث الثانوي",
        Subtitle = "التأسيس + التحصيلي",
        AccentColor = "#1B8A94",
        DiscountPercent = 57,
        DiscountCaption = "على البكج",
        ButtonText = "احجز الآن",
        IsActive = false,
        ShowOnHomePage = true,
        DisplayOrder = 3,
        Items = new List<CoursePackageItem>
        {
            Item(1, "دورة القدرات التأسيسية", "bullseye", "#0EA5E9",
                 "دورة القدرات التأسيسية", "من 8 نوفمبر إلى 3 ديسمبر 2026م."),
            Item(2, "الخطة العلاجية (عن بعد)", "bullhorn", "#6366F1",
                 "الخطة العلاجية (عن بعد)", "من 6 إلى 12 ديسمبر 2026م.", "( عبر منصة المعهد)"),
            Item(3, "دورة التحصيلي", "trophy", "#F43F5E",
                 "التحصيلي", "يبدأ في 17 يناير 2027م."),
        },
        Tags = new List<CoursePackageTag>
        {
            new() { Text = "بنين : QD-109", IconKey = "male", DisplayOrder = 1 },
            new() { Text = "بنات : QD-53", IconKey = "female", DisplayOrder = 2 },
        }
    };

    private static CoursePackageItem Item(int order, string title, string iconKey, string colorHex,
        string scheduleTitle, string scheduleText, string? scheduleNote = null) => new()
    {
        Title = title,
        IconKey = iconKey,
        ColorHex = colorHex,
        ScheduleTitle = scheduleTitle,
        ScheduleText = scheduleText,
        ScheduleNote = scheduleNote,
        DisplayOrder = order
    };
}
