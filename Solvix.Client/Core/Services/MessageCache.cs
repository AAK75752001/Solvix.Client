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
        private const int CACHE_EXPIRY_MINUTES = 5;

        public static void CacheMessages(Guid chatId, List<MessageModel> messages)
        {
            _cachedMessages[chatId] = messages;
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
    }
}
