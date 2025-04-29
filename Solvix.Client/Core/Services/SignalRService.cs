using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Services
{
    public class SignalRService : ISignalRService
    {
        private HubConnection _hubConnection;
        private readonly ISecureStorageService _secureStorageService;
        private readonly IConnectivityService _connectivityService;
        private readonly IToastService _toastService;
        private readonly ILogger<SignalRService> _logger;
        private bool _isConnected = false;
        private bool _isConnecting = false;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private int _retryAttempts = 0;
        private const int MaxRetryAttempts = 3;
        private bool _isInitialized = false;
        private bool _showConnectionErrors = false;

        public event Action<MessageModel> OnMessageReceived;
        public event Action<Guid, int> OnMessageRead;
        public event Action<string> OnError;
        public event Action<long, bool, DateTime?> OnUserStatusChanged;

        public SignalRService(
            ISecureStorageService secureStorageService,
            IConnectivityService connectivityService,
            IToastService toastService,
            ILogger<SignalRService> logger)
        {
            _secureStorageService = secureStorageService;
            _connectivityService = connectivityService;
            _toastService = toastService;
            _logger = logger;

            // Listen for connectivity changes
            _connectivityService.ConnectivityChanged += async (isConnected) =>
            {
                _logger.LogInformation("Network connectivity changed to: {IsConnected}", isConnected);

                if (isConnected && !_isConnected && _isInitialized)
                {
                    _logger.LogInformation("Network is now available, connecting to SignalR");
                    // Do not await here to avoid blocking
                    _ = ConnectAsync();
                }
                else if (!isConnected && _isConnected)
                {
                    _logger.LogInformation("Network is no longer available, disconnecting from SignalR");
                    // Do not await here to avoid blocking
                    _ = DisconnectAsync();
                }
            };

            // Start initialization in background
            Task.Run(() => InitializeHub());
        }

        private async Task InitializeHub()
        {
            try
            {
                _logger.LogInformation("Initializing SignalR hub");

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(Constants.SignalRUrl, options =>
                    {
                        options.AccessTokenProvider = async () =>
                        {
                            var token = await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
                            _logger.LogDebug("SignalR using token: {TokenPresent}", !string.IsNullOrEmpty(token));
                            return token;
                        };

                        // Add timeouts for debugging
                        options.HttpMessageHandlerFactory = handler => {
                            if (handler is HttpClientHandler clientHandler)
                            {
                                // Increase timeout
                                clientHandler.ServerCertificateCustomValidationCallback =
                                    (sender, certificate, chain, sslPolicyErrors) => true;
                            }
                            return handler;
                        };
                    })
                    .WithAutomaticReconnect(new[] {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(3),
                        TimeSpan.FromSeconds(5)
                    })
                    .Build();

                _logger.LogInformation("Setting up SignalR event handlers");

                ConfigureEventHandlers();

                _isInitialized = true;

                // Connect if the user is logged in
                var token = await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
                if (!string.IsNullOrEmpty(token))
                {
                    _logger.LogInformation("User is logged in, connecting to SignalR");

                    // Note: We don't await here to prevent blocking initialization
                    _ = ConnectAsync();
                }
                else
                {
                    _logger.LogInformation("No auth token found, not connecting to SignalR");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing SignalR hub");
            }
        }

        private void ConfigureEventHandlers()
        {
            try
            {
                // Set up event handlers
                _hubConnection.On<int, long, string, string, Guid, DateTime>("ReceiveMessage",
                    (messageId, senderId, senderName, content, chatId, sentAt) =>
                    {
                        _logger.LogInformation("Received message {MessageId} from {SenderName} in chat {ChatId}",
                            messageId, senderName, chatId);

                        var message = new MessageModel
                        {
                            Id = messageId,
                            Content = content,
                            SentAt = sentAt,
                            SenderId = senderId,
                            SenderName = senderName,
                            ChatId = chatId,
                            IsRead = false,
                            Status = Constants.MessageStatus.Delivered
                        };

                        OnMessageReceived?.Invoke(message);
                    });

                _hubConnection.On<Guid, int>("MessageRead", (chatId, messageId) =>
                {
                    _logger.LogInformation("Message {MessageId} in chat {ChatId} marked as read",
                        messageId, chatId);

                    OnMessageRead?.Invoke(chatId, messageId);
                });

                _hubConnection.On<int>("MessageSentConfirmation", (messageId) =>
                {
                    _logger.LogInformation("Message {MessageId} confirmed as sent and stored on server",
                        messageId);
                });

                _hubConnection.On<string>("ReceiveError", (errorMessage) =>
                {
                    _logger.LogWarning("Received error from SignalR: {ErrorMessage}", errorMessage);
                    OnError?.Invoke(errorMessage);
                });

                _hubConnection.On<long, bool, DateTime?>("UserStatusChanged", (userId, isOnline, lastActive) =>
                {
                    _logger.LogInformation("User {UserId} status changed: Online = {IsOnline}, LastActive = {LastActive}",
                        userId, isOnline, lastActive);

                    OnUserStatusChanged?.Invoke(userId, isOnline, lastActive);
                });

                // Handler for reconnection events
                _hubConnection.Reconnecting += (error) =>
                {
                    _isConnected = false;
                    _logger.LogWarning(error, "SignalR connection lost. Attempting to reconnect...");
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += (connectionId) =>
                {
                    _isConnected = true;
                    _logger.LogInformation("SignalR reconnected with ID: {ConnectionId}", connectionId);
                    _retryAttempts = 0; // Reset retry counter on successful reconnection
                    _showConnectionErrors = false; // Reset error display flag
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += async (error) =>
                {
                    _isConnected = false;
                    _logger.LogWarning(error, "SignalR connection closed");

                    // If closed due to an error (not requested disconnect), try to reconnect
                    if (error != null && _connectivityService.IsConnected && _retryAttempts < MaxRetryAttempts)
                    {
                        _retryAttempts++;
                        await Task.Delay(1000 * _retryAttempts); // Progressive backoff

                        // Don't wait for reconnection attempt
                        _ = ConnectAsync();
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring SignalR event handlers");
            }
        }

        public async Task ConnectAsync()
        {
            // Try to get lock but don't block if already locked
            bool lockAcquired = await _connectionLock.WaitAsync(0);
            if (!lockAcquired)
            {
                _logger.LogInformation("Connection attempt already in progress");
                return;
            }

            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("SignalR not initialized yet, deferring connection");
                    return;
                }

                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    _logger.LogInformation("Already connected to SignalR, skipping connection");
                    _isConnected = true;
                    return;
                }

                if (_isConnecting)
                {
                    _logger.LogInformation("Connection already in progress, skipping duplicate connect request");
                    return;
                }

                if (!_connectivityService.IsConnected)
                {
                    _logger.LogWarning("Cannot connect to SignalR. No internet connection");
                    return;
                }

                _isConnecting = true;
                _logger.LogInformation("Connecting to SignalR... Current state: {State}", _hubConnection.State);

                try
                {
                    // Set up a cancellation token for timeout
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    // Start connecting
                    await _hubConnection.StartAsync(cts.Token);

                    _isConnected = true;
                    _retryAttempts = 0; // Reset retry counter on successful connection
                    _showConnectionErrors = false; // Reset error display flag
                    _logger.LogInformation("Successfully connected to SignalR");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("SignalR connection timed out after 10 seconds");
                    _isConnected = false;

                    // Try again in background if still not at max retries
                    if (_retryAttempts < MaxRetryAttempts)
                    {
                        _retryAttempts++;

                        // Schedule a retry in the background
                        Task.Run(async () => {
                            await Task.Delay(1000 * _retryAttempts);
                            await ConnectAsync();
                        });
                    }
                    else if (_showConnectionErrors)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () => {
                            await _toastService.ShowToastAsync("Could not connect to chat service. Some features may be limited.", ToastType.Warning);
                        });
                    }
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    _logger.LogError(ex, "Error connecting to SignalR");

                    // Don't show a toast for every retry - only show after max retries
                    if (_retryAttempts >= MaxRetryAttempts && _showConnectionErrors)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () => {
                            await _toastService.ShowToastAsync("Could not connect to chat service. Some features may be limited.", ToastType.Warning);
                        });
                    }
                }
            }
            finally
            {
                _isConnecting = false;
                _connectionLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            // Try to get lock but don't block if already locked
            bool lockAcquired = await _connectionLock.WaitAsync(0);
            if (!lockAcquired)
            {
                _logger.LogInformation("Disconnect attempt skipped because connection is in progress");
                return;
            }

            try
            {
                if (_hubConnection.State == HubConnectionState.Disconnected)
                {
                    _logger.LogInformation("Already disconnected from SignalR");
                    _isConnected = false;
                    return;
                }

                _logger.LogInformation("Disconnecting from SignalR...");

                try
                {
                    // Set up a cancellation token for timeout
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    await _hubConnection.StopAsync(cts.Token);
                    _isConnected = false;
                    _logger.LogInformation("Successfully disconnected from SignalR");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("SignalR disconnect operation timed out");
                    _isConnected = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disconnecting from SignalR");
                    _isConnected = false;
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task SendMessageAsync(Guid chatId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Attempted to send empty message to chat {ChatId}", chatId);
                return;
            }

            try
            {
                // Only attempt to send via SignalR if connected - otherwise just let the ChatService handle it via API
                if (_hubConnection.State != HubConnectionState.Connected)
                {
                    _logger.LogInformation("Not connected to SignalR, message will be sent via API only");
                    return;
                }

                _logger.LogInformation("Sending message to chat {ChatId} via SignalR", chatId);

                // Use a timeout to prevent long waits
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _hubConnection.InvokeAsync("SendToChat", chatId, message, cts.Token);

                _logger.LogInformation("Message sent to chat {ChatId} via SignalR", chatId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("SignalR message sending timed out for chat {ChatId}", chatId);
                // We don't rethrow here - the ChatService will still send via API
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to chat {ChatId} via SignalR", chatId);
                // We don't rethrow here - the ChatService will still send via API
            }
        }

        public async Task MarkMessageAsReadAsync(int messageId)
        {
            try
            {
                // Only try if connected - otherwise let the API handle it
                if (_hubConnection.State != HubConnectionState.Connected)
                {
                    _logger.LogInformation("Not connected to SignalR, marking read status via API only");
                    return;
                }

                _logger.LogInformation("Marking message {MessageId} as read via SignalR", messageId);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _hubConnection.InvokeAsync("MarkMessageAsRead", messageId, cts.Token);

                _logger.LogInformation("Message {MessageId} marked as read via SignalR", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read via SignalR", messageId);
                // Don't rethrow as this is a background operation
            }
        }

        public async Task MarkMessagesAsReadAsync(List<int> messageIds)
        {
            if (messageIds == null || messageIds.Count == 0)
            {
                _logger.LogInformation("No messages to mark as read");
                return;
            }

            try
            {
                // Only try if connected - otherwise let the API handle it
                if (_hubConnection.State != HubConnectionState.Connected)
                {
                    _logger.LogInformation("Not connected to SignalR, marking read status via API only");
                    return;
                }

                _logger.LogInformation("Marking {Count} messages as read via SignalR", messageIds.Count);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _hubConnection.InvokeAsync("MarkMultipleMessagesAsRead", messageIds, cts.Token);

                _logger.LogInformation("{Count} messages marked as read via SignalR", messageIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read via SignalR");
                // Don't rethrow as this is a background operation
            }
        }

        // Enable or disable showing connection errors to the user
        public void SetShowConnectionErrors(bool show)
        {
            _showConnectionErrors = show;
        }
    }
}