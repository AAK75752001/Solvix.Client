using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Solvix.Client.Core.Services
{
    public class DiagnosticChatService : IChatService
    {
        private readonly IChatService _originalService;
        private readonly IToastService _toastService;
        private readonly ILogger<DiagnosticChatService> _logger;
        private readonly IAuthService _authService;

        public DiagnosticChatService(
            ChatService originalService,
            IToastService toastService,
            ILogger<DiagnosticChatService> logger,
            IAuthService authService)
        {
            _originalService = originalService;
            _toastService = toastService;
            _logger = logger;
            _authService = authService;
        }

        public async Task<long> GetCurrentUserIdAsync()
        {
            try
            {
                _logger.LogInformation("DiagnosticChatService: Getting current user ID");
                var userId = await _originalService.GetCurrentUserIdAsync();
                _logger.LogInformation("DiagnosticChatService: Current user ID is {UserId}", userId);
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error getting current user ID");
                await _toastService.ShowToastAsync($"Error getting user ID: {ex.Message}", ToastType.Error);
                return 0;
            }
        }

        public async Task<List<ChatModel>> GetChatsAsync()
        {
            try
            {
                _logger.LogInformation("DiagnosticChatService: Getting chats");
                var chats = await _originalService.GetChatsAsync();
                _logger.LogInformation("DiagnosticChatService: Got {Count} chats", chats?.Count ?? 0);

                if (chats == null || chats.Count == 0)
                {
                    _logger.LogWarning("DiagnosticChatService: No chats returned, using mock data");
                    await _toastService.ShowToastAsync("No chats found, using mock data", ToastType.Warning);
                    return CreateMockChats();
                }

                return chats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error getting chats");
                await _toastService.ShowToastAsync($"Error loading chats: {ex.Message}", ToastType.Error);
                return CreateMockChats();
            }
        }

        public async Task<ChatModel?> GetChatAsync(Guid chatId)
        {
            _logger.LogInformation("DiagnosticChatService: Getting chat {ChatId}", chatId);

            try
            {
                var chat = await _originalService.GetChatAsync(chatId);

                if (chat != null)
                {
                    _logger.LogInformation("DiagnosticChatService: Successfully got chat {ChatId}", chatId);
                    return chat;
                }
                else
                {
                    _logger.LogWarning("DiagnosticChatService: Chat {ChatId} not found, creating mock chat", chatId);
                    await _toastService.ShowToastAsync($"Chat not found, using mock data", ToastType.Warning);
                    return CreateMockChat(chatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error getting chat {ChatId}", chatId);
                await _toastService.ShowToastAsync($"Error loading chat: {ex.Message}", ToastType.Error);
                return CreateMockChat(chatId);
            }
        }

        public async Task<Guid?> StartChatAsync(long userId)
        {
            try
            {
                _logger.LogInformation("DiagnosticChatService: Starting chat with user {UserId}", userId);
                var chatId = await _originalService.StartChatAsync(userId);

                if (chatId.HasValue)
                {
                    _logger.LogInformation("DiagnosticChatService: Started chat {ChatId} with user {UserId}", chatId, userId);
                    return chatId;
                }
                else
                {
                    _logger.LogWarning("DiagnosticChatService: Failed to start chat with user {UserId}, returning mock chat ID", userId);
                    return Guid.NewGuid();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error starting chat with user {UserId}", userId);
                await _toastService.ShowToastAsync($"Error starting chat: {ex.Message}", ToastType.Error);
                return Guid.NewGuid();
            }
        }

        public async Task<List<MessageModel>> GetMessagesAsync(Guid chatId, int skip = 0, int take = 50)
        {
            try
            {
                _logger.LogInformation("DiagnosticChatService: Getting messages for chat {ChatId}", chatId);
                var messages = await _originalService.GetMessagesAsync(chatId, skip, take);

                _logger.LogInformation("DiagnosticChatService: Got {Count} messages for chat {ChatId}",
                    messages?.Count ?? 0, chatId);

                if (messages == null || messages.Count == 0)
                {
                    _logger.LogWarning("DiagnosticChatService: No messages for chat {ChatId}, using mock data", chatId);
                    await _toastService.ShowToastAsync("No messages found, using mock data", ToastType.Warning);
                    return CreateMockMessages(chatId, 10);
                }

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error getting messages for chat {ChatId}", chatId);
                await _toastService.ShowToastAsync($"Error loading messages: {ex.Message}", ToastType.Error);
                return CreateMockMessages(chatId, 10);
            }
        }

        public async Task<MessageModel?> SendMessageAsync(Guid chatId, string content)
        {
            try
            {
                _logger.LogInformation("DiagnosticChatService: Sending message to chat {ChatId}: {Content}",
                    chatId, content);

                var message = await _originalService.SendMessageAsync(chatId, content);

                if (message != null)
                {
                    _logger.LogInformation("DiagnosticChatService: Message sent to chat {ChatId}, ID: {MessageId}",
                        chatId, message.Id);
                    return message;
                }
                else
                {
                    _logger.LogWarning("DiagnosticChatService: Failed to send message to chat {ChatId}, using mock message", chatId);
                    await _toastService.ShowToastAsync("Failed to send message through API, showing mock message", ToastType.Warning);

                    return new MessageModel
                    {
                        Id = new Random().Next(1000, 9999),
                        ChatId = chatId,
                        Content = content,
                        SentAt = DateTime.UtcNow,
                        SenderId = await GetCurrentUserIdAsync(),
                        SenderName = "You (Mock)",
                        Status = Constants.MessageStatus.Sent,
                        SentAtFormatted = DateTime.UtcNow.ToString("HH:mm")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error sending message to chat {ChatId}", chatId);
                await _toastService.ShowToastAsync($"Error sending message: {ex.Message}", ToastType.Error);

                return new MessageModel
                {
                    Id = new Random().Next(1000, 9999),
                    ChatId = chatId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    SenderId = await GetCurrentUserIdAsync(),
                    SenderName = "You (Mock)",
                    Status = Constants.MessageStatus.Sent,
                    SentAtFormatted = DateTime.UtcNow.ToString("HH:mm")
                };
            }
        }

        public async Task<MessageModel?> SendMessageWithCorrelationAsync(Guid chatId, string content, string correlationId)
        {
            try
            {
                _logger.LogInformation("DiagnosticChatService: Sending message to chat {ChatId} with correlationId {CorrelationId}",
                    chatId, correlationId);

                var message = await _originalService.SendMessageWithCorrelationAsync(chatId, content, correlationId);

                if (message != null)
                {
                    _logger.LogInformation("DiagnosticChatService: Message sent successfully with server ID: {MessageId}",
                        message.Id);
                    return message;
                }
                else
                {
                    _logger.LogWarning("DiagnosticChatService: Failed to send message via API with correlationId {CorrelationId}",
                        correlationId);
                    await _toastService.ShowToastAsync("Failed to send message through API, showing mock message", ToastType.Warning);

                    return new MessageModel
                    {
                        Id = new Random().Next(1000, 9999),
                        ChatId = chatId,
                        Content = content,
                        SentAt = DateTime.UtcNow,
                        SenderId = await GetCurrentUserIdAsync(),
                        SenderName = "You (Mock)",
                        Status = Constants.MessageStatus.Sent,
                        SentAtFormatted = DateTime.UtcNow.ToString("HH:mm"),
                        CorrelationId = correlationId
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error sending message with correlationId {CorrelationId}",
                    correlationId);
                await _toastService.ShowToastAsync($"Error sending message: {ex.Message}", ToastType.Error);

                return new MessageModel
                {
                    Id = new Random().Next(1000, 9999),
                    ChatId = chatId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    SenderId = await GetCurrentUserIdAsync(),
                    SenderName = "You (Mock)",
                    Status = Constants.MessageStatus.Sent,
                    SentAtFormatted = DateTime.UtcNow.ToString("HH:mm"),
                    CorrelationId = correlationId
                };
            }
        }


        public async Task MarkAsReadAsync(Guid chatId, List<int> messageIds)
        {
            try
            {
                _logger.LogInformation("DiagnosticChatService: Marking {Count} messages as read in chat {ChatId}",
                    messageIds.Count, chatId);
                await _originalService.MarkAsReadAsync(chatId, messageIds);
                _logger.LogInformation("DiagnosticChatService: Successfully marked messages as read");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiagnosticChatService: Error marking messages as read in chat {ChatId}", chatId);
                // Don't show toast here as this is a background operation
            }
        }

        #region Mock Data Creation

        private List<ChatModel> CreateMockChats()
        {
            var random = new Random();
            var mockChats = new List<ChatModel>();

            // Create some mock users
            var users = new List<UserModel>
            {
                new UserModel { Id = 2, FirstName = "John", LastName = "Doe", PhoneNumber = "09123456789", IsOnline = true },
                new UserModel { Id = 3, FirstName = "Jane", LastName = "Smith", PhoneNumber = "09187654321", IsOnline = false, LastActive = DateTime.UtcNow.AddHours(-2) },
                new UserModel { Id = 4, FirstName = "Mike", LastName = "Johnson", PhoneNumber = "09123123123", IsOnline = true },
                new UserModel { Id = 5, FirstName = "Sarah", LastName = "Williams", PhoneNumber = "09456456456", IsOnline = false, LastActive = DateTime.UtcNow.AddDays(-1) }
            };

            // Create mock chats with these users
            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                var chatId = Guid.NewGuid();

                var lastMessageTime = i == 0 || i == 2
                    ? DateTime.UtcNow.AddMinutes(-random.Next(5, 60))
                    : DateTime.UtcNow.AddDays(-random.Next(1, 5));

                var mockChat = new ChatModel
                {
                    Id = chatId,
                    IsGroup = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                    LastMessage = i % 2 == 0 ? "Hey, how are you doing?" : "Can we meet tomorrow?",
                    LastMessageTime = lastMessageTime,
                    UnreadCount = i % 2 == 0 ? random.Next(0, 5) : 0,
                    Participants = new List<UserModel>
                    { 
                        // Add current user
                        new UserModel { Id = 1, FirstName = "Current", LastName = "User", PhoneNumber = "09111222333", IsOnline = true },
                        // Add the chat participant
                        user
                    },
                    Messages = new ObservableCollection<MessageModel>(CreateMockMessages(chatId, 5))
                };

                mockChats.Add(mockChat);
            }

            // Initialize computed properties
            foreach (var chat in mockChats)
            {
                chat.InitializeComputedProperties();
            }

            return mockChats;
        }

        private ChatModel CreateMockChat(Guid chatId)
        {
            var random = new Random();

            // Create a mock user
            var user = new UserModel
            {
                Id = 2,
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "09123456789",
                IsOnline = true
            };

            var mockChat = new ChatModel
            {
                Id = chatId,
                IsGroup = false,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                LastMessage = "Hey, how are you doing?",
                LastMessageTime = DateTime.UtcNow.AddMinutes(-random.Next(5, 60)),
                UnreadCount = 0,
                Participants = new List<UserModel>
                { 
                    // Add current user
                    new UserModel { Id = 1, FirstName = "Current", LastName = "User", PhoneNumber = "09111222333", IsOnline = true },
                    // Add the chat participant
                    user
                },
                Messages = new ObservableCollection<MessageModel>(CreateMockMessages(chatId, 15))
            };

            // Initialize computed properties
            mockChat.InitializeComputedProperties();

            return mockChat;
        }

        private List<MessageModel> CreateMockMessages(Guid chatId, int count)
        {
            var random = new Random();
            var messages = new List<MessageModel>();
            var currentTime = DateTime.Now;

            // Mock conversation starters
            var conversationStarters = new List<(string message, bool isFromCurrentUser)>
            {
                ("سلام، حالت چطوره؟", false),
                ("سلام! خوبم، ممنون. تو چطوری؟", true),
                ("منم خوبم. درباره پروژه جدید چه فکری می‌کنی؟", false),
                ("به نظر جالب میاد ولی چالش برانگیزه.", true),
                ("موافقم. کی می‌تونیم درباره‌اش صحبت کنیم؟", false),
                ("فردا بعدازظهر چطوره؟", true),
                ("خوبه. ساعت 2 بعدازظهر؟", false),
                ("عالیه! می‌بینمت.", true),
                ("حتماً. یادداشت برمی‌دارم.", false),
                ("منم همینطور.", true)
            };

            // Add some initial messages based on conversation starters
            var initialMessageCount = Math.Min(conversationStarters.Count, count);
            for (int i = 0; i < initialMessageCount; i++)
            {
                var (content, isFromCurrentUser) = conversationStarters[i];
                var messageTime = currentTime.AddMinutes(-initialMessageCount + i);

                var message = new MessageModel
                {
                    Id = i + 1,
                    Content = content,
                    SentAt = messageTime,
                    SenderId = isFromCurrentUser ? 1 : 2, // Current user has ID 1, other participant has ID 2
                    SenderName = isFromCurrentUser ? "You" : "John",
                    ChatId = chatId,
                    IsRead = true,
                    Status = Constants.MessageStatus.Read,
                    SentAtFormatted = messageTime.ToString("HH:mm")
                };

                messages.Add(message);
            }

            return messages;
        }

        #endregion
    }
}