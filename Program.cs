using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.FileProviders;
using QdratNew.AI.Trainers;
using QdratNew.Analytics;
using QdratNew.Data;
using QdratNew.Data.Seeders;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Filters;
using QdratNew.Implementations;
using QdratNew.Interfaces;
using QdratNew.Jobs;
using QdratNew.Modules.QuestionBank.Insights.Contracts;
using QdratNew.Modules.QuestionBank.Insights.Services;
using QdratNew.Modules.QuestionBank.Read.Contracts;
using QdratNew.Modules.QuestionBank.Read.Services;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services;
using QdratNew.Services.Analytics;
using QdratNew.Services.AI;
using QdratNew.Services.Analytics.Implementation;
using QdratNew.Services.Analytics.Interfaces;
using QdratNew.Services.Frontend.CourseCollections;
using QdratNew.Services.Frontend.ProfessionalCertificates;
using QdratNew.Services.Batches;
using QdratNew.Services.Common;
using QdratNew.Services.DecisionLab;
using QdratNew.Services.Exams;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Exams.Core;
using QdratNew.Services.Exams.Engines;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Exams.Helpers;
using QdratNew.Services.Exams.Implementations;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Readers;
using QdratNew.Services.Homework.Implementations;
using QdratNew.Services.Homework.Interfaces;
using QdratNew.Services.HomeworkAnalytics;
using QdratNew.Services.HomeworkEngine.Assignment;
using QdratNew.Services.HomeworkEngine.Drafts;
using QdratNew.Services.HomeworkEngine.Instructor;
using QdratNew.Services.Implementations;
using QdratNew.Services.Implementations.Exams;
using QdratNew.Services.Implementations.Remedial;
using QdratNew.Services.Instructors.Exams;
using QdratNew.Services.Instructors.Exams.Interfaces;
using QdratNew.Services.Instructors.Implementations;
using QdratNew.Services.Admin;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Interfaces.Exams;
using QdratNew.Services.Media;
using QdratNew.Services.Notifications;
using QdratNew.Services.Partner;
using QdratNew.Services.Partner.Dashboard;
using QdratNew.Services.Partner.Implementation;
using QdratNew.Services.Partner.Interfaces;
using QdratNew.Services.Reports.Implementations;
using QdratNew.Services.Reports.Interfaces;
using QdratNew.Services.StudentAnalysis;
using QdratNew.Services.StudentProgress;
using QdratNew.Services.Students;
using QdratNew.Services.Students.Abstractions;
using QdratNew.Services.Students.Exams.Abstractions;
using QdratNew.Services.Students.Exams.Implementations;
using QuestPDF.Infrastructure;
using Rotativa.AspNetCore;
using Serilog;
using System.Runtime.InteropServices;


var builder = WebApplication.CreateBuilder(args);
// ✅ تفعيل ترخيص QuestPDF المجاني
QuestPDF.Settings.License = LicenseType.Community;
#region 🧩 تحميل الإعدادات حسب البيئة
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();
#endregion

#region 🟢 إعداد الاتصال بقاعدة البيانات

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ✅ تسجيل الـ DbContext العادي للكنترولرات القديمة
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
                  .UseCompatibilityLevel(120)
                  .EnableRetryOnFailure()
                  .CommandTimeout(180)
        // ⚠️ MaxBatchSize(1) تم حذفه — كان يُبطئ كل عمليات الكتابة
        // إذا ظهرت مشكلة WITH بعد الحذف، حددها بالاستعلام المسبب وليس بتعطيل الـ batching كاملاً
    ));


// ✅ إنشاء Factory آمن للكنترولرات الجديدة (مثل StudentHomeworkDashboardController)
builder.Services.AddScoped<IDbContextFactory<ApplicationDbContext>>(sp =>
{
    var options = sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
    return new PooledDbContextFactory<ApplicationDbContext>(options);
});

#endregion

#region 🧠 الخدمات العامة
builder.Services.AddTransient<IEmailSender, DummyEmailSender>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHttpClient<IAIClientService, AIClientService>();
builder.Services.AddScoped<IStudentPerformanceAnalysisService, StudentPerformanceAnalysisService>();
builder.Services.AddScoped<IStudentProgressService, StudentProgressService>();



var mvcBuilder = builder.Services
    .AddControllersWithViews()
    .AddSessionStateTempDataProvider()
    .AddJsonOptions(options =>
    {
        // 🔹 يمنع تحويل أسماء الخصائص إلى small case
        // حتى تبقى كما هي (Correct, Wrong, Skipped)
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");
builder.Services.AddMemoryCache();

// ⚡ ضغط الاستجابات (Brotli/Gzip) — يقلل حجم الصفحات المُرسَلة على الجوال
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});

