using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Text.Json;

namespace Solvix.Client.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly IApiService _apiService;
        private readonly IToastService _toastService;
        private readonly ISignalRService _signalRService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IApiService apiService,
            IToastService toastService,
            ISignalRService signalRService,
            ILogger<ChatService> logger)
        {
            _apiService = apiService;
            _toastService = toastService;
            _signalRService = signalRService;
            _logger = logger;
        }

        public async Task<List<ChatModel>> GetChatsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching chats");

#if DEBUG
                if (Constants.BaseApiUrl.Contains("localhost"))
                {
                    return GenerateMockChats();
                }
#endif

                var response = await _apiService.GetAsync<List<ChatModel>>(Constants.Endpoints.GetChats);

                if (response == null)
                {
                    _logger.LogWarning("GetChatsAsync returned null");
                    await _toastService.ShowToastAsync("Unable to load chats. Please try again later.", ToastType.Warning);
                    return new List<ChatModel>();
                }

                // اطمینان حاصل کنیم که هر چت valid است و آماده‌سازی خصوصیت‌های محاسباتی
                foreach (var chat in response)
                {
                    if (chat != null)
                    {
                        // مطمئن شوید که هر چت حداقل یک لیست Participants و Messages خالی دارد، نه null
                        chat.Participants = chat.Participants ?? new List<UserModel>();
                        chat.Messages = chat.Messages ?? new System.Collections.ObjectModel.ObservableCollection<MessageModel>();
                    }
                }

                _logger.LogInformation("Successfully retrieved {Count} chats", response.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chats");
                await _toastService.ShowToastAsync("Failed to load chats: " + ex.Message, ToastType.Error);

                // برگرداندن لیست خالی برای جلوگیری از خطای null
                return new List<ChatModel>();
            }
        }

        public async Task<ChatModel?> GetChatAsync(Guid chatId)
        {
            try
            {
                _logger.LogInformation("Fetching chat {ChatId}", chatId);

                // Use mock data temporarily for testing UI
#if DEBUG
                if (Constants.BaseApiUrl.Contains("localhost"))
                {
                    var mockChat = GenerateMockChat(chatId);
                    return mockChat;
                }
#endif

                var endpoint = $"{Constants.Endpoints.GetChat}/{chatId}";
                var chat = await _apiService.GetAsync<ChatModel>(endpoint);

                if (chat != null)
                {
                    // Load messages for this chat
                    var messages = await GetMessagesAsync(chatId);

                    // Add messages to the chat
                    foreach (var message in messages)
                    {
                        chat.Messages.Add(message);
                    }

                    _logger.LogInformation("Successfully retrieved chat {ChatId} with {MessageCount} messages",
                        chatId, chat.Messages.Count);
                }
                else
                {
                    _logger.LogWarning("GetChatAsync returned null for chat {ChatId}", chatId);
                }

                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("Failed to load chat: " + ex.Message, ToastType.Error);
                return null;
            }
        }

        public async Task<Guid?> StartChatAsync(long userId)
        {
            try
            {
                _logger.LogInformation("Starting chat with user {UserId}", userId);

#if DEBUG
                if (Constants.BaseApiUrl.Contains("localhost"))
                {
                    return Guid.NewGuid();
                }
#endif

                var response = await _apiService.PostAsync<dynamic>(Constants.Endpoints.StartChat, userId);

                if (response != null)
                {
                    try
                    {
                        // Deserializar la respuesta como string y luego usar JsonDocument para parsearla
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
                        _logger.LogError(ex, "Error parsing chat start response for user {UserId}", userId);

                        // Intenta otra aproximación
                        try
                        {
                            // Intenta extraer directamente desde el response como string
                            var responseStr = response.ToString();
                            if (responseStr.Contains("chatId"))
                            {
                                // Busca el formato "chatId":"GUID"
                                int start = responseStr.IndexOf("chatId") + 9; // Longitud de "chatId":"
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
                            // Ignora errores en este intento de fallback
                        }

                        await _toastService.ShowToastAsync("Error starting chat: " + ex.Message, ToastType.Error);
                        return null;
                    }
                }

                _logger.LogWarning("StartChatAsync returned null");
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
                _logger.LogInformation("Fetching messages for chat {ChatId}, skip={Skip}, take={Take}",
                    chatId, skip, take);

                // Use mock data temporarily for testing UI
#if DEBUG
                if (Constants.BaseApiUrl.Contains("localhost"))
                {
                    return GenerateMockMessages(chatId, take);
                }
#endif

                var endpoint = $"{Constants.Endpoints.GetMessages}/{chatId}/messages";
                var queryParams = new Dictionary<string, string>
                {
                    { "skip", skip.ToString() },
                    { "take", take.ToString() }
                };

                var messages = await _apiService.GetAsync<List<MessageModel>>(endpoint, queryParams);

                if (messages != null)
                {
                    _logger.LogInformation("Successfully retrieved {Count} messages for chat {ChatId}",
                        messages.Count, chatId);

                    // Set the initial status for all messages
                    foreach (var message in messages)
                    {
                        if (message.IsOwnMessage)
                        {
                            // For own messages
                            message.Status = message.IsRead ? Constants.MessageStatus.Read :
                                Constants.MessageStatus.Delivered;
                        }
                    }

                    return messages;
                }
                else
                {
                    _logger.LogWarning("GetMessagesAsync returned null for chat {ChatId}", chatId);
                    return new List<MessageModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load messages for chat {ChatId}", chatId);
                await _toastService.ShowToastAsync("Failed to load messages: " + ex.Message, ToastType.Error);
                return new List<MessageModel>();
            }
        }

        public async Task<MessageModel?> SendMessageAsync(Guid chatId, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Attempting to send empty message to chat {ChatId}", chatId);
                    await _toastService.ShowToastAsync("Cannot send empty message", ToastType.Warning);
                    return null;
                }

                _logger.LogInformation("Sending message to chat {ChatId}", chatId);

                // Create a pending message with local id
                var pendingMessage = new MessageModel
                {
                    Id = -1, // Temporary ID
                    ChatId = chatId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    Status = Constants.MessageStatus.Sending
                };

                // Use mock data temporarily for testing UI
#if DEBUG
                if (Constants.BaseApiUrl.Contains("localhost"))
                {
                    pendingMessage.Id = new Random().Next(1000, 9999);
                    pendingMessage.SenderId = 1; // Current user ID
                    pendingMessage.SenderName = "You";
                    pendingMessage.Status = Constants.MessageStatus.Sent;

                    // Simulate server response after a delay
                    await Task.Delay(500);
                    pendingMessage.Status = Constants.MessageStatus.Delivered;

                    return pendingMessage;
                }
#endif

                // Send via SignalR for real-time delivery
                await _signalRService.SendMessageAsync(chatId, content);

                // Also send via REST API as a backup
                var dto = new SendMessageDto
                {
                    ChatId = chatId,
                    Content = content
                };

                var response = await _apiService.PostAsync<MessageModel>(Constants.Endpoints.SendMessage, dto);

                if (response != null)
                {
                    _logger.LogInformation("Message sent successfully to chat {ChatId}, received server ID {MessageId}",
                        chatId, response.Id);

                    // Update the status of the pending message
                    pendingMessage.Id = response.Id;
                    pendingMessage.Status = Constants.MessageStatus.Sent;
                    pendingMessage.SenderId = response.SenderId;
                    pendingMessage.SenderName = response.SenderName;

                    return pendingMessage;
                }
                else
                {
                    _logger.LogWarning("SendMessageAsync returned null for chat {ChatId}", chatId);

                    // Mark as failed if the API call failed
                    pendingMessage.Status = Constants.MessageStatus.Failed;
                    await _toastService.ShowToastAsync("Message could not be sent. Please try again.", ToastType.Warning);
                    return pendingMessage;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to chat {ChatId}", chatId);
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

                // Use mock data temporarily for testing UI
#if DEBUG
                if (Constants.BaseApiUrl.Contains("localhost"))
                {
                    // Simulate server delay
                    await Task.Delay(300);
                    return;
                }
#endif

                var endpoint = $"{Constants.Endpoints.MarkRead}/{chatId}/mark-read";
                await _apiService.PostAsync<object>(endpoint, messageIds);

                // Also mark via SignalR for real-time updates
                await _signalRService.MarkMessagesAsReadAsync(messageIds);

                _logger.LogInformation("Successfully marked messages as read in chat {ChatId}", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark messages as read in chat {ChatId}", chatId);
                // Don't show toast here as this is background operation
            }
        }

        // Helper methods for generating mock data
#if DEBUG
        private List<ChatModel> GenerateMockChats()
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
                    }
                };

                mockChats.Add(mockChat);
            }

            // Sort by last message time
            mockChats = mockChats.OrderByDescending(c => c.LastMessageTime).ToList();

            return mockChats;
        }

        private ChatModel GenerateMockChat(Guid chatId)
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
                Messages = new System.Collections.ObjectModel.ObservableCollection<MessageModel>(GenerateMockMessages(chatId, 20))
            };

            return mockChat;
        }

        private List<MessageModel> GenerateMockMessages(Guid chatId, int count)
        {
            var random = new Random();
            var messages = new List<MessageModel>();
            var currentTime = DateTime.UtcNow;

            // Mock conversation starters
            var conversationStarters = new List<(string message, bool isFromCurrentUser)>
            {
                ("Hey, how are you doing?", false),
                ("Hi! I'm good, thanks. How about you?", true),
                ("Not bad. Did you check out the new project requirements?", false),
                ("Yes, I did. Looks interesting but challenging.", true),
                ("I agree. When can we meet to discuss it?", false),
                ("How about tomorrow afternoon?", true),
                ("Works for me. Around 2 PM?", false),
                ("Perfect! See you then.", true),
                ("Great! I'll prepare some notes.", false),
                ("Sounds good. I'll do the same.", true)
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
                    Status = Constants.MessageStatus.Read
                };

                messages.Add(message);
            }

            // If we need more messages, add some randomized ones
            if (count > initialMessageCount)
            {
                var additionalMessages = new List<string>
                {
                    "What do you think about the design?",
                    "I like it, but we might need some adjustments.",
                    "Did you get my email?",
                    "Yes, I'll respond shortly.",
                    "Just checking in - how's it going?",
                    "Making good progress on the task.",
                    "Let me know if you need any help.",
                    "Thanks, I appreciate that.",
                    "Do we have a meeting scheduled for next week?",
                    "Yes, it's on Tuesday at 10 AM."
                };

                for (int i = initialMessageCount; i < count; i++)
                {
                    var isFromCurrentUser = i % 2 == 0;
                    var messageIndex = random.Next(additionalMessages.Count);
                    var messageTime = currentTime.AddHours(-count + i);

                    var message = new MessageModel
                    {
                        Id = i + 1,
                        Content = additionalMessages[messageIndex],
                        SentAt = messageTime,
                        SenderId = isFromCurrentUser ? 1 : 2,
                        SenderName = isFromCurrentUser ? "You" : "John",
                        ChatId = chatId,
                        IsRead = true,
                        Status = Constants.MessageStatus.Read
                    };

                    messages.Add(message);
                }
            }

            return messages;
        }
#endif
    }
}