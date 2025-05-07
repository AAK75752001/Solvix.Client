using Solvix.Client.Core.Models;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.Core.Helpers
{
    public static class MessageStatusHelper
    {
        public static void UpdateMessageStatus(MessageModel message, int newStatus, ILogger logger = null)
        {
            if (message == null) return;

            // Only update status if the new status is higher or if it's a failure
            if (newStatus == Constants.MessageStatus.Failed || newStatus > message.Status)
            {
                var oldStatus = message.Status;
                message.Status = newStatus;
                logger?.LogDebug("Updated message {MessageId} status from {OldStatus} to {NewStatus}",
                    message.Id, StatusToString(oldStatus), StatusToString(newStatus));

                // Update related properties based on status
                if (newStatus == Constants.MessageStatus.Read)
                {
                    message.IsRead = true;
                    if (!message.ReadAt.HasValue) message.ReadAt = DateTime.UtcNow;
                }
            }
            else
            {
                logger?.LogDebug("Ignored status update for message {MessageId} from {CurrentStatus} to {NewStatus} (no upgrade)",
                    message.Id, StatusToString(message.Status), StatusToString(newStatus));
            }
        }

        public static string StatusToString(int status)
        {
            return status switch
            {
                Constants.MessageStatus.Sending => "Sending",
                Constants.MessageStatus.Sent => "Sent",
                Constants.MessageStatus.Delivered => "Delivered",
                Constants.MessageStatus.Read => "Read",
                Constants.MessageStatus.Failed => "Failed",
                _ => $"Unknown({status})"
            };
        }

        public static string GetStatusIcon(int status, bool useEmoji = false)
        {
            if (useEmoji)
            {
                return status switch
                {
                    Constants.MessageStatus.Failed => "❌",
                    Constants.MessageStatus.Sending => "⏱️",
                    Constants.MessageStatus.Sent => "✓",
                    Constants.MessageStatus.Delivered => "✓",
                    Constants.MessageStatus.Read => "✓✓",
                    _ => "⏱️"
                };
            }
            else
            {
                return status switch
                {
                    Constants.MessageStatus.Failed => "error_outline",
                    Constants.MessageStatus.Sending => "schedule",
                    Constants.MessageStatus.Sent => "check",
                    Constants.MessageStatus.Delivered => "check",
                    Constants.MessageStatus.Read => "done_all",
                    _ => "schedule"
                };
            }
        }

        public static double GetStatusIconOpacity(int status)
        {
            return status switch
            {
                Constants.MessageStatus.Read => 1.0,
                Constants.MessageStatus.Delivered => 0.7,
                Constants.MessageStatus.Sent => 0.7,
                Constants.MessageStatus.Failed => 1.0,
                Constants.MessageStatus.Sending => 0.5,
                _ => 0.5
            };
        }

        public static Color GetStatusIconColor(int status)
        {
            Color defaultColor = Colors.Gray;
            Color readColor = Colors.DodgerBlue;
            Color errorColor = Colors.Red;

            // Try to use resource colors if available
            if (Application.Current?.Resources != null)
            {
                if (status == Constants.MessageStatus.Read &&
                    Application.Current.Resources.TryGetValue("PrimaryColor", out var primary) &&
                    primary is Color primaryColor)
                {
                    readColor = primaryColor;
                }

                if (status == Constants.MessageStatus.Failed &&
                    Application.Current.Resources.TryGetValue("ErrorColor", out var error) &&
                    error is Color errorColorRes)
                {
                    errorColor = errorColorRes;
                }

                if (Application.Current.Resources.TryGetValue("TertiaryTextColor", out var tertiary) &&
                    tertiary is Color tertiaryColor)
                {
                    defaultColor = tertiaryColor;
                }
            }

            return status switch
            {
                Constants.MessageStatus.Read => readColor,
                Constants.MessageStatus.Failed => errorColor,
                _ => defaultColor
            };
        }
    }
}