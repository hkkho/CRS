using ReactorSim.BrowserHost;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed class SerializedCommandQueueTests
{
    [Fact]
    public async Task CommandsCompleteInSubmissionOrderWithoutOverlapping()
    {
        var queue = new SerializedCommandQueue();
        int state = 0;
        int active = 0;
        var requests = Enumerable.Range(0, 50).Select(index => queue.Enqueue(() =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            Assert.Equal(index, state);
            state++;
            Interlocked.Decrement(ref active);
            return index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        })).ToArray();
        var results = await Task.WhenAll(requests);
        Assert.Equal(50, state);
        Assert.Equal(Enumerable.Range(0, 50).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)), results);
    }

    [Fact]
    public async Task FailedCommandDoesNotPoisonTheNextRequest()
    {
        var queue = new SerializedCommandQueue();
        var failure = queue.Enqueue(() => throw new InvalidOperationException("test failure"));
        var success = queue.Enqueue(() => "ready");
        await Assert.ThrowsAsync<InvalidOperationException>(() => failure);
        Assert.Equal("ready", await success);
    }
}
