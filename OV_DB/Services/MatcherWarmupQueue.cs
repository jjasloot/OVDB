using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace OV_DB.Services
{
    /// <summary>
    /// The users waiting for the route index to be built. One at a time per user: the build
    /// takes seconds, and a second request from the same page would only queue behind the
    /// first and report the same thing twice.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="ITraewellingResyncQueue"/> on purpose — same problem, same shape: an
    /// endpoint that can only queue, and a hosted service that does the work and reports it.
    /// </remarks>
    public interface IMatcherWarmupQueue
    {
        /// <summary>False when this user already has a warm-up queued or running.</summary>
        bool TryEnqueue(int userId);

        bool IsRunning(int userId);

        IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken);

        void MarkFinished(int userId);
    }

    public class MatcherWarmupQueue : IMatcherWarmupQueue
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
