namespace Solvix.Client.Core.Interfaces
{
    public interface IApiService
    {
        Task<T?> GetAsync<T>(string endpoint, bool requiresAuth = true);
        Task<T?> GetAsync<T>(string endpoint, Dictionary<string, string> queryParams, bool requiresAuth = true);
        Task<T?> PostAsync<T>(string endpoint, object data, bool requiresAuth = true);
        Task<T?> PutAsync<T>(string endpoint, object data, bool requiresAuth = true);
        Task<T?> DeleteAsync<T>(string endpoint, bool requiresAuth = true);
    }
}
