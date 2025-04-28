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
        }

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

                    // Log participant online status
                    foreach (var participant in chat.Participants)
                    {
                        _logger.LogDebug("Participant {UserId} ({Username}): IsOnline = {IsOnline}",
                            participant.Id, participant.Username, participant.IsOnline);
                    }

                    // Initialize computed properties
                    chat.InitializeComputedProperties();

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
                        _logger.LogInformation("Using cached messages for chat {ChatId}", chatId);
                        return cachedMessages;
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

                if (messages != null)
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
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Attempted to send empty message to chat {ChatId}", chatId);
                    await _toastService.ShowToastAsync("Cannot send empty message", ToastType.Warning);
                    return null;
                }

                _logger.LogInformation("Sending message to chat {ChatId}", chatId);

                // Create DTO for API call
                var dto = new SendMessageDto
                {
                    ChatId = chatId,
                    Content = content
                };

                // First send via SignalR for real-time delivery if connected
                await _signalRService.SendMessageAsync(chatId, content);

                // Then make the API call to ensure persistence
                var response = await _apiService.PostAsync<MessageModel>(Constants.Endpoints.SendMessage, dto);

                if (response != null)
                {
                    _logger.LogInformation("Message sent successfully to chat {ChatId}, server ID: {MessageId}",
                        chatId, response.Id);

                    // Get current user ID for message ownership
                    var currentUserId = await GetCurrentUserIdAsync();

                    // Populate extra properties for UI
                    response.Status = Constants.MessageStatus.Sent;
                    response.SentAtFormatted = FormatMessageTime(response.SentAt);

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
            var now = DateTime.Now;
            var messageTime = dateTime.ToLocalTime(); // Convert UTC to local time

            // Today, show only time
            if (messageTime.Date == now.Date)
            {
                return messageTime.ToString("HH:mm");
            }
            // Yesterday
            else if (messageTime.Date == now.Date.AddDays(-1))
            {
                return "Yesterday " + messageTime.ToString("HH:mm");
            }
            // Within the last week
            else if ((now.Date - messageTime.Date).TotalDays < 7)
            {
                return messageTime.ToString("ddd HH:mm"); // Day of week + time
            }
            // Older messages
            else
            {
                return messageTime.ToString("yyyy-MM-dd HH:mm");
            }
        }

        #endregion
    }
}