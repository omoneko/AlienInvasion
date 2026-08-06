using AlienInvasion.Core;
using Xunit;

public class ToppleAnimationTests
{
    [Fact]
    public void FallFraction_before_start_is_zero()
    {
        Assert.Equal(0f, ToppleAnimation.FallFraction(0f, 1.4f));
    }

    [Fact]
    public void FallFraction_at_end_is_one()
    {
        Assert.Equal(1f, ToppleAnimation.FallFraction(1.4f, 1.4f));
    }

    [Fact]
    public void FallFraction_past_end_is_clamped_to_one()
    {
        Assert.Equal(1f, ToppleAnimation.FallFraction(10f, 1.4f));
    }

    [Fact]
    public void FallFraction_is_ease_in_quadratic_at_midpoint()
    {
        // Halfway through, t=0.5 and t squared is 0.25 - it starts slowly.
        Assert.Equal(0.25f, ToppleAnimation.FallFraction(0.7f, 1.4f), 4);
    }

    [Fact]
    public void FallFraction_zero_duration_returns_one()
    {
        Assert.Equal(1f, ToppleAnimation.FallFraction(0f, 0f));
    }

    [Fact]
    public void FallFraction_negative_elapsed_is_zero()
    {
        Assert.Equal(0f, ToppleAnimation.FallFraction(-1f, 1.4f));
    }

    [Fact]
    public void IsFinished_false_during_fall()
    {
        Assert.False(ToppleAnimation.IsFinished(1.0f, 1.4f, 2.0f));
    }

    [Fact]
    public void IsFinished_false_during_dwell()
    {
        // The fall (1.4) is over but it is still lying there (2.0), so under 3.4 in total.
        Assert.False(ToppleAnimation.IsFinished(3.0f, 1.4f, 2.0f));
    }

    [Fact]
    public void IsFinished_true_after_fall_plus_dwell()
    {
        Assert.True(ToppleAnimation.IsFinished(3.4f, 1.4f, 2.0f));
    }

    [Fact]
    public void IsFinished_true_well_past_end()
    {
        Assert.True(ToppleAnimation.IsFinished(100f, 1.4f, 2.0f));
    }
}
