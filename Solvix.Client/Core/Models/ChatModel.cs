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
        public UserModel? OtherParticipant
        {
            get
            {
                if (IsGroup || Participants == null || Participants.Count == 0)
                    return null;

                // The first participant that isn't the current user
                var currentUserId = SecureStorage.GetAsync(Constants.StorageKeys.UserId).Result;

                if (!long.TryParse(currentUserId, out var userId))
                    return Participants.FirstOrDefault();

                return Participants.FirstOrDefault(p => p.Id != userId);
            }
        }

        [JsonIgnore]
        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (OtherParticipant != null)
                    return OtherParticipant.DisplayName;

                return "Chat";
            }
        }

        [JsonIgnore]
        public string LastMessageTimeFormatted
        {
            get
            {
                if (!LastMessageTime.HasValue)
                    return string.Empty;

                // Today, show time
                if (LastMessageTime.Value.Date == DateTime.Today)
                    return LastMessageTime.Value.ToString("HH:mm");

                // This week, show day name
                if (DateTime.Today.Subtract(LastMessageTime.Value.Date).TotalDays < 7)
                    return LastMessageTime.Value.ToString("ddd");

                // Older, show date
                return LastMessageTime.Value.ToString("yyyy-MM-dd");
            }
        }

        [JsonIgnore]
        public string LastActivityStatus
        {
            get
            {
                if (OtherParticipant?.IsOnline == true)
                    return "Online";

                if (OtherParticipant?.LastActive.HasValue == true)
                    return $"Last seen {OtherParticipant.LastActiveText}";

                return string.Empty;
            }
        }
    }
}
