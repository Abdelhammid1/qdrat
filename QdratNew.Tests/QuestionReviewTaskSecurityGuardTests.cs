using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Areas.Instructors.Controllers;
using QdratNew.Security;
using QdratNew.Services.QuestionReviewTasks;
using Xunit;

namespace QdratNew.Tests
{
    // QRT-S8.1 — حارس أمني آلي لكل Endpoints مهام مراجعة الأسئلة (القائمة 12.1).
    // لا يحتاج قاعدة بيانات: يفحص بنية الكنترولرات فيمنع أي تراجع مستقبلي
    // (POST بلا Antiforgery، أكشن بلا صلاحية، قبول Entity مباشرة، قبول instructorId من العميل).
    public class QuestionReviewTaskSecurityGuardTests
    {
        private static readonly Type[] Controllers =
        {
            typeof(Areas.Admin.Controllers.QuestionReviewTasksController),
            typeof(Areas.Instructors.Controllers.QuestionReviewTasksController)
        };

        private static IEnumerable<MethodInfo> ActionsOf(Type controller)
            => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

        private static bool IsPost(MethodInfo m) => m.GetCustomAttribute<HttpPostAttribute>() is not null;

        [Fact]
        public void BothControllers_ExposeActions()
        {
            foreach (var c in Controllers)
                Assert.NotEmpty(ActionsOf(c));
        }

        [Fact]
        public void EveryPostAction_HasValidateAntiForgeryToken()
        {
            var missing = Controllers.SelectMany(ActionsOf)
                .Where(IsPost)
                .Where(m => m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is null)
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .ToList();

            Assert.True(missing.Count == 0, "POST بلا ValidateAntiForgeryToken: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryAction_HasExplicitHttpVerb_SoNoStateChangeIsReachableByGet()
        {
            var missing = Controllers.SelectMany(ActionsOf)
                .Where(m => m.GetCustomAttribute<HttpGetAttribute>() is null && !IsPost(m))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .ToList();

            Assert.True(missing.Count == 0, "أكشن بلا فعل HTTP صريح: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryAdminAction_RequiresQuestionReviewTasksPermission()
        {
            var bad = ActionsOf(typeof(Areas.Admin.Controllers.QuestionReviewTasksController))
                .Where(m =>
                {
                    var perm = m.GetCustomAttribute<AdminPermissionAttribute>();
                    return perm is null || perm.Policy is null || !perm.Policy.StartsWith("QuestionReviewTasks:", StringComparison.Ordinal);
                })
                .Select(m => m.Name)
                .ToList();

            Assert.True(bad.Count == 0, "أكشن أدمن بلا صلاحية QuestionReviewTasks: " + string.Join(", ", bad));
        }

        [Fact]
        public void EveryAdminMutation_RequiresCreateOrManage_NotRead()
        {
            var readOnlyActions = new HashSet<string>
            {
                "Index", "LoadTasksData", "Details", "LoadItemsData", "Reviewers"
            };

            var weak = ActionsOf(typeof(Areas.Admin.Controllers.QuestionReviewTasksController))
                .Where(m => !readOnlyActions.Contains(m.Name))
                .Where(m => m.GetCustomAttribute<AdminPermissionAttribute>()!.Policy == "QuestionReviewTasks:Read")
                .Select(m => m.Name)
                .ToList();

            Assert.True(weak.Count == 0, "أكشن يغيّر/يقرأ بيانات إدارية بصلاحية Read فقط: " + string.Join(", ", weak));
        }

        [Fact]
        public void InstructorController_IsProtectedByRoleAuthorize_ViaBaseClass()
        {
            var type = typeof(Areas.Instructors.Controllers.QuestionReviewTasksController);
            var auth = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();

            Assert.NotEmpty(auth);
            Assert.All(auth, a => Assert.False(string.IsNullOrWhiteSpace(a.Roles) && string.IsNullOrWhiteSpace(a.Policy)));
            Assert.True(typeof(BaseInstructorController).IsAssignableFrom(type));
        }

        [Fact]
        public void InstructorActions_NeverAcceptInstructorIdFromClient()
        {
            var offenders = ActionsOf(typeof(Areas.Instructors.Controllers.QuestionReviewTasksController))
                .SelectMany(m => m.GetParameters().Select(p => (Action: m.Name, p.Name)))
                .Where(x => x.Name!.Contains("instructor", StringComparison.OrdinalIgnoreCase))
                .Select(x => $"{x.Action}({x.Name})")
                .ToList();

            Assert.True(offenders.Count == 0, "المدرب يجب أن يُستمد من الجلسة (D6): " + string.Join(", ", offenders));
        }

        [Fact]
        public void NoAction_BindsEntityDirectly_OverpostingGuard()
        {
            var offenders = Controllers.SelectMany(ActionsOf)
                .SelectMany(m => m.GetParameters().Select(p => (Action: $"{m.DeclaringType!.Name}.{m.Name}", p)))
                .Where(x => x.p.ParameterType.Namespace == "QdratNew.Entities")
                .Select(x => $"{x.Action}({x.p.ParameterType.Name} {x.p.Name})")
                .ToList();

            Assert.True(offenders.Count == 0, "أكشن يقبل Entity مباشرة: " + string.Join(", ", offenders));
        }

        // Program.cs يضبط PropertyNamingPolicy = null عالميًا؛ واجهات QRT تقرأ camelCase، وغيابه يُفرغ الجداول (اكتُشف في E2E)
        [Fact]
        public void QrtJsonOptions_SerializeDtosAsCamelCase()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                new EligibleInstructorDto(7, "أ. محمد", 3), QuestionReviewTaskJson.Options);

            Assert.Contains("\"id\":7", json);
            Assert.Contains("\"fullName\"", json);
            Assert.Contains("\"activeLockedItems\":3", json);
        }
    }
}
