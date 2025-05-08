using System.Text.Json.Serialization;

namespace Solvix.Client.Core.Models
{
    public class UserModel
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName))
                    return $"{FirstName} {LastName}".Trim();

                return !string.IsNullOrWhiteSpace(Username) ? Username : PhoneNumber ?? "Unknown User";
            }
        }



        [JsonIgnore]
        public string Initials
        {
            get
            {
                string initials = string.Empty;

                if (!string.IsNullOrWhiteSpace(FirstName) && FirstName.Length > 0)
                    initials += FirstName[0];

                if (!string.IsNullOrWhiteSpace(LastName) && LastName.Length > 0)
                    initials += LastName[0];

                if (string.IsNullOrWhiteSpace(initials) && !string.IsNullOrWhiteSpace(Username) && Username.Length > 0)
                    initials += Username[0];

                if (string.IsNullOrWhiteSpace(initials) && !string.IsNullOrWhiteSpace(PhoneNumber) && PhoneNumber.Length > 0)
                    initials += PhoneNumber[0];

                return !string.IsNullOrWhiteSpace(initials) ? initials.ToUpper() : "?";
            }
        }

        public string? PhoneNumber { get; set; }
        public string? Token { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActive { get; set; }

        [JsonIgnore]
        public string LastActiveText
        {
            get
            {
                if (IsOnline)
                    return "آنلاین";

                if (!LastActive.HasValue)
                    return string.Empty;

                var localDateTime = LastActive.Value.Kind == DateTimeKind.Utc
                    ? LastActive.Value.ToLocalTime()
                    : LastActive.Value;

                var timeSpan = DateTime.Now - localDateTime;

                if (timeSpan.TotalMinutes < 1)
                    return "لحظاتی پیش";

                if (timeSpan.TotalHours < 1)
                    return $"{(int)timeSpan.TotalMinutes} دقیقه پیش";

                if (timeSpan.TotalDays < 1)
                    return $"{(int)timeSpan.TotalHours} ساعت پیش";

                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays} روز پیش";

                return $"آخرین بازدید {localDateTime.ToString("yyyy/MM/dd")}";
            }
        }
    }
}

