using AlienInvasion.Core;
using Xunit;

public class MovementMathTests
{
    [Fact]
    public void EaseInOut_endpoints_are_stable()
    {
        Assert.Equal(0f, MovementMath.EaseInOut(0f), 3);
        Assert.Equal(1f, MovementMath.EaseInOut(1f), 3);
    }

    [Fact]
    public void EaseInOut_midpoint_is_half()
    {
        Assert.Equal(0.5f, MovementMath.EaseInOut(0.5f), 3);
    }

    [Fact]
    public void Lerp_clamps_t_below_zero()
    {
        Assert.Equal(10f, MovementMath.Lerp(10f, 20f, -1f));
    }

    [Fact]
    public void Lerp_clamps_t_above_one()
    {
        Assert.Equal(20f, MovementMath.Lerp(10f, 20f, 2f));
    }

    [Fact]
    public void Lerp_interpolates_at_half()
    {
        Assert.Equal(15f, MovementMath.Lerp(10f, 20f, 0.5f));
    }

    [Fact]
    public void IsNear_true_within_epsilon()
    {
        Assert.True(MovementMath.IsNear(10.0f, 10.05f, 0.1f));
    }

    [Fact]
    public void IsNear_false_outside_epsilon()
    {
        Assert.False(MovementMath.IsNear(10.0f, 10.5f, 0.1f));
    }
}
