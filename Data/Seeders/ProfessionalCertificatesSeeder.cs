using QdratNew.Entities.Frontend;

namespace QdratNew.Data.Seeders;

public static class ProfessionalCertificatesSeeder
{
    public static void Seed(ApplicationDbContext context)
    {
        if (!context.ProfessionalCertificateSectionSettings.Any())
        {
            context.ProfessionalCertificateSectionSettings.Add(new ProfessionalCertificateSectionSetting
            {
                Id = 1,
                IsEnabled = true,
                Title = "الشهادات الدولية الاحترافية",
                Subtitle = "برامج متخصصة في السلامة والصحة المهنية بمعايير عالمية",
                Description = "اختر البرنامج المناسب لك وسجّل بياناتك، وسيقوم فريق الدعم الفني بالتواصل معك لاستكمال تفاصيل الاشتراك والاعتماد.",
                ButtonText = "سجل اهتمامك",
                AutoCloseWhenMaxReached = false
            });
        }

        if (!context.ProfessionalCertificateCourses.Any())
        {
            context.ProfessionalCertificateCourses.AddRange(
                new ProfessionalCertificateCourse
                {
                    TitleAr = "إدارة السلامة والصحة المهنية بمعايير أوشا الدولية",
                    StandardCode = "OSHA-CFR-1910",
                    Slug = "osha-cfr-1910",
                    ShortDescription = "برنامج مهني متخصص في أساسيات ومعايير إدارة السلامة والصحة المهنية وفق متطلبات أوشا الدولية.",
                    IconClass = "fas fa-shield-halved",
                    BadgeText = "اعتماد دولي",
                    IsActive = true, ShowOnHomePage = true, IsRegistrationOpen = true, DisplayOrder = 1
                },
                new ProfessionalCertificateCourse
                {
                    TitleAr = "نظام إنشاء إدارة السلامة والصحة المهنية",
                    StandardCode = "ISO:45001",
                    Slug = "iso-45001",
                    ShortDescription = "برنامج يركز على بناء وتطبيق نظام إدارة السلامة والصحة المهنية داخل المؤسسات وفق معيار ISO 45001.",
                    IconClass = "fas fa-certificate",
                    BadgeText = "اعتماد دولي",
                    IsActive = true, ShowOnHomePage = true, IsRegistrationOpen = true, DisplayOrder = 2
                },
                new ProfessionalCertificateCourse
                {
                    TitleAr = "إدارة الأزمات والمخاطر والكوارث",
                    StandardCode = "ISO:31000",
                    Slug = "iso-31000",
                    ShortDescription = "برنامج احترافي لفهم منهجيات إدارة المخاطر والأزمات والكوارث والتعامل معها داخل بيئات العمل.",
                    IconClass = "fas fa-triangle-exclamation",
                    BadgeText = "اعتماد دولي",
                    IsActive = true, ShowOnHomePage = true, IsRegistrationOpen = true, DisplayOrder = 3
                },
                new ProfessionalCertificateCourse
                {
                    TitleAr = "السلامة والصحة المهنية بالمدارس بمعايير أوشا الدولية",
                    StandardCode = "OSHA-CFR-1910 for School",
                    Slug = "osha-school",
                    ShortDescription = "برنامج متخصص لتطبيق متطلبات السلامة والصحة المهنية داخل المدارس والبيئات التعليمية.",
                    IconClass = "fas fa-school",
                    BadgeText = "اعتماد دولي",
                    IsActive = true, ShowOnHomePage = true, IsRegistrationOpen = true, DisplayOrder = 4
                },
                new ProfessionalCertificateCourse
                {
                    TitleAr = "صناعة الرعاية الصحية والسلامة المهنية في المستشفيات",
                    StandardCode = "OSHA-CFR-1910 for Hospital",
                    Slug = "osha-hospital",
                    ShortDescription = "برنامج متخصص في تطبيق معايير السلامة والصحة المهنية داخل المستشفيات وقطاع الرعاية الصحية.",
                    IconClass = "fas fa-hospital",
                    BadgeText = "اعتماد دولي",
                    IsActive = true, ShowOnHomePage = true, IsRegistrationOpen = true, DisplayOrder = 5
                }
            );
        }

        context.SaveChanges();
    }
}
