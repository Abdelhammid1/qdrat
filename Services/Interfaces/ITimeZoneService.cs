namespace QdratNew.Services.Interfaces
{
    public interface ITimeZoneService
    {
        DateTime GetNowUtc();
        DateTime GetNowSaudi();
        DateTime ConvertToSaudi(DateTime utcTime);
        DateTime ConvertToUtc(DateTime saudiTime);
    }
}
