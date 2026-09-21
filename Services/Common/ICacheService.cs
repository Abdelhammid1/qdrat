namespace QdratNew.Services.Common
{
    public interface ICacheService
    {
        T GetOrCreate<T>(string key, Func<T> factory, int minutes = 10);
        void Remove(string key);
    }
}
