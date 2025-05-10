using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solvix.Client.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly IApiService _apiService;
        private readonly ILogger<ChatService> _logger;
        private readonly IToastService _toastService;

        // Cache for chats and messages
        private readonly Dictionary<Guid, ChatModel> _chatsCache = new();
        private readonly Dictionary<Guid, List<MessageModel>> _messagesCache = new();
        private DateTime? _lastChatsRefreshTime;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public ChatService(
            IApiService apiService,
            ILogger<ChatService> logger,
            IToastService toastService)
        {
            _apiService = apiService;
            _logger = logger;
            _toastService = toastService;
        }

        public async Task<List<ChatModel>?> GetUserChatsAsync(bool forceRefresh = false)
        {
            try
            {
                // Check cache validity
                if (!forceRefresh && _lastChatsRefreshTime.HasValue &&
                    DateTime.UtcNow - _lastChatsRefreshTime.Value < _cacheExpiration &&
                    _chatsCache.Any())
                {
                    _logger.LogInformation("Returning {Count} chats from cache", _chatsCache.Count);
                    return _chatsCache.Values.ToList();
                }

                _logger.LogInformation("Fetching user chats from server...");
                var chats = await _apiService.GetAsync<List<ChatModel>>(Constants.Endpoints.GetChats);

                if (chats != null && chats.Any())
                {
                    _logger.LogInformation("Fetched {Count} chats from server", chats.Count);

                    // Update cache
                    _chatsCache.Clear();
                    foreach (var chat in chats)
                    {
                        _chatsCache[chat.Id] = chat;
                    }
                    _lastChatsRefreshTime = DateTime.UtcNow;

                    return chats;
                }

                return chats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user chats");
                await _toastService.ShowToastAsync("خطا در دریافت لیست چت‌ها", ToastType.Error);

                // Return cached data if available
                if (_chatsCache.Any())
                {
                    return _chatsCache.Values.ToList();
                }

                return null;
            }
        }

        public async Task<List<MessageModel>?> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50, bool forceRefresh = false)
        {
            try
            {
                // Check cache for initial load
                if (!forceRefresh && skip == 0 && _messagesCache.TryGetValue(chatId, out var cachedMessages))
                {
                    _logger.LogInformation("Returning {Count} messages from cache for chat {ChatId}",
                        cachedMessages.Count, chatId);
                    return cachedMessages.Take(take).ToList();
                }

                string endpoint = $"{Constants.Endpoints.GetMessages}/{chatId}/messages";
                var queryParams = new Dictionary<string, string>
            {
                { "skip", skip.ToString() },
                { "take", take.ToString() }
            };

                _logger.LogInformation("Fetching messages for chat {ChatId} from server...", chatId);
                var messages = await _apiService.GetAsync<List<MessageModel>>(endpoint, queryParams);

                // Update cache for initial load
                if (messages != null && skip == 0)
                {
                    _messagesCache[chatId] = messages;
                }

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching messages for chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("خطا در دریافت پیام‌های چت", ToastType.Error);
                return null;
            }
        }


        // Method to update cache when new message arrives
        public void UpdateMessageCache(MessageModel message)
        {
            if (_messagesCache.TryGetValue(message.ChatId, out var messages))
            {
                var existingMessage = messages.FirstOrDefault(m => m.Id == message.Id);
                if (existingMessage == null)
                {
                    messages.Add(message);
                    messages.Sort((a, b) => a.SentAt.CompareTo(b.SentAt));
                }
                else
                {
                    // Update existing message
                    existingMessage.Content = message.Content;
                    existingMessage.Status = message.Status;
                    existingMessage.IsRead = message.IsRead;
                    existingMessage.ReadAt = message.ReadAt;
                }
            }
        }


        // Method to invalidate cache
        public void InvalidateCache(Guid? chatId = null)
        {
            if (chatId.HasValue)
            {
                _chatsCache.Remove(chatId.Value);
                _messagesCache.Remove(chatId.Value);
            }
            else
            {
                _chatsCache.Clear();
                _messagesCache.Clear();
                _lastChatsRefreshTime = null;
            }
        }

        public async Task<ChatModel?> GetChatByIdAsync(Guid chatId)
        {
            try
            {
                string endpoint = $"{Constants.Endpoints.GetChat}/{chatId}";
                _logger.LogInformation("Fetching chat details for {ChatId}...", chatId);

                var chat = await _apiService.GetAsync<ChatModel>(endpoint);

                if (chat != null)
                {
                    _logger.LogInformation("Fetched chat details for {ChatId}.", chatId);
                }
                else
                {
                    _logger.LogWarning("Chat with ID {ChatId} not found or access denied.", chatId);
                }

                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chat details for {ChatId}", chatId);
                await _toastService.ShowToastAsync("خطا در دریافت اطلاعات چت", ToastType.Error);
                return null;
            }
        }

        public async Task<(Guid? chatId, bool alreadyExists)> StartChatWithUserAsync(long recipientUserId)
        {
            try
            {
                _logger.LogInformation("Starting chat with user {RecipientUserId}...", recipientUserId);

                var response = await _apiService.PostAsync<StartChatResponseDto>(Constants.Endpoints.StartChat, recipientUserId);

                if (response != null)
                {
                    _logger.LogInformation("Chat started/found with user {RecipientUserId}. ChatId: {ChatId}, Existed: {AlreadyExists}",
                        recipientUserId, response.ChatId, response.AlreadyExists);
                    return (response.ChatId, response.AlreadyExists);
                }
                else
                {
                    _logger.LogWarning("StartChat response was null or did not contain expected data for user {RecipientUserId}.", recipientUserId);
                    await _toastService.ShowToastAsync("پاسخ نامعتبر از سرور برای شروع چت دریافت شد", ToastType.Error);
                    return (null, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat with user {RecipientUserId}", recipientUserId);
                await _toastService.ShowToastAsync("خطا در شروع چت جدید", ToastType.Error);
                return (null, false);
            }
        }

        public async Task<MessageModel?> SendMessageAsync(Guid chatId, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Attempted to send empty message to chat {ChatId}", chatId);
                    await _toastService.ShowToastAsync("متن پیام نمی‌تواند خالی باشد", ToastType.Warning);
                    return null;
                }

                var dto = new SendMessageDto
                {
                    ChatId = chatId,
                    Content = content
                };

                _logger.LogInformation("Sending message to chat {ChatId}...", chatId);

                var message = await _apiService.PostAsync<MessageModel>(Constants.Endpoints.SendMessage, dto);

                if (message != null)
                {
                    _logger.LogInformation("Message sent successfully to chat {ChatId}. Message ID: {MessageId}, Time: {SentAt}",
                        chatId, message.Id, message.SentAt);

                    // اطمینان از اینکه SentAt مقدار درستی دارد
                    if (message.SentAt == default)
                    {
                        message.SentAt = DateTime.UtcNow;
                        _logger.LogWarning("Message SentAt was default, setting to current UTC time: {Now}", message.SentAt);
                    }
                }
                else
                {
                    _logger.LogWarning("Received null response from server when sending message to chat {ChatId}",
                        chatId);
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("خطا در ارسال پیام", ToastType.Error);
                return null;
            }
        }

        public async Task MarkMessagesAsReadAsync(Guid chatId, List<int> messageIds)
        {
            try
            {
                if (messageIds == null || !messageIds.Any())
                {
                    _logger.LogWarning("Attempted to mark empty message list as read for chat {ChatId}", chatId);
                    return;
                }

                string endpoint = $"{Constants.Endpoints.MarkRead}/{chatId}/mark-read";

                _logger.LogInformation("Marking {Count} messages as read in chat {ChatId}...",
                    messageIds.Count, chatId);

                await _apiService.PostAsync<object>(endpoint, messageIds);

                _logger.LogInformation("Successfully marked {Count} messages as read in chat {ChatId}.",
                    messageIds.Count, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("خطا در بروزرسانی وضعیت پیام‌ها", ToastType.Error);
            }
        }
    }

    public class SendMessageDto
    {
        public Guid ChatId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}