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
            set => _status = value;
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

                return false; // مقدار پیش‌فرض اگر تنظیم نشده باشد
            }
            set => _isOwnMessage = value;
        }

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
        public string Signature => $"{SenderId}:{Content.GetHashCode()}:{SentAt.Ticks}";

        // بازنویسی Equals و GetHashCode برای مقایسه بهتر
        public override bool Equals(object obj)
        {
            if (obj is MessageModel other)
            {
                // پیام‌ها را برابر در نظر بگیرید اگر همان شناسه را داشته باشند (اگر شناسه > 0)
                // یا اگر امضای یکسانی برای پیام‌های موقت داشته باشند
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
            // از شناسه برای پیام‌های دائمی، از امضا برای موقت‌ها
            if (Id > 0)
                return Id.GetHashCode();

            return Signature.GetHashCode();
        }
    }
}