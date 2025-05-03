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
        private int _status = Constants.MessageStatus.Sending;
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

        // خصوصیت‌های محلی برای رابط کاربری
        [JsonIgnore]
        public DateTime LocalSentAt
        {
            get
            {
                // تبدیل زمان سرور به زمان محلی برای نمایش دقیق
                return SentAt.Kind == DateTimeKind.Utc ? SentAt.ToLocalTime() : SentAt;
            }
        }

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
                if (!string.IsNullOrEmpty(_sentAtFormatted))
                    return _sentAtFormatted;

                return FormatMessageTime();
            }
            set
            {
                if (_sentAtFormatted != value)
                {
                    _sentAtFormatted = value;
                    OnPropertyChanged();
                }
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

        // سایر خصوصیت‌های محاسباتی بدون تغییر
        [JsonIgnore]
        public bool IsSent => Status >= Constants.MessageStatus.Sent;

        [JsonIgnore]
        public bool IsDelivered => Status >= Constants.MessageStatus.Delivered;

        [JsonIgnore]
        public bool IsReadByReceiver => Status >= Constants.MessageStatus.Read;

        [JsonIgnore]
        public bool IsFailed => Status == Constants.MessageStatus.Failed;

        [JsonIgnore]
        public string StatusIcon
        {
            get
            {
                if (IsFailed)
                    return "❌"; // نماد خطا (پیام ارسال نشده)

                if (Status == Constants.MessageStatus.Sending)
                    return "⏱"; // ساعت (در حال ارسال)

                if (IsReadByReceiver)
                    return "✓✓"; // دو تیک (خوانده شده)

                if (IsDelivered)
                    return "✓"; // یک تیک (تحویل داده شده)

                if (IsSent)
                    return "✓"; // یک تیک (ارسال شده)

                // حالت پیش‌فرض
                return "⏱"; // ساعت (در حال ارسال)
            }
        }

        private string FormatMessageTime()
        {
            try
            {
                // تبدیل زمان سرور به زمان محلی
                var localTime = LocalSentAt;

                // فقط نمایش ساعت و دقیقه
                return localTime.ToString("HH:mm");
            }
            catch
            {
                // در صورت بروز خطا، از قالب ساده استفاده کنید
                return SentAt.ToString("HH:mm");
            }
        }

        // ایجاد امضای منحصر به فرد برای جلوگیری از تکرار پیام
        [JsonIgnore]
        public string Signature => $"{SenderId}:{Content.GetHashCode()}:{SentAt.Ticks}:{CorrelationId}";

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        // بازنویسی Equals و GetHashCode برای مقایسه بهتر
        public override bool Equals(object obj)
        {
            if (obj is MessageModel other)
            {
                // اگر هر دو پیام دارای CorrelationId باشند و یکسان باشند
                if (!string.IsNullOrEmpty(CorrelationId) && !string.IsNullOrEmpty(other.CorrelationId))
                {
                    return CorrelationId == other.CorrelationId;
                }

                // پیام‌ها را برابر در نظر بگیرید اگر همان شناسه را داشته باشند (اگر شناسه > 0)
                if (Id > 0 && other.Id > 0)
                    return Id == other.Id;

                // مقایسه بر اساس محتوا و زمان ارسال
                return SenderId == other.SenderId &&
                       Content == other.Content &&
                       Math.Abs((SentAt - other.SentAt).TotalSeconds) < 60;
            }
            return false;
        }

        public override int GetHashCode()
        {
            // اگر CorrelationId وجود دارد، از آن استفاده کنیم
            if (!string.IsNullOrEmpty(CorrelationId))
                return CorrelationId.GetHashCode();

            // از شناسه برای پیام‌های دائمی، از امضا برای موقت‌ها
            if (Id > 0)
                return Id.GetHashCode();

            return Signature.GetHashCode();
        }
    }
}