// ⚡ Output Caching للصفحات العامة شبه الثابتة (Areas/Public)
builder.Services.AddOutputCache();

// الهوية
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
    // 🔹 عرض البيانات
    //options.AddPolicy("ViewPolicy", policy =>
    //    policy.RequireClaim("Permission", "CanView"));

    // 🔹 إضافة البيانات
    //options.AddPolicy("CreatePolicy", policy =>
    //    policy.RequireClaim("Permission", "CanCreate"));


 


    // 🔹 تعديل البيانات
    options.AddPolicy("EditPolicy", policy =>
        policy.RequireClaim("Permission", "CanEdit"));

    // 🔹 حذف البيانات
    options.AddPolicy("DeletePolicy", policy =>
        policy.RequireClaim("Permission", "CanDelete"));

    // 🔹 طباعة التقارير
    options.AddPolicy("PrintReportsPolicy", policy =>
        policy.RequireClaim("Permission", "CanPrintReports"));

    // 🔹 دخول لوحة الأدمن
    options.AddPolicy("AdminArea", policy =>
        policy.RequireRole(
            "SuperAdmin",
            "Owner",
            "Admin",
            "Developer",
            "Employee",
            "DataEntry"
        ));

    // ✅ دخول Area الشريك (Role فقط – بدون Claims)
    options.AddPolicy("PartnerOnly", policy =>
    {
        policy.RequireRole("Partner", "PartnerAdmin", "PartnerInstructor");
    });

    // ✅ دخول Area ولي الأمر
    options.AddPolicy("ParentArea", policy =>
    {
        policy.RequireRole("Parent");
    });


    // ===============================
    // 🧪 Admin Permission System (STABLE)
    // ===============================
    options.AddPolicy(
      AdminPermissionPolicies.Questions_Read,
      policy => policy.Requirements.Add(
          new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Questions_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.Questions_Add,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Questions_Add)));

    options.AddPolicy(
        AdminPermissionPolicies.Questions_Edit,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Questions_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.Questions_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Questions_Delete)));

    options.AddPolicy(
        AdminPermissionPolicies.Questions_Approve,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Questions_Approve)));

    options.AddPolicy(
        AdminPermissionPolicies.Questions_Reject,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Questions_Reject)));

    options.AddPolicy(
    AdminPermissionPolicies.Questions_ViewAudit,
    policy => policy.Requirements.Add(
        new AdminPermissionAuthorizationRequirement(
            AdminPermissionPolicies.Questions_ViewAudit)));



    // ===============================
    // 🧪 Placement Exams (Level Assessment)
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.PlacementExams_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.PlacementExams_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.PlacementExams_Create,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.PlacementExams_Create)));

    options.AddPolicy(
        AdminPermissionPolicies.PlacementExams_Results,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.PlacementExams_Results)));

    options.AddPolicy(
        AdminPermissionPolicies.PlacementExams_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.PlacementExams_Delete)));

    options.AddPolicy(
        AdminPermissionPolicies.PlacementExams_Archive,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.PlacementExams_Archive)));


    // ===============================
    // 🧪 Homework & Lesson Completion
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.AdminLessonCompletions_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.AdminLessonCompletions_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.AdminLessonCompletions_GenerateHomework,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.AdminLessonCompletions_GenerateHomework)));

    options.AddPolicy(
        AdminPermissionPolicies.AdminLessonCompletions_ManageModelHomework,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.AdminLessonCompletions_ManageModelHomework)));

    options.AddPolicy(
        AdminPermissionPolicies.AdminLessonCompletions_ManageModelExam,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.AdminLessonCompletions_ManageModelExam)));

    options.AddPolicy(
        AdminPermissionPolicies.AdminLessonCompletions_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.AdminLessonCompletions_Delete)));

    options.AddPolicy(
    AdminPermissionPolicies.Homework_Read,
    policy => policy.Requirements.Add(
        new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_Reports,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_Reports)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_ManageQuestions,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_ManageQuestions)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_AddQuestion,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_AddQuestion)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_RemoveQuestion,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_RemoveQuestion)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_Resend,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_Resend)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_Archive,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_Archive)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_Delete)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_Details,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_Details)));

    options.AddPolicy(
        AdminPermissionPolicies.Homework_Review,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Homework_Review)));

    options.AddPolicy(
    AdminPermissionPolicies.AdminLessonCompletions_Enhancement,
    policy => policy.Requirements.Add(
        new AdminPermissionAuthorizationRequirement(
            AdminPermissionPolicies.AdminLessonCompletions_Enhancement)));



    // ===============================
    // 🧪 Exams (ALL exam controllers)
    // ===============================



    options.AddPolicy(
    AdminPermissionPolicies.Exams_Read,
    policy => policy.Requirements.Add(
        new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.Exams_Details,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Details)));

    options.AddPolicy(
        AdminPermissionPolicies.Exams_Generate,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Generate)));

    options.AddPolicy(
        AdminPermissionPolicies.Exams_Edit,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.Exams_Publish,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Publish)));

    options.AddPolicy(
        AdminPermissionPolicies.Exams_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Delete)));

    options.AddPolicy(
        AdminPermissionPolicies.Exams_Results,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Results)));

    options.AddPolicy(
        AdminPermissionPolicies.Exams_Archive,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.Exams_Archive)));

    // ===============================
    // 🧪 ExamIndividualAssignments
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.ExamIndividualAssignments_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.ExamIndividualAssignments_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.ExamIndividualAssignments_Details,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.ExamIndividualAssignments_Details)));

    options.AddPolicy(
        AdminPermissionPolicies.ExamIndividualAssignments_Create,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.ExamIndividualAssignments_Create)));

    options.AddPolicy(
        AdminPermissionPolicies.ExamIndividualAssignments_Edit,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.ExamIndividualAssignments_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.ExamIndividualAssignments_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.ExamIndividualAssignments_Delete)));



    // ===============================
    // 🧪 Performance Dashboard
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.PerformanceDashboard_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceDashboard_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceDashboard_BatchDetails,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceDashboard_BatchDetails)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceDashboard_ExamReport,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceDashboard_ExamReport)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceDashboard_ExamAnalytics,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceDashboard_ExamAnalytics)));


    // ===============================
    // 🧪 Performance Exam Reports
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.PerformanceExamReports_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceExamReports_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceExamReports_StudentReport,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceExamReports_StudentReport)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceExamReports_BatchReport,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceExamReports_BatchReport)));


    // ===============================
    // 🧪 Performance Indicator Exams
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.PerformanceIndicatorExams_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceIndicatorExams_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceIndicatorExams_Add,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceIndicatorExams_Add)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceIndicatorExams_CreateFromProfessionalModel,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceIndicatorExams_CreateFromProfessionalModel)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceIndicatorExams_ConfirmSend,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceIndicatorExams_ConfirmSend)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceIndicatorExams_Results,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceIndicatorExams_Results)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceIndicatorExams_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceIndicatorExams_Delete)));
    options.AddPolicy(
    AdminPermissionPolicies.PerformanceIndicatorExams_Edit,
    policy => policy.Requirements.Add(
        new AdminPermissionAuthorizationRequirement(
            AdminPermissionPolicies.PerformanceIndicatorExams_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.PerformanceIndicatorExams_Archive,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.PerformanceIndicatorExams_Archive)));



    // ===============================
    // 🧪 Professional Models
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_CreateModel,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_CreateModel)));

    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_EditModel,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_EditModel)));

    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_DeleteModel,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_DeleteModel)));

    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_ApproveModel,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_ApproveModel)));

    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_RejectModel,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_RejectModel)));

    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_Visibility,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_Visibility)));

    options.AddPolicy(
        AdminPermissionPolicies.ProfessionalModels_Duplicate,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.ProfessionalModels_Duplicate)));



    // ===============================
    // 🧪 Attendance (Presence & Absence)
    // ===============================
    options.AddPolicy(
     AdminPermissionPolicies.Attendance_Read,
     policy => policy.Requirements.Add(
         new AdminPermissionAuthorizationRequirement(
             AdminPermissionPolicies.Attendance_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.Attendance_Mark,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Attendance_Mark)));

    options.AddPolicy(
        AdminPermissionPolicies.Attendance_Students,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Attendance_Students)));

    options.AddPolicy(
        AdminPermissionPolicies.Attendance_Archive,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Attendance_Archive)));


    options.AddPolicy(
    AdminPermissionPolicies.Settings_EditSettings,
    policy => policy.Requirements.Add(
        new AdminPermissionAuthorizationRequirement(
            AdminPermissionPolicies.Settings_EditSettings)));




    // ===============================
    // 🧪 Lectures Management
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.Lectures_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Lectures_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.Lectures_Create,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Lectures_Create)));

    options.AddPolicy(
        AdminPermissionPolicies.Lectures_Edit,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Lectures_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.Lectures_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Lectures_Delete)));



    options.AddPolicy(
    AdminPermissionPolicies.VerbalPassages_Read,
    policy => policy.Requirements.Add(
        new AdminPermissionAuthorizationRequirement(
            AdminPermissionPolicies.VerbalPassages_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.VerbalPassages_Create,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.VerbalPassages_Create)));

    options.AddPolicy(
        AdminPermissionPolicies.VerbalPassages_Edit,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.VerbalPassages_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.VerbalPassages_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.VerbalPassages_Delete)));



    // ===============================
    // 🧪 Enhancement Skills
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.EnhancementSkills_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.EnhancementSkills_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.EnhancementSkills_Create,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.EnhancementSkills_Create)));

    options.AddPolicy(
        AdminPermissionPolicies.EnhancementSkills_Edit,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.EnhancementSkills_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.EnhancementSkills_Send,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.EnhancementSkills_Send)));

    options.AddPolicy(
        AdminPermissionPolicies.EnhancementSkills_Resend,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.EnhancementSkills_Resend)));

    options.AddPolicy(
        AdminPermissionPolicies.EnhancementSkills_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.EnhancementSkills_Delete)));

    options.AddPolicy(
        AdminPermissionPolicies.EnhancementSkills_Archive,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.EnhancementSkills_Archive)));


    // ===============================
    // 🧪 Batches
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.Batches_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.Batches_Details,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Details)));

    options.AddPolicy(
        AdminPermissionPolicies.Batches_Create,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Create)));

    options.AddPolicy(
        AdminPermissionPolicies.Batches_Edit,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Edit)));

    options.AddPolicy(
        AdminPermissionPolicies.Batches_Delete,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Delete)));

    options.AddPolicy(
        AdminPermissionPolicies.Batches_Archive,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Archive)));

    options.AddPolicy(
        AdminPermissionPolicies.Batches_Students,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Students)));

    options.AddPolicy(
        AdminPermissionPolicies.Batches_Send,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.Batches_Send)));

    // ===============================
    // 🛡️ IntegrityViolations (Translation Guard — QG-E)
    // ===============================
    options.AddPolicy(
        AdminPermissionPolicies.IntegrityViolations_Read,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.IntegrityViolations_Read)));

    options.AddPolicy(
        AdminPermissionPolicies.IntegrityViolations_Resolve,
        policy => policy.Requirements.Add(
            new AdminPermissionAuthorizationRequirement(
                AdminPermissionPolicies.IntegrityViolations_Resolve)));


});


