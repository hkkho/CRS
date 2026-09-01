using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class GameSessionTests
{
    [Fact]
    public void PracticeSessionAdvancesAndAppliesPlayerCommands()
    {
        GameSession session = PracticeGameSessionFactory.Create();

        Assert.Equal(0.0, session.Snapshot.SimulationTimeSeconds);
        Assert.Equal(PracticeGameSessionFactory.PlayPlaybackModeId, session.Snapshot.PlaybackModeId);

        GameSessionCommandResult queued = session.QueuePowerTarget(0.95);
        Assert.True(queued.Accepted);
        Assert.Equal(1u, queued.Snapshot.PendingActionCount);

        GameSessionCommandResult advanced = session.AdvanceWallMilliseconds(100);
        Assert.True(advanced.Accepted);
        Assert.Equal(1.0, advanced.Snapshot.SimulationTimeSeconds);
        Assert.Equal(0.95, advanced.Snapshot.NormalizedPowerFraction);

        Assert.True(session.Pause().Accepted);
        Assert.Equal(1.0, session.AdvanceWallMilliseconds(100).Snapshot.SimulationTimeSeconds);
        Assert.True(session.Resume().Accepted);
    }
}
