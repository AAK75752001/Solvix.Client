using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;

namespace Solvix.Client.Core.Services
{
    public class ConnectivityService : IConnectivityService
    {
        private readonly ILogger<ConnectivityService> _logger;

        // For development purposes, override connectivity state
        // This will ensure the app attempts to connect even if connectivity check fails
        private bool _forceConnected = true;

        public bool IsConnected
        {
            get
            {
                var networkStatus = Connectivity.NetworkAccess == NetworkAccess.Internet;

                // In debug mode, we can override connectivity check
#if DEBUG
                if (_forceConnected)
                {
                    _logger.LogInformation("Forcing connectivity to true for development");
                    return true;
                }
#endif

                return networkStatus;
            }
        }

        public event Action<bool> ConnectivityChanged;

        public ConnectivityService(ILogger<ConnectivityService> logger)
        {
            _logger = logger;

            try
            {
                _logger.LogInformation("Initializing ConnectivityService");
                Connectivity.ConnectivityChanged += OnConnectivityChanged;

                _logger.LogInformation("ConnectivityService initialized. Initial state: {IsConnected}", IsConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ConnectivityService");
            }
        }

        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Network connectivity event: {NetworkAccess}", e.NetworkAccess);

                // In debug mode, we can override connectivity
#if DEBUG
                if (_forceConnected)
                {
                    _logger.LogInformation("Forcing connectivity to true despite network change");
                    ConnectivityChanged?.Invoke(true);
                    return;
                }
#endif

                var isConnected = e.NetworkAccess == NetworkAccess.Internet;
                _logger.LogInformation("Network connectivity changed to: {IsConnected}", isConnected);
                ConnectivityChanged?.Invoke(isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ConnectivityChanged event handler");
            }
        }
    }
}