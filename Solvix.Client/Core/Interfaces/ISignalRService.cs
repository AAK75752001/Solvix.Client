using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Interfaces
{
    public interface ISignalRService
    {
        bool IsConnected { get; }

        event Action<MessageModel>? OnMessageReceived;
        event Action<Guid, int, int>? OnMessageStatusUpdated;
        event Action<long, bool>? OnUserStatusChanged;
        event Action<bool>? OnConnectionStateChanged;
        event Action<Guid, long, bool>? OnUserTyping;
        event Action<string, int>? OnMessageCorrelationConfirmation;

        Task StartAsync();
        Task StopAsync(bool autoReconnect = true);
        Task<bool> SendMessageAsync(MessageModel message);
        Task MarkAsReadAsync(Guid chatId, int messageId);
        Task TypingAsync(Guid chatId, bool isTyping);
    }
}
