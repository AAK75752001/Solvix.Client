using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Solvix.Client.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly IApiService _apiService;
        private readonly IToastService _toastService;
        private readonly ISignalRService _signalRService;
        private readonly IAuthService _authService;
        private readonly ILogger<ChatService> _logger;
        private readonly SemaphoreSlim _sendMessageLock = new SemaphoreSlim(1, 1);

        // Track sent messages to prevent duplicates
        private readonly Dictionary<string, DateTime> _sentMessages = new Dictionary<string, DateTime>();
        private readonly SemaphoreSlim _sentMessagesLock = new SemaphoreSlim(1, 1);

        public ChatService(
            IApiService apiService,
            IToastService toastService,
            ISignalRService signalRService,
            IAuthService authService,
            ILogger<ChatService> logger)
        {
            _apiService = apiService;
            _toastService = toastService;
            _signalRService = signalRService;
            _authService = authService;
            _logger = logger;

            // Periodically clean up old sent message records
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    await CleanupSentMessagesAsync();
                }
            });
        }

        private async Task CleanupSentMessagesAsync()
        {
            await _sentMessagesLock.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var keysToRemove = _sentMessages
                    .Where(kvp => (now - kvp.Value).TotalMinutes > 30)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _sentMessages.Remove(key);
                }

                _logger.LogInformation("Cleaned up {Count} old sent message records", keysToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up sent messages cache");
            }
            finally
            {
                _sentMessagesLock.Release();
            }
        }

        public async Task<List<MessageModel>> GetMessagesAsync(Guid chatId, int skip = 0, int take = 50)
        {
            try
            {
                // First check cache if skip is 0 (initial load)
                if (skip == 0)
                {
                    var cachedMessages = MessageCache.GetCachedMessages(chatId);
                    if (cachedMessages != null && cachedMessages.Count > 0)
                    {
                        _logger.LogInformation("Using cached messages for chat {ChatId}, count: {Count}",
                            chatId, cachedMessages.Count);
                        return cachedMessages;
                    }
                    else
                    {
                        _logger.LogInformation("No cache found for chat {ChatId}, loading from server", chatId);
                    }
                }

                _logger.LogInformation("Fetching messages for chat {ChatId}, skip={Skip}, take={Take}",
                    chatId, skip, take);

                var endpoint = $"{Constants.Endpoints.GetMessages}/{chatId}/messages";
                var queryParams = new Dictionary<string, string>
                {
                    { "skip", skip.ToString() },
                    { "take", take.ToString() }
                };

                var messages = await _apiService.GetAsync<List<MessageModel>>(endpoint, queryParams);

                if (messages != null && messages.Count > 0)
                {
                    _logger.LogInformation("Retrieved {Count} messages for chat {ChatId}",
                        messages.Count, chatId);

                    // Get current user ID for message ownership determination
                    var currentUserId = await GetCurrentUserIdAsync();

                    // Update message status based on read state
                    foreach (var message in messages)
                    {
                        // Check if this is the current user's message
                        bool isOwnMessage = message.SenderId == currentUserId;
                        message.IsOwnMessage = isOwnMessage;

                        if (isOwnMessage)
                        {
                            // Set status for current user's messages
                            message.Status = message.IsRead ?
                                Constants.MessageStatus.Read :
                                Constants.MessageStatus.Delivered;
                        }

                        // Set message time text for display
                        message.SentAtFormatted = FormatMessageTime(message.SentAt);
                    }

                    // Cache the messages if this is the initial load
                    if (skip == 0)
                    {
                        MessageCache.CacheMessages(chatId, messages);
                        _logger.LogInformation("Cached {Count} messages for chat {ChatId}",
                            messages.Count, chatId);
                    }

                    return messages;
                }
                else
                {
                    _logger.LogWarning("No messages found for chat {ChatId}", chatId);
                    return new List<MessageModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load messages for chat {ChatId}", chatId);
                return new List<MessageModel>();
            }
        }

        public async Task<MessageModel?> SendMessageAsync(Guid chatId, string content)
        {
            bool lockTaken = false;
            try
            {
                // Use a lock to prevent concurrent sends of the same message
                await _sendMessageLock.WaitAsync();
                lockTaken = true;

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Attempted to send empty message to chat {ChatId}", chatId);
                    await _toastService.ShowToastAsync("Cannot send empty message", ToastType.Warning);
                    return null;
                }

                // Check if we've sent this exact message in the last minute
                string messageKey = $"{chatId}:{content.GetHashCode()}";
                bool isDuplicate = false;

                await _sentMessagesLock.WaitAsync();
                try
                {
                    if (_sentMessages.TryGetValue(messageKey, out var sentTime))
                    {
                        // If sent less than 60 seconds ago, treat as duplicate
                        if ((DateTime.UtcNow - sentTime).TotalSeconds < 60)
                        {
                            isDuplicate = true;
                            _logger.LogWarning("Detected duplicate message to chat {ChatId} within 60 seconds", chatId);
                        }
                    }

                    if (!isDuplicate)
                    {
                        // Record this message as sent
                        _sentMessages[messageKey] = DateTime.UtcNow;
                    }
                }
                finally
                {
                    _sentMessagesLock.Release();
                }

                if (isDuplicate)
                {
                    await _toastService.ShowToastAsync("Message already sent", ToastType.Info);
                    return null;
                }

                _logger.LogInformation("Sending message to chat {ChatId}", chatId);

                // Tell SignalR to clear message tracking when sending new messages
                await _signalRService.ClearMessageTrackingAsync();

                // Create DTO for API call
                var dto = new SendMessageDto
                {
                    ChatId = chatId,
                    Content = content
                };

                // Send via API for persistence
                var response = await _apiService.PostAsync<MessageModel>(Constants.Endpoints.SendMessage, dto);

                // Wait a moment before sending via SignalR to ensure server persistence
                // This avoids race conditions with SignalR delivery
                await Task.Delay(100);

                // Send via SignalR for real-time delivery 
                Task signalRTask = _signalRService.SendMessageAsync(chatId, content);

                if (response != null)
                {
                    _logger.LogInformation("Message sent successfully to chat {ChatId}, server ID: {MessageId}",
                        chatId, response.Id);

                    // Get current user ID for message ownership
                    var currentUserId = await GetCurrentUserIdAsync();

                    // Populate extra properties for UI
                    response.Status = Constants.MessageStatus.Sent;
                    response.SentAtFormatted = FormatMessageTime(response.SentAt);
                    response.IsOwnMessage = response.SenderId == currentUserId;

                    // Mark cache as invalid to ensure fresh data on next load
                    MessageCache.InvalidateCache(chatId);

                    return response;
                }
                else
                {
                    _logger.LogWarning("Failed to send message to chat {ChatId}", chatId);
                    await _toastService.ShowToastAsync("Message could not be sent", ToastType.Warning);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("Failed to send message: " + ex.Message, ToastType.Error);
                return null;
            }
            finally
            {
                if (lockTaken)
                {
                    _sendMessageLock.Release();
                }
            }
        }

        public async Task MarkAsReadAsync(Guid chatId, List<int> messageIds)
        {
            if (messageIds == null || messageIds.Count == 0)
            {
                _logger.LogInformation("No messages to mark as read for chat {ChatId}", chatId);
                return;
            }

            try
            {
                _logger.LogInformation("Marking {Count} messages as read in chat {ChatId}",
                    messageIds.Count, chatId);

                var endpoint = $"{Constants.Endpoints.MarkRead}/{chatId}/mark-read";
                await _apiService.PostAsync<object>(endpoint, messageIds);

                // Also mark via SignalR for real-time updates to other clients
                await _signalRService.MarkMessagesAsReadAsync(messageIds);

                _logger.LogInformation("Successfully marked messages as read in chat {ChatId}", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark messages as read in chat {ChatId}", chatId);
                // Don't show toast here as this is a background operation
            }
        }

        #region Helper Methods

        private string FormatMessageTime(DateTime dateTime)
        {
            try
            {
                // Convert UTC server time to local time
                var localDateTime = dateTime.ToLocalTime();
                var now = DateTime.Now;

                // Today, show only time in 24-hour format
                if (localDateTime.Date == now.Date)
                {
                    return localDateTime.ToString("HH:mm");
                }
                // Yesterday
                else if (localDateTime.Date == now.Date.AddDays(-1))
                {
                    return "Yesterday " + localDateTime.ToString("HH:mm");
                }
                // Within the last week
                else if ((now.Date - localDateTime.Date).TotalDays < 7)
                {
                    return localDateTime.ToString("ddd HH:mm"); // Day of week + time
                }
                // Older messages
                else
                {
                    return localDateTime.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch (Exception ex)
            {
                // If there's an error, just display the time
                try
                {
                    return dateTime.ToLocalTime().ToString("HH:mm");
                }
                catch
                {
                    // Fallback to simple time format if any error occurs
                    return dateTime.ToString("HH:mm");
                }
            }
        }

        #endregion

        public async Task<long> GetCurrentUserIdAsync()
        {
            try
            {
                return await _authService.GetUserIdAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID");
                return 0; // Default value if there's an error
            }
        }

        public async Task<List<ChatModel>> GetChatsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching chats");

                var response = await _apiService.GetAsync<List<ChatModel>>(Constants.Endpoints.GetChats);

                if (response != null)
                {
                    // Log online status for debugging
                    foreach (var chat in response)
                    {
                        foreach (var participant in chat.Participants)
                        {
                            _logger.LogDebug("Participant {UserId} ({Username}) in chat {ChatId} - IsOnline: {IsOnline}",
                                participant.Id, participant.Username, chat.Id, participant.IsOnline);
                        }
                    }

                    // Initialize computed properties for each chat
                    foreach (var chat in response)
                    {
                        chat.InitializeComputedProperties();
                    }

                    return response;
                }

                return new List<ChatModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chats");
                throw;
            }
        }

        public async Task<ChatModel?> GetChatAsync(Guid chatId)
        {
            try
            {
                _logger.LogInformation("Fetching chat {ChatId}", chatId);

                var endpoint = $"{Constants.Endpoints.GetChat}/{chatId}";
                var chat = await _apiService.GetAsync<ChatModel>(endpoint);

                if (chat != null)
                {
                    // Initialize collections if they're null
                    chat.Participants ??= new List<UserModel>();
                    chat.Messages ??= new ObservableCollection<MessageModel>();

                    // Get the current user ID to determine which participant is the "other" one
                    var currentUserId = await GetCurrentUserIdAsync();

                    // Log participant details
                    foreach (var participant in chat.Participants)
                    {
                        // Mark our own participant as online (from our perspective)
                        if (participant.Id == currentUserId)
                        {
                            participant.IsOnline = true;
                        }

                        _logger.LogDebug("Participant {UserId} ({Username}): IsOnline = {IsOnline}, Current User = {IsCurrentUser}",
                            participant.Id, participant.Username, participant.IsOnline, participant.Id == currentUserId);
                    }

                    // Initialize computed properties
                    chat.InitializeComputedProperties();

                    // Tell SignalR to clear its message tracking when changing chats
                    await _signalRService.ClearMessageTrackingAsync();

                    _logger.LogInformation("Successfully retrieved chat {ChatId}", chatId);
                    return chat;
                }
                else
                {
                    _logger.LogWarning("Chat not found: {ChatId}", chatId);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chat {ChatId}", chatId);
                throw;
            }
        }

        public async Task<Guid?> StartChatAsync(long userId)
        {
            try
            {
                _logger.LogInformation("Starting chat with user {UserId}", userId);

                var response = await _apiService.PostAsync<dynamic>(Constants.Endpoints.StartChat, userId);

                if (response != null)
                {
                    try
                    {
                        // Parse the response to get the chat ID
                        string responseJson = System.Text.Json.JsonSerializer.Serialize(response);
                        using (JsonDocument doc = JsonDocument.Parse(responseJson))
                        {
                            JsonElement root = doc.RootElement;

                            if (root.TryGetProperty("chatId", out JsonElement chatIdElement) &&
                                chatIdElement.ValueKind != JsonValueKind.Null)
                            {
                                string chatIdStr = chatIdElement.ToString();

                                bool alreadyExists = false;
                                if (root.TryGetProperty("alreadyExists", out JsonElement alreadyExistsElement) &&
                                    alreadyExistsElement.ValueKind == JsonValueKind.True)
                                {
                                    alreadyExists = true;
                                }

                                if (Guid.TryParse(chatIdStr, out Guid chatId))
                                {
                                    if (alreadyExists)
                                    {
                                        _logger.LogInformation("Returning to existing chat {ChatId}", chatId);
                                        await _toastService.ShowToastAsync("Returning to existing conversation", ToastType.Info);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("New chat started with ID {ChatId}", chatId);
                                        await _toastService.ShowToastAsync("New conversation started", ToastType.Success);
                                    }

                                    return chatId;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing chat start response");

                        // Try an alternative parsing approach
                        try
                        {
                            // Try to extract directly from the string representation
                            var responseStr = response.ToString();
                            if (responseStr.Contains("chatId"))
                            {
                                // Look for "chatId":"GUID" pattern
                                int start = responseStr.IndexOf("chatId") + 9; // Length of "chatId":"
                                int end = responseStr.IndexOf("\"", start);
                                if (start > 9 && end > start)
                                {
                                    string chatIdStr = responseStr.Substring(start, end - start);
                                    if (Guid.TryParse(chatIdStr, out Guid chatId))
                                    {
                                        return chatId;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignore errors in this fallback attempt
                        }
                    }
                }

                _logger.LogWarning("Failed to start chat with user {UserId}", userId);
                await _toastService.ShowToastAsync("Unable to start chat. Please try again.", ToastType.Warning);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start chat with user {UserId}", userId);
                await _toastService.ShowToastAsync("Failed to start chat: " + ex.Message, ToastType.Error);
                return null;
            }
        }
    }
}