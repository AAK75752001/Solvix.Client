using Solvix.Client.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solvix.Client.Core.Services
{
    public static class MessageCache
    {
        private static readonly Dictionary<Guid, List<MessageModel>> _cachedMessages = new();
        private static readonly Dictionary<Guid, DateTime> _lastCacheUpdate = new();
        private static readonly Dictionary<Guid, HashSet<string>> _messageSignatures = new();
        private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const int CACHE_EXPIRY_MINUTES = 30;

        public static async Task CacheMessagesAsync(Guid chatId, List<MessageModel> messages)
        {
            if (messages == null || !messages.Any())
                return;

            await _cacheLock.WaitAsync();

            try
            {
                // Create a deep copy of messages to avoid reference issues
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

                // Store message signatures for deduplication
                var signatures = new HashSet<string>();
                foreach (var message in messagesCopy)
                {
                    signatures.Add(message.Signature);
                }
                _messageSignatures[chatId] = signatures;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // For backward compatibility
        public static void CacheMessages(Guid chatId, List<MessageModel> messages)
        {
            CacheMessagesAsync(chatId, messages).ConfigureAwait(false);
        }

        public static async Task<List<MessageModel>> GetCachedMessagesAsync(Guid chatId)
        {
            await _cacheLock.WaitAsync();

            try
            {
                if (_cachedMessages.TryGetValue(chatId, out var messages) &&
                    _lastCacheUpdate.TryGetValue(chatId, out var lastUpdate))
                {
                    // Check if cache is still valid
                    if (DateTime.UtcNow.Subtract(lastUpdate).TotalMinutes < CACHE_EXPIRY_MINUTES)
                    {
                        return messages.ToList(); // Return a copy to avoid potential issues
                    }
                }
                return null;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // For backward compatibility
        public static List<MessageModel> GetCachedMessages(Guid chatId)
        {
            var task = GetCachedMessagesAsync(chatId);
            task.Wait();
            return task.Result;
        }

        public static async Task InvalidateCacheAsync(Guid chatId)
        {
            await _cacheLock.WaitAsync();

            try
            {
                _cachedMessages.Remove(chatId);
                _lastCacheUpdate.Remove(chatId);
                _messageSignatures.Remove(chatId);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // For backward compatibility
        public static void InvalidateCache(Guid chatId)
        {
            InvalidateCacheAsync(chatId).ConfigureAwait(false);
        }

        public static async Task InvalidateAllCachesAsync()
        {
            await _cacheLock.WaitAsync();

            try
            {
                _cachedMessages.Clear();
                _lastCacheUpdate.Clear();
                _messageSignatures.Clear();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // For backward compatibility
        public static void InvalidateAllCaches()
        {
            InvalidateAllCachesAsync().ConfigureAwait(false);
        }

        // Check if a message with the same signature exists in the cache
        public static async Task<bool> ContainsDuplicateAsync(Guid chatId, MessageModel message)
        {
            if (message == null)
                return false;

            await _cacheLock.WaitAsync();

            try
            {
                // Check if we have signatures for this chat
                if (_messageSignatures.TryGetValue(chatId, out var signatures))
                {
                    return signatures.Contains(message.Signature);
                }

                return false;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // Manually add a message signature to prevent duplicates
        public static async Task AddMessageSignatureAsync(Guid chatId, MessageModel message)
        {
            if (message == null)
                return;

            await _cacheLock.WaitAsync();

            try
            {
                if (!_messageSignatures.TryGetValue(chatId, out var signatures))
                {
                    signatures = new HashSet<string>();
                    _messageSignatures[chatId] = signatures;
                }

                signatures.Add(message.Signature);
            }
            finally
            {
                _cacheLock.Release();
            }
        }
    }
}