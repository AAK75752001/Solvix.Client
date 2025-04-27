using Solvix.Client.Core.Interfaces;

namespace Solvix.Client.Core.Services
{
    public class SecureStorageService : ISecureStorageService
    {
        public async Task SaveAsync(string key, string value)
        {
            try
            {
                await SecureStorage.SetAsync(key, value);
            }
            catch (Exception)
            {
                // Fallback to Preferences if SecureStorage fails
                Preferences.Set(key, value);
            }
        }

        public async Task<string?> GetAsync(string key)
        {
            try
            {
                return await SecureStorage.GetAsync(key) ?? Preferences.Get(key, null);
            }
            catch (Exception)
            {
                // Fallback to Preferences if SecureStorage fails
                return Preferences.Get(key, null);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                SecureStorage.Remove(key);
            }
            catch (Exception)
            {
                // Fallback to Preferences if SecureStorage fails
                Preferences.Remove(key);
            }

            await Task.CompletedTask;
        }

        public async Task ClearAsync()
        {
            try
            {
                SecureStorage.RemoveAll();
            }
            catch (Exception)
            {
                // Fallback to Preferences if SecureStorage fails
                Preferences.Clear();
            }

            await Task.CompletedTask;
        }
    }
}