// مسار تسجيل الدخول الجديد
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "/LMS/login");
});

// ملفات تعريف الارتباط (الكوكيز)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/LMS/login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// الجلسات
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(6);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
#endregion

#region 🧩 تسجيل الخدمات الداخلية (DI)

// ── خدمة التخفي (Impersonation) ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<QdratNew.Services.ImpersonationService>();

builder.Services.AddScoped<IDropdownService, DropdownService>();
builder.Services.AddScoped<IStudentActivityLogger, StudentActivityLogger>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IAdvancedNotificationService, AdvancedNotificationService>();
builder.Services.AddScoped<IBatchLectureAutoGenerationService, BatchLectureAutoGenerationService>();
builder.Services.AddScoped<IAutoExamGenerationService, AutoExamGenerationService>();
builder.Services.AddScoped<IStudentAnalyticsService, StudentAnalyticsService>();
builder.Services.AddScoped<IStudentRankingService, StudentRankingService>();
builder.Services.AddScoped<IStudentPracticeService, StudentPracticeService>();
builder.Services.AddScoped<IEnhancementSkillService, EnhancementSkillService>();
builder.Services.AddScoped<IStandardExamGeneratorService, StandardExamGeneratorService>();
builder.Services.AddScoped<IPlacementExamGeneratorService, PlacementExamGeneratorService>();
builder.Services.AddScoped<ITimeZoneService, TimeZoneService>();
builder.Services.AddScoped<IStudentExamStatisticsService, StudentExamStatisticsService>();
builder.Services.AddScoped<IExamRecommendationService, ExamRecommendationService>();
builder.Services.AddScoped<StudentAnalyticsHelper>();
builder.Services.AddScoped<IStudentHomeworkAnalyticsService, StudentHomeworkAnalyticsService>();
builder.Services.AddScoped<IHomeworkRecommendationService, HomeworkRecommendationService>();
builder.Services.AddScoped<IBatchPerformanceRecommendationService, BatchPerformanceRecommendationService>();
builder.Services.AddScoped<QdratNew.Services.Exams.Abstractions.IStudentExamDashboardService, QdratNew.Services.Exams.Implementations.StudentExamDashboardService>();
builder.Services.AddScoped<RemedialPlanService>();
builder.Services.AddScoped<ISectionExamGeneratorService, SectionExamGeneratorService>();
builder.Services.AddScoped<ICourseExamGeneratorService, CourseExamGeneratorService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<IAdminActivityLogger, AdminActivityLogger>();
builder.Services.AddScoped<INotificationCenterService, NotificationCenterService>();
builder.Services.AddScoped<HomeworkReminderJob>();
builder.Services.AddScoped<AutoCloseLecturesJob>();
builder.Services.AddScoped<IStudentActivityAIAnalyzer, StudentActivityAIAnalyzer>();
builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
builder.Services.AddScoped<IChartAIAnalyzer, ChartAIAnalyzer>();
builder.Services.AddScoped<IStudentPerformanceService, StudentPerformanceService>();
builder.Services.AddScoped<IHomeworkAssignmentService, HomeworkAssignmentService>();
builder.Services.AddScoped<ILessonCompletionService, LessonCompletionService>();
builder.Services.AddScoped<IStudentSectionExamService, StudentSectionExamService>();
builder.Services.AddScoped<IIntegrityGuardService, IntegrityGuardService>();
builder.Services.AddScoped<IHomeworkManagementService, HomeworkManagementService>();
builder.Services.AddScoped<IExamQuestionSelectorService, ExamQuestionSelectorService>();
builder.Services.AddScoped<QdratNew.Services.Exams.Generators.ICurriculumExamGeneratorService, QdratNew.Services.Exams.Generators.CurriculumExamGeneratorService>();
builder.Services.AddScoped<IAIStudentProgressTrainingService, AIStudentProgressTrainingService>();
builder.Services.AddScoped<AIStudentProgressTrainer>();
builder.Services.AddSingleton<AIStudentProgressPredictionService>();
builder.Services.AddSingleton(_ => new QdratNew.Services.Reports.ReportBrowserProvider(new PuppeteerSharp.BrowserFetcher()));
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IUserAccountDropdownService, UserAccountDropdownService>();
builder.Services.AddScoped<StudentPerformanceTrainer>();
builder.Services.AddScoped<IPerformanceAnalyzerService, PerformanceAnalyzerService>();
builder.Services.AddScoped<IPerformanceReportService, PerformanceReportService>();
builder.Services.AddScoped<IPerformanceDashboardService, PerformanceDashboardService>();
builder.Services.AddScoped<IPerformanceInsightService, PerformanceInsightService>();
builder.Services.AddScoped<IPerformanceIndicatorAnalysisService, PerformanceIndicatorAnalysisService>();
builder.Services.AddScoped<IRemedialSessionService, RemedialSessionService>();
builder.Services.AddScoped<IRemedialTrackingService, RemedialTrackingService>();
builder.Services.AddScoped<IRemedialReportService, RemedialReportService>();
builder.Services.AddScoped<IRemedialPlanBuilderService, RemedialPlanBuilderService>();
builder.Services.AddScoped<IPerformanceComparisonService, PerformanceComparisonService>();
builder.Services.AddScoped<IExamAttendanceTracker, ExamAttendanceTrackerService>();
builder.Services.AddScoped<IStudentDashboardService, StudentDashboardService>();
builder.Services.AddScoped<IStudentIdentityService, StudentIdentityService>();
builder.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
builder.Services.AddScoped<IPerformanceExamAutoCloseService, PerformanceExamAutoCloseService>();
builder.Services.AddScoped<IPlacementExamAutoCloseService, PlacementExamAutoCloseService>();
builder.Services.AddScoped<IStudentExamStatusService, StudentExamStatusService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<PartnerFeatureFilter>();
builder.Services.AddScoped<IUserPartnerDropdownService, UserPartnerDropdownService>();
builder.Services.AddScoped<IPartnerStudentImportService, PartnerStudentImportService>();
builder.Services.AddScoped<IPartnerSubscriptionService, PartnerSubscriptionService>();
builder.Services.AddScoped<PartnerStudentExcelTemplateService>();
builder.Services.AddScoped<
    QdratNew.Services.HomeworkDraft.Interfaces.IHomeworkDraftService,
    QdratNew.Services.HomeworkDraft.Implementations.HomeworkDraftService>();
