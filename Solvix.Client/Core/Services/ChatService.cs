using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly IApiService _apiService;
        private readonly ILogger<ChatService> _logger;
        private readonly IToastService _toastService;

        public ChatService(IApiService apiService, ILogger<ChatService> logger, IToastService toastService)
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
                _logger.LogInformation("Fetching messages for chat {ChatId} (Skip: {Skip}, Take: {Take})...", chatId, skip, take);
                var messages = await _apiService.GetAsync<List<MessageModel>>(endpoint, queryParams);
                _logger.LogInformation("Fetched {Count} messages for chat {ChatId}.", messages?.Count ?? 0, chatId);
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
                _logger.LogInformation("Fetched chat details for {ChatId}.", chatId);
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
                var result = await _apiService.PostAsync<dynamic>(Constants.Endpoints.StartChat, new { recipientUserId }); // ارسال به صورت anonymous object

                if (result != null && result.chatId != null)
                {
                    try
                    {
                        Guid chatIdResult = Guid.Parse(result.chatId.ToString());
                        bool alreadyExistsResult = bool.Parse(result.alreadyExists.ToString());
                        _logger.LogInformation("Chat started/found with user {RecipientUserId}. ChatId: {ChatId}, Existed: {AlreadyExists}", recipientUserId, chatIdResult, alreadyExistsResult);
                        return (chatIdResult, alreadyExistsResult);
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogError(parseEx, "Error parsing StartChat response object.");
                        await _toastService.ShowToastAsync("خطا در پردازش پاسخ سرور", ToastType.Error);
                        return (null, false);
                    }
                }
                else
                {
                    _logger.LogWarning("StartChat response was null or did not contain chatId.");
                    await _toastService.ShowToastAsync("پاسخ نامعتبر از سرور دریافت شد", ToastType.Error);
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
                var dto = new { ChatId = chatId, Content = content };
                _logger.LogInformation("Sending message to chat {ChatId}...", chatId);
                var message = await _apiService.PostAsync<MessageModel>(Constants.Endpoints.SendMessage, dto);
                _logger.LogInformation("Message sent response received for chat {ChatId}.", chatId);
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
                string endpoint = $"{Constants.Endpoints.MarkRead}/{chatId}/mark-read";
                _logger.LogInformation("Marking {Count} messages as read in chat {ChatId}...", messageIds.Count, chatId);
                await _apiService.PostAsync<object>(endpoint, messageIds);
                _logger.LogInformation("Messages marked as read in chat {ChatId}.", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("خطا در بروزرسانی وضعیت پیام‌ها", ToastType.Error);
            }
        }
    }
}