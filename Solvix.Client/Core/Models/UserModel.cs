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

                // اولویت با نام و نام خانوادگی
                if (!string.IsNullOrWhiteSpace(FirstName) && FirstName.Length > 0)
                    initials += FirstName[0];

                if (!string.IsNullOrWhiteSpace(LastName) && LastName.Length > 0)
                    initials += LastName[0];

                // اگر نام و نام خانوادگی موجود نبود، از نام کاربری استفاده می‌کنیم
                if (string.IsNullOrWhiteSpace(initials) && !string.IsNullOrWhiteSpace(Username) && Username.Length > 0)
                {
                    // تلاش برای پیدا کردن دو حرف اول در نام کاربری (اگر شامل فاصله یا نقطه است)
                    var parts = Username.Split(new char[] { ' ', '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        // استفاده از حرف اول دو بخش اول
                        initials = parts[0].Length > 0 ? parts[0][0].ToString() : "";
                        initials += parts[1].Length > 0 ? parts[1][0].ToString() : "";
                    }
                    else
                    {
                        // فقط حرف اول نام کاربری
                        initials = Username[0].ToString();

                        // اگر نام کاربری طولانی باشد، از حرف دوم هم استفاده می‌کنیم
                        if (Username.Length > 1)
                            initials += Username[1].ToString();
                    }
                }

                // اگر هنوز حروف اختصاری ایجاد نشده، از شماره تلفن استفاده می‌کنیم
                if (string.IsNullOrWhiteSpace(initials) && !string.IsNullOrWhiteSpace(PhoneNumber) && PhoneNumber.Length > 0)
                {
                    initials = PhoneNumber[0].ToString();
                    if (PhoneNumber.Length > 1)
                        initials += PhoneNumber[1].ToString();
                }

                // اگر هیچ چیزی موجود نبود، از علامت سوال استفاده می‌کنیم
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