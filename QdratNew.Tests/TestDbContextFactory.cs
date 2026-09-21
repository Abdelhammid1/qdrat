using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Tests
{
    /// <summary>
    /// تنفيذ اختباري لـ IDbContextFactory&lt;ApplicationDbContext&gt; يستخدم قاعدة بيانات
    /// InMemory فريدة لكل اختبار (اسم عشوائي)، بدلاً من SQL Server الحقيقي.
    /// </summary>
    public class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(string databaseName)
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
        }

        public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(_options);

        public static ApplicationDbContext CreateFreshContext(out DbContextOptions<ApplicationDbContext> options)
        {
            options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}
