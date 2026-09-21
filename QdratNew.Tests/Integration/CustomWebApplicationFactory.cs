using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using QdratNew.Data;
using System.Security.Claims;

namespace QdratNew.Tests.Integration
{
    // Sprint 9 (QG-G / G2) — يُشغّل التطبيق الفعلي (Program.cs) بالكامل تحت بيئة "Testing"
    // على قاعدة بيانات SQL Server معزولة (QdratNewDB_IntegrationTests، راجع appsettings.Testing.json)
    // بدل قاعدة التطوير الحقيقية QdratNewDB مباشرة.
    //
    // ملاحظة معمارية مهمة: تبيّن أن تاريخ الـ Migrations الكامل لهذا المشروع (بعض الجداول القديمة
    // أُنشئت خارج الـ Migrations أصلاً) وكذلك الموديل الحيّ (دورة Cascade تتقاطع عند AspNetUsers)
    // لا يُنتجان مخططًا صالحًا عند البناء من الصفر (لا عبر Migrate ولا عبر EnsureCreated) — عيبان
    // موجودان مسبقًا لا علاقة لهما بميزة حماية مسارات الحل، وخارج نطاق هذا الـ Sprint تمامًا.
    // لذلك تُبنى قاعدة الاختبار المعزولة كنسخة (BACKUP/RESTORE) حقيقية من قاعدة التطوير المحلية
    // QdratNewDB نفسها (المبنية تدريجيًا عبر تاريخها الفعلي وتعمل بنجاح) — تُنسَخ مرة واحدة فقط
    // لكل جلسة اختبار (يُتخطى النسخ لو كانت قاعدة الاختبار موجودة بالفعل من تشغيل سابق)، ثم يُكتفى
    // بحذف الصفوف التي زرعتها الاختبارات نفسها بين كل اختبار وآخر بدل إعادة النسخ الكاملة.
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();

                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                });
            });
        }

        /// <summary>
        /// يضمن وجود قاعدة الاختبار المعزولة (نسخ لمرة واحدة فقط)، ثم يحذف الصفوف التي زرعتها
        /// اختبارات Translation Guard تحديدًا (بمعرّفات/أسماء مستخدمين ثابتة معروفة) استعدادًا
        /// لإعادة الزرع — بديل خفيف عن إعادة نسخ القاعدة بالكامل قبل كل اختبار.
        /// </summary>
        public async Task ResetDatabaseAsync()
        {
            await TestDatabaseBaseline.EnsureAsync();

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // StudentID عمود Identity في القاعدة الحقيقية (يتولد تلقائيًا) — التنظيف يتم عبر
            // NationalID الثابت بدل معرّف متغيّر بين كل إعادة زرع.
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM IntegrityViolationLogs WHERE StudentId IN (SELECT StudentID FROM Students WHERE NationalID = '1000000001')");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM StudentBatchEnrollments WHERE StudentID IN (SELECT StudentID FROM Students WHERE NationalID = '1000000001')");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Students WHERE NationalID = '1000000001'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Batches WHERE Name = N'دفعة اختبار التكامل'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Courses WHERE Name = N'دورة اختبار التكامل'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Branches WHERE Name = N'فرع اختبار التكامل'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM AspNetUsers WHERE UserName = 'tg-integration-student'");
        }

        /// <summary>
        /// يولّد زوج توكن antiforgery صالح (كوكي + هيدر) دون أي طلب HTTP فعلي مسبق، عبر استدعاء
        /// IAntiforgery مباشرة من حاوية DI الخاصة بمضيف الاختبار. يجب تمرير نفس قيمة هوية
        /// المستخدم (X-Test-UserId) التي سيُصادَق بها الطلب الفعلي عبر TestAuthHandler: antiforgery
        /// الافتراضي في ASP.NET Core يُضمِّن اسم المستخدم المصادَق عليه وقت توليد التوكن، ويرفض
        /// التحقق لاحقًا إن اختلف عن اسم مستخدم الطلب الفعلي الذي يُستخدَم فيه — لذا لا يصلح توليد
        /// زوج واحد "عام" يُستخدَم لأكثر من هوية.
        /// </summary>
        public (string CookieHeader, string RequestToken) CreateValidAntiForgeryTokens(string authenticatedUserId)
        {
            using var scope = Services.CreateScope();
            var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();

            // antiforgery يربط التوكن بهوية المستخدم عبر ClaimTypes.NameIdentifier (Issuer+Value)
            // إن وُجدت — وليس ClaimTypes.Name — فيجب أن تطابق تمامًا ما يضعه TestAuthHandler.
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, authenticatedUserId),
                    new Claim(ClaimTypes.Name, authenticatedUserId)
                },
                TestAuthHandler.SchemeName,
                ClaimTypes.Name,
                ClaimTypes.Role);

            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = new ClaimsPrincipal(identity)
            };
            httpContext.Response.Body = new MemoryStream();

            var tokens = antiforgery.GetAndStoreTokens(httpContext);

            var setCookie = httpContext.Response.Headers["Set-Cookie"].ToString();
            var cookieValue = setCookie.Split(';')[0]; // "<CookieName>=<Value>"

            return (cookieValue, tokens.RequestToken!);
        }
    }
}
