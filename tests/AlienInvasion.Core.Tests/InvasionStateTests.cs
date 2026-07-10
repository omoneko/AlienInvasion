using AlienInvasion.Core;
using Xunit;

public class InvasionStateTests
{
    [Theory]
    [InlineData(InvasionState.Idle, InvasionState.Descending, true)]
    [InlineData(InvasionState.Descending, InvasionState.Bombarding, true)]
    [InlineData(InvasionState.Bombarding, InvasionState.Ascending, true)]
    [InlineData(InvasionState.Ascending, InvasionState.TripodDeploy, true)]
    [InlineData(InvasionState.TripodDeploy, InvasionState.TripodsActive, true)]
    [InlineData(InvasionState.TripodsActive, InvasionState.Departing, true)]
    [InlineData(InvasionState.Departing, InvasionState.Done, true)]
    [InlineData(InvasionState.Done, InvasionState.Idle, true)]
    [InlineData(InvasionState.Idle, InvasionState.Bombarding, false)]
    [InlineData(InvasionState.Descending, InvasionState.Idle, false)]
    [InlineData(InvasionState.Bombarding, InvasionState.Done, false)]
    [InlineData(InvasionState.Ascending, InvasionState.Done, false)]
    [InlineData(InvasionState.TripodDeploy, InvasionState.Done, false)]
    [InlineData(InvasionState.TripodsActive, InvasionState.Done, false)]
    public void CanTransition_follows_linear_cycle(InvasionState from, InvasionState to, bool expected)
    {
        Assert.Equal(expected, InvasionStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(InvasionState.Idle, InvasionState.Descending)]
    [InlineData(InvasionState.Descending, InvasionState.Bombarding)]
    [InlineData(InvasionState.Bombarding, InvasionState.Ascending)]
    [InlineData(InvasionState.Ascending, InvasionState.TripodDeploy)]
    [InlineData(InvasionState.TripodDeploy, InvasionState.TripodsActive)]
    [InlineData(InvasionState.TripodsActive, InvasionState.Departing)]
    [InlineData(InvasionState.Departing, InvasionState.Done)]
    [InlineData(InvasionState.Done, InvasionState.Idle)]
    public void Next_returns_the_following_state(InvasionState current, InvasionState expected)
    {
        Assert.Equal(expected, InvasionStateMachine.Next(current));
    }

    [Fact]
    public void Next_traverses_the_full_cycle_including_tripod_phases()
    {
        InvasionState state = InvasionState.Idle;
        var visited = new System.Collections.Generic.List<InvasionState> { state };

        for (int i = 0; i < 8; i++)
        {
            state = InvasionStateMachine.Next(state);
            visited.Add(state);
        }

        Assert.Equal(new[]
        {
            InvasionState.Idle,
            InvasionState.Descending,
            InvasionState.Bombarding,
            InvasionState.Ascending,
            InvasionState.TripodDeploy,
            InvasionState.TripodsActive,
            InvasionState.Departing,
            InvasionState.Done,
            InvasionState.Idle
        }, visited);
    }

    [Fact]
    public void ContaminationZone_stores_fields()
    {
        var z = new ContaminationZone(10f, 20f, 60f, 123L);
        Assert.Equal(10f, z.CenterX);
        Assert.Equal(20f, z.CenterZ);
        Assert.Equal(60f, z.Radius);
        Assert.Equal(123L, z.StartTicks);
    }
}
