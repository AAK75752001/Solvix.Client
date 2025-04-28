using System.Text.Json.Serialization;

namespace Solvix.Client.Core.Models
{
    public class MessageModel
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public long SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public Guid ChatId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }

        // Local properties for UI
        [JsonIgnore]
        public int Status { get; set; } = Constants.MessageStatus.Sending;

        [JsonIgnore]
        public string SentAtFormatted { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsSent => Status >= Constants.MessageStatus.Sent;

        [JsonIgnore]
        public bool IsDelivered => Status >= Constants.MessageStatus.Delivered;

        [JsonIgnore]
        public bool IsReadByReceiver => Status >= Constants.MessageStatus.Read;

        [JsonIgnore]
        public bool IsFailed => Status == Constants.MessageStatus.Failed;

        [JsonIgnore]
        public bool IsOwnMessage
        {
            get
            {
                try
                {
                    var currentUserIdTask = SecureStorage.GetAsync(Constants.StorageKeys.UserId);

                    if (currentUserIdTask.IsCompleted)
                    {
                        var currentUserId = currentUserIdTask.Result;
                        return !string.IsNullOrEmpty(currentUserId) &&
                               long.TryParse(currentUserId, out var userId) &&
                               userId == SenderId;
                    }

                    // If the task is not completed yet, we have a small timeout
                    var timeoutTask = Task.Delay(300); // 300ms timeout
                    if (Task.WhenAny(currentUserIdTask, timeoutTask).Result == currentUserIdTask)
                    {
                        var currentUserId = currentUserIdTask.Result;
                        return !string.IsNullOrEmpty(currentUserId) &&
                               long.TryParse(currentUserId, out var userId) &&
                               userId == SenderId;
                    }

                    // Default to false if we can't determine
                    return false;
                }
                catch
                {
                    // In case of any error, default to false
                    return false;
                }
            }
        }

        [JsonIgnore]
        public string TimeText => string.IsNullOrEmpty(SentAtFormatted)
            ? SentAt.ToString("HH:mm")
            : SentAtFormatted;

        [JsonIgnore]
        public string StatusIcon
        {
            get
            {
                if (IsFailed)
                    return "\ue000"; // error

                if (IsReadByReceiver)
                    return "\ue8f0"; // done_all (filled)

                if (IsDelivered)
                    return "\ue5ca"; // done_all (outline)

                if (IsSent)
                    return "\ue5ca"; // done (outline)

                return "\ue192"; // watch_later
            }
        }
    }

    public class SendMessageDto
    {
        public Guid ChatId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}