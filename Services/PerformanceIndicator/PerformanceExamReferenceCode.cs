namespace QdratNew.Services.PerformanceIndicator
{
    // APD-S4.1: رمز مرجعي من 6 أرقام لا يتكرر بين اختبارات مؤشر الأداء، ولكل نسخة مدرسة رمزها.
    public static class PerformanceExamReferenceCode
    {
        public const int MaxAttempts = 50;

        public static async Task<string> GenerateAsync(Func<string, Task<bool>> exists, Func<int>? next = null)
        {
            ArgumentNullException.ThrowIfNull(exists);
            next ??= () => Random.Shared.Next(100000, 1000000);

            for (var i = 0; i < MaxAttempts; i++)
            {
                var code = next().ToString();
                if (!await exists(code))
                    return code;
            }

            throw new InvalidOperationException("تعذر توليد رمز مرجعي فريد لاختبار مؤشر الأداء.");
        }
    }
}