builder.Services.AddScoped<
    QdratNew.Services.PartnerHomework.IPartnerHomeworkAssignmentService,
    QdratNew.Services.PartnerHomework.PartnerHomeworkAssignmentService>();
builder.Services.AddScoped<
    QdratNew.Services.Statistics.Interfaces.IHomeworkStatisticsService,
    QdratNew.Services.Statistics.Implementations.HomeworkStatisticsService>();
builder.Services.AddScoped<
    QdratNew.Services.HomeworkTracking.Interfaces.ISentHomeworkService,
    QdratNew.Services.HomeworkTracking.Implementations.SentHomeworkService>();


builder.Services.AddSingleton<QdratNew.Services.AdminDashboard.IAdminLiveStudentTracker, QdratNew.Services.AdminDashboard.AdminLiveStudentTracker>();

builder.Services.AddScoped<QdratNew.Services.AdminDashboard.IAdminOperationsDrillDownService, QdratNew.Services.AdminDashboard.AdminOperationsDrillDownService>();


builder.Services.AddScoped<
    QdratNew.Services.PartnerHomework.ProfessionalModels.IProfessionalModelAssignmentService,
    QdratNew.Services.PartnerHomework.ProfessionalModels.ProfessionalModelAssignmentService>();
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IPartnerExamGenerationService,
    QdratNew.Services.Exams.Implementations.PartnerExamGenerationService>();
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IPartnerExamDraftService,
    QdratNew.Services.Exams.Implementations.PartnerExamDraftService>();
