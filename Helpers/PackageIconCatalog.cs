namespace QdratNew.Helpers;

public sealed record PackageIcon(string Key, string Label, string FaClass, string BiClass);

public static class PackageIconCatalog
{
    public static readonly IReadOnlyList<PackageIcon> All = new[]
    {
        new PackageIcon("bullseye",  "القدرات التأسيسية",  "fas fa-bullseye",     "bi bi-bullseye"),
        new PackageIcon("bullhorn",  "الخطة العلاجية",     "fas fa-bullhorn",     "bi bi-megaphone"),
        new PackageIcon("trophy",    "التميز / الإنجاز",   "fas fa-trophy",       "bi bi-trophy"),
        new PackageIcon("lightbulb", "التحصيلي",           "fas fa-lightbulb",    "bi bi-lightbulb"),
        new PackageIcon("gears",     "النماذج المكثفة",    "fas fa-gears",        "bi bi-gear-wide-connected"),
        new PackageIcon("puzzle",    "التفكير والتحليل",   "fas fa-puzzle-piece", "bi bi-puzzle"),
        new PackageIcon("book",      "مادة علمية",         "fas fa-book",         "bi bi-book"),
        new PackageIcon("clock",     "مدة / وقت",          "fas fa-clock",        "bi bi-clock"),
        new PackageIcon("star",      "مميّز",              "fas fa-star",         "bi bi-star-fill"),
        new PackageIcon("male",      "بنين",               "fas fa-person",       "bi bi-person-fill"),
        new PackageIcon("female",    "بنات",               "fas fa-person-dress", "bi bi-person-standing-dress"),
    };

    private static readonly Dictionary<string, PackageIcon> ByKey =
        All.ToDictionary(x => x.Key, StringComparer.Ordinal);

    public static bool IsValidKey(string? key) => key != null && ByKey.ContainsKey(key);

    public static PackageIcon Resolve(string? key) =>
        key != null && ByKey.TryGetValue(key, out var icon) ? icon : ByKey["book"];
}
