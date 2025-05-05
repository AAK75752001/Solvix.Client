using Solvix.Client.Core.Models;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.Core.Helpers
{
    public static class MessageStatusHelper
    {
        public static void UpdateMessageStatus(MessageModel message, int newStatus, ILogger logger = null)
        {
            if (message == null) return;

            if (newStatus == Constants.MessageStatus.Failed || newStatus > message.Status)
            {
                var oldStatus = message.Status;
                message.Status = newStatus;
                logger?.LogDebug("Updated message {MessageId} status from {OldStatus} to {NewStatus}",
                    message.Id, StatusToString(oldStatus), StatusToString(newStatus));
            }
            else
            {
                logger?.LogDebug("Ignored status update for message {MessageId} from {CurrentStatus} to {NewStatus} (no downgrade)",
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
                    Constants.MessageStatus.Delivered => "✓✓", 
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
                    Constants.MessageStatus.Sent => "done",
                    Constants.MessageStatus.Delivered => "done_all",
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
            object resourceColor = Colors.Transparent;
            bool found = false;

            if (status == Constants.MessageStatus.Read)
            {
                found = Application.Current?.Resources.TryGetValue("PrimaryColor", out resourceColor) ?? false;
            }
            else if (status == Constants.MessageStatus.Failed)
            {
                found = Application.Current?.Resources.TryGetValue("ErrorColor", out resourceColor) ?? false;
            }
            else
            {
                found = Application.Current?.Resources.TryGetValue("TertiaryTextColor", out resourceColor) ?? false;
            }

            if (found && resourceColor is Color color)
            {
                return color;
            }

            return status switch
            {
                Constants.MessageStatus.Read => Colors.DodgerBlue,
                Constants.MessageStatus.Failed => Colors.Red,
                _ => Colors.Gray
            };
        }
    }
}