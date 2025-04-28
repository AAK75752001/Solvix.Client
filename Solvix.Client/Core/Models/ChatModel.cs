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
        private UserModel _cachedOtherParticipant;
        private string _cachedLastActivityStatus;
        private bool _propertiesInitialized = false;
        public List<UserModel> Participants { get; set; } = new List<UserModel>();

        [JsonIgnore]
        public ObservableCollection<MessageModel> Messages { get; set; } = new ObservableCollection<MessageModel>();

        [JsonIgnore]
        public UserModel OtherParticipant
        {
            get
            {
                if (_propertiesInitialized && _cachedOtherParticipant != null)
                    return _cachedOtherParticipant;

                if (IsGroup || Participants == null || Participants.Count == 0)
                    return null;

                try
                {
                    var currentUserIdTask = SecureStorage.GetAsync(Constants.StorageKeys.UserId);
                    string currentUserId = null;

                    // Evitar bloquear el hilo UI
                    if (currentUserIdTask.IsCompleted)
                        currentUserId = currentUserIdTask.Result;
                    else
                    {
                        // Si la tarea no está completa, esperar pero con timeout
                        var timeoutTask = Task.Delay(500); // 500ms timeout
                        if (Task.WhenAny(currentUserIdTask, timeoutTask).Result == currentUserIdTask)
                            currentUserId = currentUserIdTask.Result;
                    }

                    // Si no podemos obtener el ID del usuario, devolver el primer participante
                    if (string.IsNullOrEmpty(currentUserId) || !long.TryParse(currentUserId, out var userId))
                        _cachedOtherParticipant = Participants.FirstOrDefault();
                    else
                        _cachedOtherParticipant = Participants.FirstOrDefault(p => p.Id != userId);

                    // Importante: No modificar el estado aquí, solo cachearlo
                    _propertiesInitialized = true;
                    return _cachedOtherParticipant;
                }
                catch
                {
                    // En caso de error, devolver el primer participante
                    return Participants.FirstOrDefault();
                }
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
                try
                {
                    var other = OtherParticipant;

                    if (other != null)
                    {
                        if (other.IsOnline)
                            return "Online";
                        else if (other.LastActive.HasValue)
                            return $"Last seen {other.LastActiveText}";
                    }

                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public void InitializeComputedProperties()
        {
            _propertiesInitialized = false;
            // این باعث می‌شود خصوصیت‌های محاسباتی یک‌بار محاسبه شوند و کش شوند
            var dummy1 = OtherParticipant;
            var dummy2 = LastActivityStatus;
        }


        public void InternalInitializeComputedProperties()
        {
            try
            {
                // محاسبه مقادیر پیش‌محاسبه شده
                var otherParticipant = this.OtherParticipant;
                var displayTitle = this.DisplayTitle;
                var lastActivityStatus = this.LastActivityStatus;
                var lastMessageTimeFormatted = this.LastMessageTimeFormatted;
            }
            catch (Exception ex)
            {
                // فقط لاگ خطا بدون پرتاب استثنا
                System.Diagnostics.Debug.WriteLine($"Error initializing computed properties: {ex.Message}");
            }
        }

        // کامپوننت جدید برای بهبود UX و نمایش انیمیشن
        //public static class UXUtilities
        //{
        //    // انیمیشن کمرنگ شدن و از بین رفتن
        //    public static async Task FadeOutAsync(this VisualElement element, uint duration = 250, Easing easing = null)
        //    {
        //        await element.FadeTo(0, duration, easing ?? Easing.CubicIn);
        //        element.IsVisible = false;
        //    }

        //    // انیمیشن پررنگ شدن و ظاهر شدن
        //    public static async Task FadeInAsync(this VisualElement element, uint duration = 250, Easing easing = null)
        //    {
        //        element.Opacity = 0;
        //        element.IsVisible = true;
        //        await element.FadeTo(1, duration, easing ?? Easing.CubicOut);
        //    }
        //}

    }
}
