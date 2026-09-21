namespace QdratNew.Services.Interfaces
{
    public interface ISystemSettingService
    {
        /// <summary>
        /// الحصول على قيمة الإعداد كنص.
        /// </summary>
        string? GetValue(string key);

        /// <summary>
        /// الحصول على قيمة رقمية من الإعداد.
        /// </summary>
        int GetInt(string key, int defaultValue = 0);

        /// <summary>
        /// الحصول على قيمة منطقية من الإعداد.
        /// </summary>
        bool GetBool(string key, bool defaultValue = false);

        /// <summary>
        /// تحديث الإعداد (مستقبليًا عند تعديل الإعدادات من لوحة التحكم).
        /// </summary>
        Task<bool> UpdateValueAsync(string key, string value);


        Task<string?> GetAsync(string key);
        Task<int> GetIntAsync(string key, int defaultValue = 0);
        Task<double> GetDoubleAsync(string key, double defaultValue = 0);
        Task<bool> GetBoolAsync(string key, bool defaultValue = false);
    }
}
