using Solvix.Client.Core.Models;


namespace Solvix.Client.Core.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatModel>?> GetUserChatsAsync();
        Task<ChatModel?> GetChatByIdAsync(Guid chatId);
        Task<List<MessageModel>?> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50);
        Task<(Guid? chatId, bool alreadyExists)> StartChatWithUserAsync(long recipientUserId);
        Task<MessageModel?> SendMessageAsync(Guid chatId, string content);
        Task MarkMessagesAsReadAsync(Guid chatId, List<int> messageIds);
    }
}