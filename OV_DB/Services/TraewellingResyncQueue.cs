using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace OV_DB.Services
{
    /// <summary>
    /// The users waiting for a full-history resync. One at a time per user: the walk takes
    /// minutes, and a second one started alongside it would re-read the same pages and race
    /// the first on the inbox's unique index.
    /// </summary>
    public interface ITraewellingResyncQueue
    {
        /// <summary>False when this user already has a resync queued or running.</summary>
        bool TryEnqueue(int userId);

        bool IsRunning(int userId);

        IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken);

        void MarkFinished(int userId);
    }

    public class TraewellingResyncQueue : ITraewellingResyncQueue
    {
        private readonly Channel<int> _queue = Channel.CreateUnbounded<int>();
        private readonly ConcurrentDictionary<int, byte> _pending = new();

        public bool TryEnqueue(int userId)
        {
            if (!_pending.TryAdd(userId, 0))
                return false;

            if (_queue.Writer.TryWrite(userId))
                return true;

            _pending.TryRemove(userId, out _);
            return false;
        }

        public bool IsRunning(int userId) => _pending.ContainsKey(userId);

        public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken) =>
            _queue.Reader.ReadAllAsync(cancellationToken);

        public void MarkFinished(int userId) => _pending.TryRemove(userId, out _);
    }
}
