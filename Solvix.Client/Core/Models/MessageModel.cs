using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Solvix.Client.Core.Models
{
    public class MessageModel : INotifyPropertyChanged
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
        private int _status = Constants.MessageStatus.Unknown;
        private string _sentAtFormatted = string.Empty;
        private string _correlationId = string.Empty;

        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                    // If Id is set (from server), status shouldn't be Sending unless explicitly set
                    if (_status == Constants.MessageStatus.Sending && _id > 0)
                    {
                        Status = Constants.MessageStatus.Sent;
                    }
                    else if (_status == Constants.MessageStatus.Unknown && _id > 0)
                    {
                        Status = Constants.MessageStatus.Sent; // Default to Sent if loaded with ID
                    }
                }
            }
        }

        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime SentAt
        {
            get => _sentAt;
            set
            {
                if (_sentAt != value)
                {
                    _sentAt = value;
                    OnPropertyChanged();
                    _sentAtFormatted = string.Empty; // Force reformat on change
                    OnPropertyChanged(nameof(SentAtFormatted));
                    OnPropertyChanged(nameof(LocalSentAt));
                }
            }
        }

        public long SenderId
        {
            get => _senderId;
            set
            {
                if (_senderId != value)
                {
                    _senderId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SenderName
        {
            get => _senderName;
            set
            {
                if (_senderName != value)
                {
                    _senderName = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public Guid ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead != value)
                {
                    _isRead = value;
                    OnPropertyChanged();
                    // Automatically update status if set to read
                    if (_isRead && Status < Constants.MessageStatus.Read)
                    {
                        Status = Constants.MessageStatus.Read;
                    }
                }
            }
        }

        public DateTime? ReadAt
        {
            get => _readAt;
            set
            {
                if (_readAt != value)
                {
                    _readAt = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEdited
        {
            get => _isEdited;
            set
            {
                if (_isEdited != value)
                {
                    _isEdited = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime? EditedAt
        {
            get => _editedAt;
            set
            {
                if (_editedAt != value)
                {
                    _editedAt = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string CorrelationId
        {
            get => _correlationId;
            set
            {
                if (_correlationId != value)
                {
                    _correlationId = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public DateTime LocalSentAt => SentAt.Kind == DateTimeKind.Utc ? SentAt.ToLocalTime() : SentAt;

        [JsonIgnore]
        public int Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(IsSending));
                    OnPropertyChanged(nameof(IsSent));
                    OnPropertyChanged(nameof(IsDelivered));
                    OnPropertyChanged(nameof(IsReadByReceiver));
                    OnPropertyChanged(nameof(IsFailed));
                }
            }
        }

        [JsonIgnore]
        public string SentAtFormatted
        {
            get
            {
                if (string.IsNullOrEmpty(_sentAtFormatted))
                {
                    _sentAtFormatted = FormatMessageTime();
                }
                return _sentAtFormatted;
            }
        }

        [JsonIgnore]
        public bool IsOwnMessage
        {
            get => _isOwnMessage.GetValueOrDefault();
            set
            {
                if (_isOwnMessage != value)
                {
                    _isOwnMessage = value;
                    OnPropertyChanged();
                }
            }
        }


        [JsonIgnore]
        public bool IsSending => Status == Constants.MessageStatus.Sending;

        [JsonIgnore]
        public bool IsSent => Status >= Constants.MessageStatus.Sent && Status != Constants.MessageStatus.Failed;

        [JsonIgnore]
        public bool IsDelivered => Status >= Constants.MessageStatus.Delivered && Status != Constants.MessageStatus.Failed;

        [JsonIgnore]
        public bool IsReadByReceiver => Status >= Constants.MessageStatus.Read && Status != Constants.MessageStatus.Failed;

        [JsonIgnore]
        public bool IsFailed => Status == Constants.MessageStatus.Failed;

        [JsonIgnore]
        public string StatusIcon
        {
            get
            {
                if (!IsOwnMessage) return string.Empty;

                return Status switch
                {
                    Constants.MessageStatus.Failed => "❌",
                    Constants.MessageStatus.Sending => "⏱️",
                    Constants.MessageStatus.Sent => "✓🖥",
                    Constants.MessageStatus.Delivered => "✓",
                    Constants.MessageStatus.Read => "✓✓",
                    _ => "⏱️"
                };
            }
        }

        private string FormatMessageTime()
        {
            try
            {
                var localTime = LocalSentAt;
                return localTime.ToString("HH:mm");
            }
            catch
            {
                return SentAt.ToString("HH:mm");
            }
        }

        [JsonIgnore]
        public string Signature => $"{SenderId}:{Content?.GetHashCode() ?? 0}:{SentAt.Ticks}:{CorrelationId}";

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public override bool Equals(object obj)
        {
            if (obj is MessageModel other)
            {
                if (!string.IsNullOrEmpty(CorrelationId) && !string.IsNullOrEmpty(other.CorrelationId))
                {
                    return CorrelationId == other.CorrelationId;
                }
                if (Id > 0 && other.Id > 0)
                {
                    return Id == other.Id;
                }
                if (Id <= 0 && other.Id <= 0 && !string.IsNullOrEmpty(CorrelationId) && CorrelationId == other.CorrelationId)
                {
                    return true;
                }
                if (Id <= 0 && other.Id <= 0)
                {
                    return SenderId == other.SenderId &&
                     Content == other.Content &&
                     Math.Abs((SentAt - other.SentAt).TotalSeconds) < 5;
                }

            }
            return false;
        }

        public override int GetHashCode()
        {
            if (!string.IsNullOrEmpty(CorrelationId))
                return HashCode.Combine(CorrelationId);
            if (Id > 0)
                return Id.GetHashCode();

            return HashCode.Combine(SenderId, Content, SentAt.Ticks);
        }
    }
}