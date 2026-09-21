using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Data.Seeders
{
    public static class AdminPermissionProfileSeeder
    {
        public static void Seed(ApplicationDbContext context)
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
                Description = "إدخال الأسئلة فقط",
                IsSystemProfile = false
            },
            new AdminProfile
            {
                Name = "Reviewer",
                Description = "مراجعة واعتماد الأسئلة",
                IsSystemProfile = false
            }
        };

            context.AdminProfiles.AddRange(profiles);
            context.SaveChanges();
        }
    }
}
