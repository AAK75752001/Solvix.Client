using Solvix.Client.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solvix.Client.Core.Services
{
    public class MessageCache
    {
        private static readonly Dictionary<Guid, List<MessageModel>> _cachedMessages = new();
        private static readonly Dictionary<Guid, DateTime> _lastCacheUpdate = new();
        private const int CACHE_EXPIRY_MINUTES = 30; // Increased from 5 to 30 minutes

        public static void CacheMessages(Guid chatId, List<MessageModel> messages)
        {
            // Make a deep copy of messages to avoid reference issues
            var messagesCopy = messages.Select(m => new MessageModel
            {
                Id = m.Id,
                Content = m.Content,
                SentAt = m.SentAt,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                ChatId = m.ChatId,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                IsEdited = m.IsEdited,
                EditedAt = m.EditedAt,
                Status = m.Status,
                SentAtFormatted = m.SentAtFormatted,
                IsOwnMessage = m.IsOwnMessage
            }).ToList();

            _cachedMessages[chatId] = messagesCopy;
            _lastCacheUpdate[chatId] = DateTime.UtcNow;
        }

        public static List<MessageModel> GetCachedMessages(Guid chatId)
        {
            if (_cachedMessages.TryGetValue(chatId, out var messages) &&
                _lastCacheUpdate.TryGetValue(chatId, out var lastUpdate))
            {
                // Check if cache is still valid
                if (DateTime.UtcNow.Subtract(lastUpdate).TotalMinutes < CACHE_EXPIRY_MINUTES)
                {
                    return messages;
                }
            }
            return null;
        }

        public static void InvalidateCache(Guid chatId)
        {
            if (_cachedMessages.ContainsKey(chatId))
            {
                _cachedMessages.Remove(chatId);
                _lastCacheUpdate.Remove(chatId);
            }
        }

        public static void InvalidateAllCaches()
        {
            _cachedMessages.Clear();
            _lastCacheUpdate.Clear();
        }
    }
}
