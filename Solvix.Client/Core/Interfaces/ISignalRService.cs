using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Interfaces
{
    public interface ISignalRService
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        Task SendMessageAsync(Guid chatId, string message);
        Task SendMessageWithCorrelationAsync(Guid chatId, string message, string correlationId);
        Task MarkMessageAsReadAsync(int messageId);
        Task MarkMessagesAsReadAsync(List<int> messageIds);
        Task ClearMessageTrackingAsync();
        void SetShowConnectionErrors(bool show);
        Task HandleAppSleepAsync();
        Task HandleAppResumeAsync();

        event Action<MessageModel> OnMessageReceived;
        event Action<Guid, int> OnMessageRead;
        event Action<string> OnError;
        event Action<long, bool, DateTime?> OnUserStatusChanged;
        event Action<int> OnMessageConfirmed;
        event Action<string, int> OnMessageCorrelationConfirmed;
    }
}