namespace Solvix.Client.Core.Interfaces
{
    public interface ISettingsService
    {
        string GetTheme();
        Task SetThemeAsync(string theme);
    }
}
