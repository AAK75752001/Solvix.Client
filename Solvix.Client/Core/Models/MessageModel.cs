using System.Text.Json.Serialization;

namespace Solvix.Client.Core.Models
{
    public class MessageModel
    {
        private int _id;
        private string _content = string.Empty;
        private DateTime _sentAt;
        private long _senderId;
        private string _senderName = string.Empty;
        private Guid _chatId;
        private bool _isRead;
        private DateTime? _readAt;
        private bool _isEdited;
        private DateTime? _editedAt;
        private bool? _isOwnMessage;
        private int _status = Constants.MessageStatus.Sending;
        private string _sentAtFormatted = string.Empty;

        public int Id
        {
            get => _id;
            set => _id = value;
        }

        public string Content
        {
            get => _content;
            set => _content = value ?? string.Empty;
        }

        public DateTime SentAt
        {
            get => _sentAt;
            set => _sentAt = value;
        }

        public long SenderId
        {
            get => _senderId;
            set => _senderId = value;
        }

        public string SenderName
        {
            get => _senderName;
            set => _senderName = value ?? string.Empty;
        }

        public Guid ChatId
        {
            get => _chatId;
            set => _chatId = value;
        }

        public bool IsRead
        {
            get => _isRead;
            set => _isRead = value;
        }

        public DateTime? ReadAt
        {
            get => _readAt;
            set => _readAt = value;
        }

        public bool IsEdited
        {
            get => _isEdited;
            set => _isEdited = value;
        }

        public DateTime? EditedAt
        {
            get => _editedAt;
            set => _editedAt = value;
        }

        // Local properties for UI
        [JsonIgnore]
        public DateTime LocalSentAt => SentAt.ToLocalTime();

        [JsonIgnore]
        public int Status
        {
            get => _status;
            set => _status = value;
        }

        [JsonIgnore]
        public string SentAtFormatted
        {
            get => string.IsNullOrEmpty(_sentAtFormatted) ? FormatTimeText() : _sentAtFormatted;
            set => _sentAtFormatted = value;
        }

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

                return FormatTimeText();
            }
        }

        private string FormatTimeText()
        {
            try
            {
                // Convert server time to local time
                var localTime = LocalSentAt;
                return localTime.ToString("HH:mm");
            }
            catch
            {
                // In case of error, use simple format
                return SentAt.ToString("HH:mm");
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
                    return "✓✓"; // Unicode double check mark for delivered

                if (IsSent)
                    return "✓"; // Unicode check mark for sent

                return "⏱"; // Unicode watch symbol for sending
            }
        }

        // Generate a unique signature for message deduplication
        [JsonIgnore]
        public string Signature => $"{SenderId}:{Content.GetHashCode()}:{SentAt.Ticks}";

        // Override Equals and GetHashCode for better comparison
        public override bool Equals(object obj)
        {
            if (obj is MessageModel other)
            {
                // Consider messages equal if they have the same ID (if ID > 0)
                // or if they have the same signature for temporary messages
                if (Id > 0 && other.Id > 0)
                    return Id == other.Id;

                return SenderId == other.SenderId &&
                       Content == other.Content &&
                       Math.Abs((SentAt - other.SentAt).TotalSeconds) < 60;
            }
            return false;
        }

        public override int GetHashCode()
        {
            // Use ID for permanent messages, signature for temporary ones
            if (Id > 0)
                return Id.GetHashCode();

            return Signature.GetHashCode();
        }
    }

    public class SendMessageDto
    {
        public Guid ChatId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}