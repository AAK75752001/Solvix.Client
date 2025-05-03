using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatModel>> GetChatsAsync();
        Task<ChatModel?> GetChatAsync(Guid chatId);
        Task<Guid?> StartChatAsync(long userId);
        Task<List<MessageModel>> GetMessagesAsync(Guid chatId, int skip = 0, int take = 50);
        Task<MessageModel?> SendMessageAsync(Guid chatId, string content);
        Task<MessageModel?> SendMessageWithCorrelationAsync(Guid chatId, string content, string correlationId);
        Task MarkAsReadAsync(Guid chatId, List<int> messageIds);
        Task<long> GetCurrentUserIdAsync();
    }
}