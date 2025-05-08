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

        public ChatService(
            IApiService apiService,
            ILogger<ChatService> logger,
            IToastService toastService)
        {
            _apiService = apiService;
            _logger = logger;
            _toastService = toastService;
        }

        public async Task<List<ChatModel>?> GetUserChatsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching user chats...");
                var chats = await _apiService.GetAsync<List<ChatModel>>(Constants.Endpoints.GetChats);
                _logger.LogInformation("Fetched {Count} chats.", chats?.Count ?? 0);
                return chats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user chats.");
                await _toastService.ShowToastAsync("خطا در دریافت لیست چت‌ها", ToastType.Error);
                return null;
            }
        }

        public async Task<List<MessageModel>?> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50)
        {
            try
            {
                string endpoint = $"{Constants.Endpoints.GetMessages}/{chatId}/messages";
                var queryParams = new Dictionary<string, string>
                {
                    { "skip", skip.ToString() },
                    { "take", take.ToString() }
                };

                _logger.LogInformation("Fetching messages for chat {ChatId} (Skip: {Skip}, Take: {Take})...",
                    chatId, skip, take);

                var messages = await _apiService.GetAsync<List<MessageModel>>(endpoint, queryParams);

                _logger.LogInformation("Fetched {Count} messages for chat {ChatId}.",
                    messages?.Count ?? 0, chatId);

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching messages for chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("خطا در دریافت پیام‌های چت", ToastType.Error);
                return null;
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