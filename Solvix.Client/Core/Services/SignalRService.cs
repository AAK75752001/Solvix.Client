using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.Concurrent;

namespace Solvix.Client.Core.Services
{
    public class SignalRService : ISignalRService, IDisposable
    {
        private readonly ILogger<SignalRService> _logger;
        private readonly IAuthService _authService;
        private readonly IConnectivityService _connectivityService;
        private readonly IToastService _toastService;

        private HubConnection? _hubConnection;
        private bool _isConnecting = false;
        private bool _isDisposed = false;
        private bool _autoReconnect = true;

        private readonly SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<PendingMessage> _messageQueue = new ConcurrentQueue<PendingMessage>();
        private bool _isProcessingQueue = false;

        // Events
        public event Action<MessageModel>? OnMessageReceived;
        public event Action<Guid, int, int>? OnMessageStatusUpdated;
        public event Action<long, bool>? OnUserStatusChanged;
        public event Action<bool>? OnConnectionStateChanged;
        public event Action<Guid, long, bool>? OnUserTyping;
        public event Action<string, int>? OnMessageCorrelationConfirmation;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        private int _reconnectAttempts = 0;
        private readonly int _maxReconnectAttempts = 5;
        private readonly TimeSpan[] _reconnectDelays = new[]
        {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    };

        public SignalRService(
            ILogger<SignalRService> logger,
            IAuthService authService,
            IConnectivityService connectivityService,
            IToastService toastService)
        {
            _logger = logger;
            _authService = authService;
            _connectivityService = connectivityService;
            _toastService = toastService;

            _connectivityService.ConnectivityChanged += OnConnectivityChanged;
        }

        public async Task StartAsync()
        {
            if (IsConnected || _isConnecting) return;

            await _connectionSemaphore.WaitAsync();
            try
            {
                // Double check to ensure connection wasn't established while waiting
                if (IsConnected || _isConnecting) return;
                _isConnecting = true;

                _logger.LogInformation("Starting SignalR connection...");

                if (!_connectivityService.IsConnected)
                {
                    _logger.LogWarning("Cannot start SignalR - No internet connection");
                    return;
                }

                string? token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Cannot start SignalR - No authentication token");
                    return;
                }

                try
                {
                    await InitializeHubConnectionAsync(token);
                    await _hubConnection!.StartAsync();

                    _logger.LogInformation("SignalR connection established successfully");
                    OnConnectionStateChanged?.Invoke(true);

                    // Process queued messages
                    ProcessPendingMessages();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting SignalR connection");
                    await _toastService.ShowToastAsync("خطا در اتصال به سرور پیام رسان", ToastType.Error);
                }
            }
            finally
            {
                _isConnecting = false;
                _connectionSemaphore.Release();
            }
        }

