using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OV_DB.Services;

namespace OV_DB.Tests;

/// <summary>
/// A full-history walk takes minutes, so the button stays pressable long before the previous
/// walk is done. Keeping one per user is what stops a second walk re-reading the same pages
/// and racing the first on the inbox's unique index.
/// </summary>
public class TraewellingResyncQueueTests
{
    [Fact]
    public void RefusesASecondResyncWhileOneIsPending()
    {
        var queue = new TraewellingResyncQueue();

        Assert.True(queue.TryEnqueue(1));
        Assert.False(queue.TryEnqueue(1));
        Assert.True(queue.IsRunning(1));
    }

    [Fact]
    public void LetsAnotherUserThrough()
    {
        var queue = new TraewellingResyncQueue();
        queue.TryEnqueue(1);

        Assert.True(queue.TryEnqueue(2));
        Assert.False(queue.IsRunning(3));
    }

    [Fact]
    public async Task AcceptsTheUserAgainOnceTheirWalkFinished()
    {
        var queue = new TraewellingResyncQueue();
        queue.TryEnqueue(1);

        var dequeued = await FirstAsync(queue);
        queue.MarkFinished(dequeued);

        Assert.False(queue.IsRunning(1));
        Assert.True(queue.TryEnqueue(1));
    }

    private static async Task<int> FirstAsync(TraewellingResyncQueue queue)
    {
        await foreach (var userId in queue.DequeueAllAsync(CancellationToken.None))
            return userId;
        return 0;
    }
}
