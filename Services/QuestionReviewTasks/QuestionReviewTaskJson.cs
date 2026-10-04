using System.Text.Json;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// خيارات JSON واجهات مهام المراجعة: Program.cs يضبط PropertyNamingPolicy = null عالميًا (Pascal)،
    /// بينما جافاسكربت QRT (DataTables والملخصات) يقرأ camelCase — فنثبّته هنا محليًا دون تغيير الإعداد العالمي.
    /// </summary>
    public static class QuestionReviewTaskJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    }
}
