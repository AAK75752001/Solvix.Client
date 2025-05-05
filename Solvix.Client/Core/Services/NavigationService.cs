using Solvix.Client.Core.Interfaces;


namespace Solvix.Client.Core.Services
{
    public class NavigationService : INavigationService
    {
        public Task NavigateToAsync(string route)
        {
            return Shell.Current.GoToAsync(route);
        }
    }
}
