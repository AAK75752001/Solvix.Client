namespace Solvix.Client.Core.Interfaces
{
    public interface ISecureStorageService
    {
        Task SaveAsync(string key, string value);
        Task<string?> GetAsync(string key);
        Task RemoveAsync(string key);
        Task ClearAsync();
    }
}
