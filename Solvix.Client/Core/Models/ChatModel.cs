using System.Collections.ObjectModel;
using System.Text.Json.Serialization;


namespace Solvix.Client.Core.Models
{
    public class ChatModel
    {
        public Guid Id { get; set; }
        public bool IsGroup { get; set; }
        public string? Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public List<UserModel> Participants { get; set; } = new List<UserModel>();


        [JsonIgnore]
        public ObservableCollection<MessageModel> Messages { get; set; } = new ObservableCollection<MessageModel>();

        [JsonIgnore]
        public UserModel? OtherParticipant { get; set; }


        [JsonIgnore]
        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (OtherParticipant != null)
                    return OtherParticipant.DisplayName;

                return IsGroup ? "گروه" : "چت";
            }
        }

        [JsonIgnore]
        public string LastMessageTimeFormatted
        {
            get
            {
                if (!LastMessageTime.HasValue) return string.Empty;

                var localDateTime = LastMessageTime.Value;
                var today = DateTime.Now.Date;

                if (localDateTime.Date == today) return localDateTime.ToString("HH:mm");
                if (today.Subtract(localDateTime.Date).TotalDays < 7) return localDateTime.ToString("ddd");
                return localDateTime.ToString("yyyy/MM/dd");
            }
        }
    }
}