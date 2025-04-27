namespace Solvix.Client.Core.Interfaces
{
    public interface IConnectivityService
    {
        bool IsConnected { get; }
        event Action<bool> ConnectivityChanged;
    }
}