// اختبار محاكاة اختبار الوزارة (Ministry Simulation Exam) — Sprint 1 (MSE-A): تسجيل أولي، التنفيذ الفعلي لاحقًا (Sprint 4/5/8/9/10)
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IMinistrySimExamGeneratorService,
    QdratNew.Services.Exams.Implementations.MinistrySimExamGeneratorService>();
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IMinistrySimExamAssignmentService,
    QdratNew.Services.Exams.Implementations.MinistrySimExamAssignmentService>();
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IMinistrySimExamAttemptService,
    QdratNew.Services.Exams.Implementations.MinistrySimExamAttemptService>();
// Sprint 14 (MSE-H / H1): استخراج حساب النتيجة/المراجعة إلى خدمة تقبل studentId صريح — يعيد استخدامها Admin Drill-down
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IMinistrySimExamResultService,
    QdratNew.Services.Exams.Implementations.MinistrySimExamResultService>();
// Sprint 18 (MSE-K / K1): استخراج منطق BatchAnalytics (MSE-I) إلى خدمة + GetBatchAverageStatsAsync لتقرير ولي الأمر
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IMinistrySimExamBatchAnalyticsService,
    QdratNew.Services.Exams.Implementations.MinistrySimExamBatchAnalyticsService>();
builder.Services.AddScoped<IInstructorExamGenerationService, InstructorExamGenerationService>();
builder.Services.AddScoped<IInstructorExamDraftService, InstructorExamDraftService>();
builder.Services.AddScoped<IInstructorExamSendService, InstructorExamSendService>();
builder.Services.AddScoped<IPassageMediaService, PassageMediaService>();
builder.Services.AddScoped<IExamResultEngine, ExamResultEngine>();
builder.Services.AddScoped<
    QdratNew.Services.Exams.Interfaces.IExamAssignmentIntegrityService,
    QdratNew.Services.Exams.Implementations.ExamAssignmentIntegrityService>();
