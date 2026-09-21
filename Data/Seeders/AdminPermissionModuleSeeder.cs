using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Data.Seeders
{
    public static class AdminPermissionModuleSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            if (context.AdminPermissionModules.Any())
                return;

            var modules = new List<AdminPermissionModule>
            {
                new AdminPermissionModule
                {
                    Key = "Users",
                    NameAr = "المستخدمون",
                    DisplayOrder = 1
                },
                new AdminPermissionModule
                {
                    Key = "Students",
                    NameAr = "الطلاب",
                    DisplayOrder = 2
                },
                new AdminPermissionModule
                {
                    Key = "Homework",
                    NameAr = "الواجبات",
                    DisplayOrder = 3
                },
                new AdminPermissionModule
                {
                    Key = "Exams",
                    NameAr = "الاختبارات",
                    DisplayOrder = 4
                },
                new AdminPermissionModule
                {
                    Key = "Reports",
                    NameAr = "التقارير",
                    DisplayOrder = 5
                },
                new AdminPermissionModule
                {
                    Key = "Partners",
                    NameAr = "الشركاء",
                    DisplayOrder = 6
                },
                new AdminPermissionModule
                {
                    Key = "Settings",
                    NameAr = "الإعدادات",
                    DisplayOrder = 7
                },
                new AdminPermissionModule
                {
                    Key = "AI",
                    NameAr = "الذكاء الاصطناعي",
                    DisplayOrder = 8
                }
            };

            context.AdminPermissionModules.AddRange(modules);
            context.SaveChanges();
        }
    }
}