        public async Task StopAsync(bool autoReconnect = true)
        {
            _autoReconnect = autoReconnect;

            if (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
                return;

            try
            {
                await _hubConnection.StopAsync();
                _logger.LogInformation("SignalR connection stopped");
                OnConnectionStateChanged?.Invoke(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping SignalR connection");
            }
        }

        public async Task<bool> SendMessageAsync(MessageModel message)
        {
            if (message == null || string.IsNullOrEmpty(message.CorrelationId))
            {
                _logger.LogError("Cannot send message - Message or CorrelationId is null/empty.");
                return false;
            }

            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send message - SignalR not connected");
                QueueMessage(message);
                await StartAsync();
                return false;
            }

            try
            {
                _logger.LogInformation("Sending message via SignalR to chat {ChatId}: {Content}",
           message.ChatId, message.Content.Substring(0, Math.Min(20, message.Content.Length)));

                await _hubConnection!.InvokeAsync("SendToChat", message.ChatId, message.Content, message.CorrelationId);
                _logger.LogInformation("Message sent via SignalR to chat {ChatId} with CorrelationId {CorrelationId}", message.ChatId, message.CorrelationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message via SignalR. CorrelationId: {CorrelationId}", message.CorrelationId);
                QueueMessage(message);
                return false;
            }
        }

        public async Task MarkAsReadAsync(Guid chatId, int messageId)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot mark message as read - SignalR not connected");
                await StartAsync();
                return;
            }

            try
            {
                await _hubConnection!.InvokeAsync("MarkMessageAsRead", messageId);
                _logger.LogInformation("Message {MessageId} marked as read via SignalR", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read via SignalR");
            }
        }

        public async Task TypingAsync(Guid chatId, bool isTyping)
        {
            if (!IsConnected)
                return;

            try
            {
                await _hubConnection!.InvokeAsync("UserTyping", chatId, isTyping);
                _logger.LogDebug("Sent typing status ({IsTyping}) for chat {ChatId}", isTyping, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending typing status via SignalR");
            }
        }

        private async Task InitializeHubConnectionAsync(string token)
        {
            if (_hubConnection != null)
            {
                _hubConnection.Closed -= OnConnectionClosed;
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(Constants.SignalRUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token)!;
#if DEBUG
                    options.HttpMessageHandlerFactory = (handler) =>
                    {
                        if (handler is HttpClientHandler clientHandler)
                        {
                            clientHandler.ServerCertificateCustomValidationCallback =
                                (message, cert, chain, errors) => true;
                        }
                        return handler;
                    };
#endif
                })
                .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            _hubConnection.On<MessageModel>("ReceiveMessage", OnReceivedMessageHandler);
            _hubConnection.On<Guid, int, int>("MessageStatusChanged", OnMessageStatusChangedHandler);
            _hubConnection.On<long, bool>("UserStatusChanged", OnUserStatusChangedHandler);
            _hubConnection.On<Guid, long, bool>("UserTyping", OnUserTypingHandler);
            _hubConnection.On<string, int>("MessageCorrelationConfirmation", OnMessageCorrelationConfirmationHandler);
            _hubConnection.On<string>("ReceiveError", message => _logger.LogError("SignalR Error: {Message}", message));
            _hubConnection.Closed += OnConnectionClosed;
        }

        private void OnReceivedMessageHandler(MessageModel message)
        {
            _logger.LogInformation("Message received from SignalR for chat {ChatId}, Content: {Content}",
                message.ChatId,
                message.Content?.Substring(0, Math.Min(20, message.Content?.Length ?? 0)));

            // اطمینان از داشتن مقادیر صحیح در پیام
            if (message.SentAt == default)
            {
                message.SentAt = DateTime.UtcNow;
                _logger.LogWarning("Message {MessageId} had default SentAt, setting to current time", message.Id);
            }

            OnMessageReceived?.Invoke(message);
        }

        private void OnMessageStatusChangedHandler(Guid chatId, int messageId, int status)
        {
            _logger.LogDebug("Message status changed: Chat={ChatId}, MessageId={MessageId}, Status={Status}",
                chatId, messageId, status);
            OnMessageStatusUpdated?.Invoke(chatId, messageId, status);
        }

        private void OnUserStatusChangedHandler(long userId, bool isOnline)
        {
            _logger.LogInformation("User {UserId} is now {Status}", userId, isOnline ? "online" : "offline");
            OnUserStatusChanged?.Invoke(userId, isOnline);
        }

        private void OnUserTypingHandler(Guid chatId, long userId, bool isTyping)
        {
            _logger.LogDebug("User {UserId} is {Status} in chat {ChatId}",
                userId, isTyping ? "typing" : "not typing", chatId);
            OnUserTyping?.Invoke(chatId, userId, isTyping);
        }

        private void OnMessageCorrelationConfirmationHandler(string correlationId, int serverMessageId)
        {
            _logger.LogInformation("Received message confirmation: CorrelationId={CorrelationId}, ServerMessageId={ServerMessageId}",
                correlationId, serverMessageId);
            OnMessageCorrelationConfirmation?.Invoke(correlationId, serverMessageId);
        }

        private async Task OnConnectionClosed(Exception? exception)
        {
            OnConnectionStateChanged?.Invoke(false);

            if (exception != null)
            {
                _logger.LogError(exception, "SignalR connection closed with error");
            }
            else
            {
                _logger.LogInformation("SignalR connection closed normally");
            }

            if (_autoReconnect && !_isDisposed && _connectivityService.IsConnected)
            {
                await ReconnectWithBackoffAsync();
            }
        }


        private async Task ReconnectWithBackoffAsync()
        {
            while (_reconnectAttempts < _maxReconnectAttempts && !_isDisposed)
            {
                var delay = _reconnectDelays[Math.Min(_reconnectAttempts, _reconnectDelays.Length - 1)];
                _logger.LogInformation("Attempting to reconnect (attempt {Attempt}/{Max}) after {Delay} seconds",
                    _reconnectAttempts + 1, _maxReconnectAttempts, delay.TotalSeconds);

                await Task.Delay(delay);

                if (_isDisposed) return;

                try
                {
                    await StartAsync();
                    _reconnectAttempts = 0; // Reset on successful connection
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reconnection attempt {Attempt} failed", _reconnectAttempts + 1);
                    _reconnectAttempts++;
                }
            }

            if (_reconnectAttempts >= _maxReconnectAttempts)
            {
                _logger.LogError("Max reconnection attempts reached. Connection failed.");
                await _toastService.ShowToastAsync("اتصال به سرور برقرار نشد. لطفاً بعداً تلاش کنید.", ToastType.Error);
            }
        }

        private async void OnConnectivityChanged(bool isConnected)
        {
            if (isConnected && _autoReconnect && !IsConnected && !_isDisposed)
            {
                _logger.LogInformation("Network connection restored. Reconnecting to SignalR...");
                await StartAsync();
            }
            else if (!isConnected && IsConnected)
            {
                _logger.LogInformation("Network connection lost. SignalR will disconnect.");
            }
        }

        private void QueueMessage(MessageModel message)
        {
            var pendingMessage = new PendingMessage { Message = message, Timestamp = DateTime.UtcNow };
            _messageQueue.Enqueue(pendingMessage);
            _logger.LogInformation("Message queued for later sending. Queue size: {Count}", _messageQueue.Count);
        }

        private async void ProcessPendingMessages()
        {
            if (_isProcessingQueue || !IsConnected) return;
            _isProcessingQueue = true;
            try
            {
                _logger.LogInformation("Processing pending message queue. Count: {Count}", _messageQueue.Count);
                int processedCount = 0;
                while (_messageQueue.TryDequeue(out PendingMessage pendingMessage))
                {
                    if ((DateTime.UtcNow - pendingMessage.Timestamp).TotalHours > 24)
                    {
                        _logger.LogWarning("Discarding old queued message (CorrId: {CorrId})", pendingMessage.Message?.CorrelationId);
                        continue;
                    }

                    if (pendingMessage.Message == null || string.IsNullOrEmpty(pendingMessage.Message.CorrelationId))
                    {
                        _logger.LogError("Found invalid message in queue, discarding.");
                        continue;
                    }

                    try
                    {
                        await _hubConnection!.InvokeAsync("SendToChat",
                            pendingMessage.Message.ChatId,
                            pendingMessage.Message.Content,
                            pendingMessage.Message.CorrelationId);

                        _logger.LogInformation("Queued message sent successfully (CorrId: {CorrId})", pendingMessage.Message.CorrelationId);
                        processedCount++;
                        await Task.Delay(300);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending queued message (CorrId: {CorrId})", pendingMessage.Message.CorrelationId);
                        if (pendingMessage.RetryCount < 3)
                        {
                            pendingMessage.RetryCount++;
                            _messageQueue.Enqueue(pendingMessage);
                        }
                        else
                        {
                            _logger.LogWarning("Max retry count reached for queued message (CorrId: {CorrId}). Discarding.", pendingMessage.Message.CorrelationId);
                        }
                        break;
                    }
                }
                _logger.LogInformation("Processed {Count} queued messages", processedCount);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error processing pending messages queue"); }
            finally { _isProcessingQueue = false; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _isDisposed = true;
                _autoReconnect = false;

                _connectivityService.ConnectivityChanged -= OnConnectivityChanged;

                StopAsync(false).ConfigureAwait(false);

                _hubConnection?.DisposeAsync().AsTask().ConfigureAwait(false);
                _connectionSemaphore.Dispose();
            }

            _isDisposed = true;
        }

        private class PendingMessage
        {
            public MessageModel? Message { get; set; }
            public DateTime Timestamp { get; set; }
            public int RetryCount { get; set; }
        }
    }
}