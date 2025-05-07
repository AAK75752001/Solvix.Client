namespace Solvix.Client.Core
{
    public static class Constants
    {
        // Set a proper localhost URL that matches your server
        //public static string BaseApiUrl = "https://localhost:7144";
        public static string BaseApiUrl = "https://solvixserver-production.up.railway.app";
        public static string ApiUrl = $"{BaseApiUrl}/api/";
        public static string SignalRUrl = $"{BaseApiUrl}/chathub";

        // API Endpoints
        public static class Endpoints
        {
            // Auth Endpoints
            public static string CheckPhone = "auth/check-phone"; // + /{phoneNumber}
            public static string Login = "auth/login";
            public static string Register = "auth/register";
            public static string CurrentUser = "auth/current-user";
            public static string RefreshToken = "auth/refresh-token";

            // Chat Endpoints
            public static string GetChats = "chat";
            public static string GetChat = "chat";  // + /{chatId}
            public static string StartChat = "chat/start";
            public static string GetMessages = "chat";  // + /{chatId}/messages
            public static string SendMessage = "chat/send-message";
            public static string MarkRead = "chat";  // + /{chatId}/mark-read

            // User Endpoints
            public static string SearchUsers = "user/search";
            public static string GetUser = "user";  // + /{userId}
            public static string GetOnlineUsers = "user/online";
        }

        // Secure Storage Keys
        public static class StorageKeys
        {
            public static string AuthToken = "auth_token";
            public static string UserId = "user_id";
            public static string Username = "username";
            public static string PhoneNumber = "phone_number";
            public static string Theme = "app_theme";
            public static string Notifications = "notifications_enabled";
            public static string TwoFactorAuth = "two_factor_auth";
        }

        // Message Status
        public static class MessageStatus
        {
            public const int Unknown = -1;
            public const int Sending = 0;    // در حال ارسال - نمایش آیکون ساعت
            public const int Sent = 1;       // ارسال شده به سرور - نمایش یک تیک
            public const int Delivered = 2;  // دریافت شده توسط گیرنده - نمایش یک تیک
            public const int Read = 3;       // خوانده شده توسط گیرنده - نمایش دو تیک
            public const int Failed = 4;     // خطا در ارسال - نمایش آیکون خطا
        }

        // Validation Regex
        public static class Validation
        {
            public const string PhoneRegex = @"^09\d{9}$";
            public const int PasswordMinLength = 8;
        }

        // Themes
        public static class Themes
        {
            public const string Light = "Light";
            public const string Dark = "Dark";
            public const string System = "System";
        }

        // Animation Durations
        public static class AnimationDurations
        {
            public const uint Short = 150;
            public const uint Medium = 250;
            public const uint Long = 350;
        }
    }
}