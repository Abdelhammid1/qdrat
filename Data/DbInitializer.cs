using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QdratNew.Data.Seeders;
using QdratNew.Entities;

namespace QdratNew.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            string[] roles = new[]
            {
            "Owner", "Partner", "SuperAdmin", "Admin",
            "Employee", "DataEntry", "Developer", "Student", "Instructor"
        };

            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        var result = await roleManager.CreateAsync(new IdentityRole(role));
                        if (result.Succeeded)
                        {
                            logger.LogInformation($"✅ Role '{role}' created successfully.");
                        }
                        else
                        {
                            logger.LogWarning($"⚠️ Failed to create role '{role}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                        }
                    }
                    else
                    {
                        logger.LogInformation($"ℹ️ Role '{role}' already exists.");
                    }
                }
            }
        }

        public static void SeedSystemSettings(ApplicationDbContext context)
        {
            SystemSettingsSeeder.Seed(context);
            NotificationSeeder.Seed(context);
        }

    }

}
