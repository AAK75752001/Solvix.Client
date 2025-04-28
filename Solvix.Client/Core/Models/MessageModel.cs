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
                    // Get the current user ID from secure storage
                    var currentUserIdTask = SecureStorage.GetAsync(Constants.StorageKeys.UserId);

                    // If the task is already completed, get the result directly
                    if (currentUserIdTask.IsCompleted)
                    {
                        var currentUserId = currentUserIdTask.Result;
                        return !string.IsNullOrEmpty(currentUserId) &&
                               long.TryParse(currentUserId, out var userId) &&
                               userId == SenderId;
                    }

                    // If the task is not completed yet, we have to make a synchronous wait
                    // This is not ideal but necessary for this property
                    var timeoutTask = Task.Delay(500); // 500ms timeout
                    if (Task.WhenAny(currentUserIdTask, timeoutTask).Result == currentUserIdTask)
                    {
                        var currentUserId = currentUserIdTask.Result;
                        return !string.IsNullOrEmpty(currentUserId) &&
                               long.TryParse(currentUserId, out var userId) &&
                               userId == SenderId;
                    }

                    // If we timed out, return false (safer default)
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
        public string TimeText
        {
            get
            {
                return SentAt.ToString("HH:mm");
            }
        }

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