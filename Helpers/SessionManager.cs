using Microsoft.AspNetCore.Http;

namespace QdratNew.Helpers
{
    public static class SessionManager
    {
        public static void SetActiveRole(HttpContext context, string role)
        {
            context.Session.SetString(SessionKeys.ActiveRole, role);
        }

        public static string? GetActiveRole(HttpContext context)
        {
            return context.Session.GetString(SessionKeys.ActiveRole);
        }

        public static void SetActiveCurriculumId(HttpContext context, int curriculumId)
        {
            context.Session.SetInt32(SessionKeys.ActiveCurriculumId, curriculumId);
        }

        public static int? GetActiveCurriculumId(HttpContext context)
        {
            return context.Session.GetInt32(SessionKeys.ActiveCurriculumId);
        }
    }
}
