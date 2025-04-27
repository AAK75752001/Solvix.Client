namespace Solvix.Client.Core.Interfaces
{
    public interface IToastService
    {
        Task ShowToastAsync(string message, ToastType type = ToastType.Info);
    }
}
