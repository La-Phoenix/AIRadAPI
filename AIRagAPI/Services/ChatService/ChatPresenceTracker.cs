using System.Collections.Concurrent;

namespace AIRagAPI.Services.ChatService;

public class ChatPresenceTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Guid>> _connectionChats = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, int>> _chatUserConnectionCounts = new();

    public bool AddConnectionToChat(string connectionId, Guid chatId, Guid userId, out bool becameOnline)
    {
        becameOnline = false;

        var joinedChats = _connectionChats.GetOrAdd(connectionId, _ => new ConcurrentDictionary<Guid, Guid>());
        if (!joinedChats.TryAdd(chatId, userId))
            return false;

        var userCounts = _chatUserConnectionCounts.GetOrAdd(chatId, _ => new ConcurrentDictionary<Guid, int>());
        var count = userCounts.AddOrUpdate(userId, 1, (_, existing) => existing + 1);
        becameOnline = count == 1;

        return true;
    }

    public bool RemoveConnectionFromChat(string connectionId, Guid chatId, out Guid userId, out bool becameOffline)
    {
        userId = Guid.Empty;
        becameOffline = false;

        if (!_connectionChats.TryGetValue(connectionId, out var joinedChats))
            return false;

        if (!joinedChats.TryRemove(chatId, out userId))
            return false;

        becameOffline = DecrementUserCount(chatId, userId);

        if (joinedChats.IsEmpty)
            _connectionChats.TryRemove(connectionId, out _);

        return true;
    }

    public List<(Guid chatId, Guid userId, bool becameOffline)> RemoveConnection(string connectionId)
    {
        if (!_connectionChats.TryRemove(connectionId, out var joinedChats))
            return [];

        var changes = new List<(Guid chatId, Guid userId, bool becameOffline)>();

        foreach (var entry in joinedChats)
        {
            var becameOffline = DecrementUserCount(entry.Key, entry.Value);
            changes.Add((entry.Key, entry.Value, becameOffline));
        }

        return changes;
    }

    public bool IsUserOnline(Guid chatId, Guid userId)
    {
        if (!_chatUserConnectionCounts.TryGetValue(chatId, out var userCounts))
            return false;

        return userCounts.TryGetValue(userId, out var count) && count > 0;
    }

    private bool DecrementUserCount(Guid chatId, Guid userId)
    {
        if (!_chatUserConnectionCounts.TryGetValue(chatId, out var userCounts))
            return false;

        while (true)
        {
            if (!userCounts.TryGetValue(userId, out var current))
                return false;

            if (current <= 1)
            {
                if (!userCounts.TryRemove(userId, out _))
                    continue;

                if (userCounts.IsEmpty)
                    _chatUserConnectionCounts.TryRemove(chatId, out _);

                return true;
            }

            if (userCounts.TryUpdate(userId, current - 1, current))
                return false;
        }
    }
}