builder.Services.AddScoped<IExamResultReader, ExamResultReader>();
builder.Services.AddScoped<ICurriculumFilterService, CurriculumFilterService>();
builder.Services.AddScoped<IStudentCourseDashboardService, StudentCourseDashboardService>();
builder.Services.AddScoped<IPartnerDashboardSnapshotService, PartnerDashboardSnapshotService>();
builder.Services.AddScoped<IPartnerDashboardDataService, PartnerDashboardDataService>();
builder.Services.AddScoped<IStudentHomeworkStatusService, StudentHomeworkStatusService>();
builder.Services.AddScoped<IInstructorScopeService, InstructorScopeService>();
builder.Services.AddScoped<ILectureInstructorSyncService, LectureInstructorSyncService>();
builder.Services.AddScoped<IEmployeeBatchAccessService, EmployeeBatchAccessService>();
builder.Services.AddScoped<QdratNew.Services.Admin.EmployeeDashboard.IEmployeeDashboardService,
                           QdratNew.Services.Admin.EmployeeDashboard.EmployeeDashboardService>();
builder.Services.AddScoped<IInstructorDashboardService, InstructorDashboardService>();
builder.Services.AddScoped<IInstructorAccessService, InstructorAccessService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IStudentCourseContext, StudentCourseContext>();
builder.Services.AddScoped<IHomeworkEngineAssignmentService, HomeworkEngineAssignmentService>();
builder.Services.AddScoped<IInstructorHomeworkEngineService, InstructorHomeworkEngineService>();
builder.Services.AddScoped<
    IHomeworkDraftEngineService,
    HomeworkDraftEngineService>();
