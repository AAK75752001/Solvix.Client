using Solvix.Client.Core.Models;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.Core.Helpers
{
    public static class MessageStatusHelper
    {
        /// <summary>
        /// Updates a message's status based on the message state
        /// </summary>
        public static void UpdateMessageStatus(MessageModel message, int newStatus, ILogger logger = null)
        {
            if (message == null) return;

            // Only allow status progression in one direction (except for failure state)
            // Sending -> Sent -> Delivered -> Read
            if (newStatus == Constants.MessageStatus.Failed ||
                newStatus > message.Status)
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

        /// <summary>
        /// Converts a message status code to a human-readable string
        /// </summary>
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

        /// <summary>
        /// Gets the appropriate status icon for a message based on its status
        /// </summary>
        public static string GetStatusIcon(int status, bool useEmoji = true)
        {
            if (useEmoji)
            {
                return status switch
                {
                    Constants.MessageStatus.Failed => "❌",    // Error
                    Constants.MessageStatus.Sending => "⏱",   // Clock (sending)
                    Constants.MessageStatus.Read => "✓✓",      // Double tick (read)
                    Constants.MessageStatus.Delivered => "✓", // Double tick (delivered but opacity will be different)
                    Constants.MessageStatus.Sent => "✓",       // Single tick (sent)
                    _ => "⏱"                                   // Clock (default)
                };
            }
            else
            {
                return status switch
                {
                    Constants.MessageStatus.Failed => "error",
                    Constants.MessageStatus.Sending => "watch_later",
                    Constants.MessageStatus.Read => "done_all",
                    Constants.MessageStatus.Delivered => "done_all",
                    Constants.MessageStatus.Sent => "done",
                    _ => "watch_later"
                };
            }
        }

        /// <summary>
        /// Gets the opacity value for a message status icon, useful for showing delivered vs read
        /// </summary>
        public static double GetStatusIconOpacity(int status)
        {
            return status switch
            {
                Constants.MessageStatus.Read => 1.0,        // Full opacity for read status
                Constants.MessageStatus.Delivered => 0.7,   // Slightly dimmed for delivered 
                Constants.MessageStatus.Sent => 0.7,        // Slightly dimmed for sent
                Constants.MessageStatus.Failed => 1.0,      // Full opacity for error
                _ => 0.5                                    // Dimmed for sending and others
            };
        }
    }
}