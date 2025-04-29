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
        private bool? _isOwnMessage;

        // Local properties for UI
        [JsonIgnore]
        public DateTime LocalSentAt => SentAt.ToLocalTime();

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
                if (_isOwnMessage.HasValue)
                    return _isOwnMessage.Value;

                try
                {
                    var currentUserIdTask = SecureStorage.GetAsync(Constants.StorageKeys.UserId);

                    if (currentUserIdTask.IsCompleted)
                    {
                        var currentUserId = currentUserIdTask.Result;
                        _isOwnMessage = !string.IsNullOrEmpty(currentUserId) &&
                                       long.TryParse(currentUserId, out var userId) &&
                                       userId == SenderId;
                        return _isOwnMessage.Value;
                    }

                    // If the task is not completed yet, we have a small timeout
                    var timeoutTask = Task.Delay(300); // 300ms timeout
                    Task.WhenAny(currentUserIdTask, timeoutTask).Wait(); // Use Wait here to be synchronous

                    if (currentUserIdTask.IsCompleted)
                    {
                        var currentUserId = currentUserIdTask.Result;
                        _isOwnMessage = !string.IsNullOrEmpty(currentUserId) &&
                                       long.TryParse(currentUserId, out var userId) &&
                                       userId == SenderId;
                        return _isOwnMessage.Value;
                    }

                    // Default to false if we can't determine
                    _isOwnMessage = false;
                    return false;
                }
                catch
                {
                    // In case of any error, default to false
                    _isOwnMessage = false;
                    return false;
                }
            }
            set => _isOwnMessage = value;
        }

        [JsonIgnore]
        public string TimeText
        {
            get
            {
                if (!string.IsNullOrEmpty(SentAtFormatted))
                    return SentAtFormatted;

                try
                {
                    // زمان سرور را به زمان محلی تبدیل می‌کنیم
                    var localTime = LocalSentAt;
                    return localTime.ToString("HH:mm");
                }
                catch
                {
                    // در صورت خطا از فرمت ساده استفاده می‌کنیم
                    return SentAt.ToString("HH:mm");
                }
            }
        }


        [JsonIgnore]
        public string StatusIcon
        {
            get
            {
                if (IsFailed)
                    return "❌"; // Unicode error symbol

                if (IsReadByReceiver)
                    return "✓✓"; // Unicode double check mark for read

                if (IsDelivered)
                    return "✓"; // Unicode check mark for delivered

                if (IsSent)
                    return "✓"; // Unicode check mark for sent

                return "⏱"; // Unicode watch symbol for sending
            }
        }

    }

    public class SendMessageDto
    {
        public Guid ChatId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}