builder.Services.AddScoped<
    IStudentExamContextService,
    StudentExamContextService>();
builder.Services.AddScoped<QdratNew.Services.Homework.Interfaces.IHomeworkAnalyticsService, QdratNew.Services.Homework.Implementations.HomeworkAnalyticsService>();
builder.Services.AddScoped<QdratNew.Services.HomeworkAnalytics.IHomeworkAnalyticsService, QdratNew.Services.HomeworkAnalytics.HomeworkAnalyticsService>();
builder.Services.AddScoped<IPartnerHomeworkInsightsService, PartnerHomeworkInsightsService>();
builder.Services.AddScoped<IPartnerExamInsightsService, PartnerExamInsightsService>();
builder.Services.AddScoped<IPartnerRiskService, PartnerRiskService>();
builder.Services.AddScoped<
    QdratNew.Modules.QuestionBank.Read.Contracts.IQuestionBankReadService,
    QdratNew.Modules.QuestionBank.Read.Services.QuestionBankReadService>();
builder.Services.AddScoped<QuestionDifficultyService>();
builder.Services.AddScoped<IExamDispatchService, ExamDispatchService>();
builder.Services.AddScoped<IExamGenerationService, ExamGenerationService>();
builder.Services.AddScoped<IRemedialEngine, RemedialEngine>();
builder.Services.AddScoped<IWeakPointEngine, WeakPointEngine>();
builder.Services.AddScoped<QdratNew.Services.AdminDashboard.IAdminOperationsDashboardService, QdratNew.Services.AdminDashboard.AdminOperationsDashboardService>();
builder.Services.AddScoped<
    QdratNew.Modules.QuestionBank.Lookups.Contracts.IQuestionBankLookupService,
    QdratNew.Modules.QuestionBank.Lookups.Services.QuestionBankLookupService>();
builder.Services.AddScoped<QuestionBankInsightsService>();
builder.Services.AddScoped<IPartnerExamGenerationService, PartnerExamGenerationService>();
builder.Services.AddScoped<IExamWriteService, ExamWriteService>();
builder.Services.AddScoped<IHomeworkWriteService, HomeworkWriteService>();
builder.Services.AddScoped<InstructorExamDraftManagementService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IQuestionPoolService, QuestionPoolService>();
builder.Services.AddScoped<IInstructorExamMonitoringService, InstructorExamMonitoringService>();
builder.Services.AddScoped<IBatchReportService, BatchReportService>();
builder.Services.AddScoped<IStudentBatchReportService, StudentBatchReportService>();
builder.Services.AddScoped<IDecisionLabAnalysisService, DecisionLabAnalysisService>();
builder.Services.AddScoped<IDecisionRecommendationService, RuleBasedDecisionRecommendationService>();
builder.Services.AddScoped<IDecisionRecommendationApprovalService, DecisionRecommendationApprovalService>();
builder.Services.AddScoped<IInterventionTaskService, InterventionTaskService>();
builder.Services.AddScoped<IMetricDecisionService, MetricDecisionService>();
builder.Services.AddScoped<IBatchDecisionStatusService, BatchDecisionStatusService>();
builder.Services.AddScoped<IAnalyticsDashboardService, AnalyticsDashboardService>();
builder.Services.AddScoped<QdratNew.Services.Analytics.IAnalyticsSectionsService, QdratNew.Services.Analytics.Implementation.AnalyticsSectionsService>();
builder.Services.AddScoped<QdratNew.Services.Analytics.Interfaces.IQuestionDifficultyRecalibrationService, QdratNew.Services.Analytics.Implementation.QuestionDifficultyRecalibrationService>();
builder.Services.AddScoped<IBatchAnalyticsService, BatchAnalyticsService>();
builder.Services.AddScoped<IInstructorAnalyticsService, InstructorAnalyticsService>();
builder.Services.AddScoped<IProfessionalCertificateService, ProfessionalCertificateService>();
builder.Services.AddScoped<ICourseCollectionService, CourseCollectionService>();




// تسجيل معالج الصلاحيات المخصص
builder.Services.AddScoped<IAuthorizationHandler, AdminPermissionAuthorizationHandler>();



