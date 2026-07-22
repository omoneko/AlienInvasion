using AlienInvasion.Core;
using Xunit;

public class NuclearHitTestTests
{
    [Fact]
    public void DirectHit_at_impact_point_is_true()
    {
        Assert.True(NuclearHitTest.IsDirectHit(0f, 0f, 150f));
    }

    [Fact]
    public void DirectHit_within_radius_is_true()
    {
        // (90,90) -> 距離 ~127.3 < 150
        Assert.True(NuclearHitTest.IsDirectHit(90f, 90f, 150f));
    }

    [Fact]
    public void DirectHit_exactly_on_radius_is_true()
    {
        Assert.True(NuclearHitTest.IsDirectHit(150f, 0f, 150f));
    }

    [Fact]
    public void DirectHit_just_outside_radius_is_false()
    {
        Assert.False(NuclearHitTest.IsDirectHit(151f, 0f, 150f));
    }

    [Fact]
    public void DirectHit_far_away_is_false()
    {
        Assert.False(NuclearHitTest.IsDirectHit(1000f, 1000f, 150f));
    }

    [Fact]
    public void DirectHit_negative_offsets_use_magnitude()
    {
        Assert.True(NuclearHitTest.IsDirectHit(-100f, -100f, 150f));  // 距離 ~141.4 < 150
        Assert.False(NuclearHitTest.IsDirectHit(-120f, -120f, 150f)); // 距離 ~169.7 > 150
    }

    [Fact]
    public void DirectHit_zero_radius_only_exact_point()
    {
        Assert.True(NuclearHitTest.IsDirectHit(0f, 0f, 0f));
        Assert.False(NuclearHitTest.IsDirectHit(1f, 0f, 0f));
    }

    [Fact]
    public void DirectHit_negative_radius_is_false()
    {
        Assert.False(NuclearHitTest.IsDirectHit(0f, 0f, -1f));
    }
}
