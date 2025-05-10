using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatModel>?> GetUserChatsAsync(bool forceRefresh = false);
        Task<List<MessageModel>?> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50, bool forceRefresh = false);
        Task<ChatModel?> GetChatByIdAsync(Guid chatId);
        Task<(Guid? chatId, bool alreadyExists)> StartChatWithUserAsync(long recipientUserId);
        Task<MessageModel?> SendMessageAsync(Guid chatId, string content);
        Task MarkMessagesAsReadAsync(Guid chatId, List<int> messageIds);

        // Add these methods
        void UpdateMessageCache(MessageModel message);
        void InvalidateCache(Guid? chatId = null);
        void UpdateChatCache(Guid chatId, string lastMessage, DateTime lastMessageTime, bool incrementUnread = false);
    }
}