builder.Services.AddScoped<PartnerPermissionService>();

builder.Services.AddScoped<IPerformanceIndicatorExamService, PerformanceIndicatorExamService>();

// ✅ Parent Portal Services
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IParentAccessService, QdratNew.Services.Parents.Implementations.ParentAccessService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IParentDashboardService, QdratNew.Services.Parents.Implementations.ParentDashboardService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IParentSafeInsightService, QdratNew.Services.Parents.Implementations.ParentSafeInsightService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IParentSmartPracticeService, QdratNew.Services.Parents.Implementations.ParentSmartPracticeService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IStudentWeaknessAnalyzerService, QdratNew.Services.Parents.Implementations.StudentWeaknessAnalyzerService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IAdaptiveQuestionPickerService, QdratNew.Services.Parents.Implementations.AdaptiveQuestionPickerService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IParentNotificationService, QdratNew.Services.Parents.Implementations.ParentNotificationService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IInstitutePlanFollowUpService, QdratNew.Services.Parents.Implementations.InstitutePlanFollowUpService>();
builder.Services.AddScoped<QdratNew.Services.Parents.Interfaces.IParentMessageService, QdratNew.Services.Parents.Implementations.ParentMessageService>();

#endregion

#region ⚙️ Hangfire
builder.Services.AddHangfire(x => x.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
#endregion

#region 📋 Serilog + PDF + Logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();
QuestPDF.Settings.License = LicenseType.Community;
#endregion






builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("http://localhost:4200", "https://qdrat.edu.sa")
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var app = builder.Build();

#region 🚀 Middleware

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    context.Database.Migrate();

    await DbInitializer.SeedRolesAsync(services);

    DbInitializer.SeedSystemSettings(context);

    if (!context.StaticPages.Any())
    {
        context.StaticPages.AddRange(
        // ضع هنا نفس عناصر StaticPages القديمة الموجودة عندك
        );

        context.SaveChanges();
    }

    AdminPermissionModuleSeeder.Seed(context);

    AdminPermissionProfileSeeder.Seed(context);

    ProfessionalCertificatesSeeder.Seed(context);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ⚡ يجب أن يسبق UseStaticFiles حتى يضغط أيضاً استجابات الملفات الثابتة
app.UseResponseCompression();

app.UseStaticFiles();

var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseMiddleware<QdratNew.Middleware.RequestTimingMiddleware>();

app.UseRouting();

app.UseCors("AllowFrontend");

app.UseSession();

app.UseAuthentication();

app.UseMiddleware<QdratNew.Middleware.StudentLiveActivityMiddleware>();

// ── التخفي: يستبدل HttpContext.User بهوية المستخدم المستهدف ──
app.UseMiddleware<QdratNew.Middleware.ImpersonationMiddleware>();

// ── منع الموقوفين: يسجّل خروج أي مستخدم IsActive=false فور أي طلب ──
app.UseMiddleware<QdratNew.Middleware.ActiveUserMiddleware>();

app.UseAuthorization();

// ⚡ بعد UseAuthorization حتى لا يُخزَّن أي رد يعتمد على هوية المستخدم بالخطأ
app.UseOutputCache();

app.UseHangfireDashboard();

#endregion



// ✅ التعامل مع الأخطاء العامة



#region ⏱️ المهام المجدولة
RecurringJob.AddOrUpdate<IAttendanceMonitoringService>(
    "Check-Missing-BatchLessons",
    service => service.CheckMissingBatchLessonCompletionsAsync(),
    "0 22 * * *");

RecurringJob.AddOrUpdate<HomeworkReminderJob>(
    "homework-reminder-job",
    job => job.RunAsync(),
    Cron.Daily);

RecurringJob.AddOrUpdate<AutoCloseLecturesJob>(
    "auto-close-lectures-job",
    job => job.RunAsync(),
    "*/10 * * * *"); // كل 10 دقائق

RecurringJob.AddOrUpdate<IPlacementExamAutoCloseService>(
    "auto-close-placement-exams-job",
    service => service.CloseExpiredExamsAsync(),
    "*/5 * * * *"); // كل 5 دقائق
#endregion




#region 🔗 التوجيه + Razor + Rotativa

app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

app.Run();

#endregion

// Sprint 9 (QG-G / G2) — يسمح بـ WebApplicationFactory<Program> من مشروع QdratNew.Tests
// لاختبارات التكامل (IntegrityGuardController / IntegrityViolationsController) بدون أي تغيير سلوكي.
public partial class Program { }
