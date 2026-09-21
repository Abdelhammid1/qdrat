using QdratNew.Data;
using QdratNew.Entities;

public static class AdminProfileSeeder
{
    public static void SeedAdminProfiles(ApplicationDbContext context)
    {
        if (context.AdminProfiles.Any())
            return; // 🔐 حماية كاملة

        var profiles = new List<AdminProfile>
        {
            new AdminProfile
            {
                Name = "Super Admin",
                Description = "صلاحيات كاملة على النظام",
                IsSystemProfile = true
            },
            new AdminProfile
            {
                Name = "Data Entry",
                Description = "إدخال الأسئلة فقط بدون تعديل أو حذف",
                IsSystemProfile = false
            },
            new AdminProfile
            {
                Name = "Reviewer",
                Description = "مراجعة واعتماد المحتوى",
                IsSystemProfile = false
            }
        };

        context.AdminProfiles.AddRange(profiles);
        context.SaveChanges();
    }
}
