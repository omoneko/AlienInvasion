using AlienInvasion.Core;
using Xunit;

public class TripodWalkTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Rotate_90deg_maps_unit_x_to_unit_z()
    {
        float ndx, ndz;
        TripodWalk.Rotate(1f, 0f, (float)(System.Math.PI / 2.0), out ndx, out ndz);

        Assert.Equal(0f, ndx, 4);
        Assert.Equal(1f, ndz, 4);
    }

    [Fact]
    public void Rotate_360deg_is_identity()
    {
        float ndx, ndz;
        TripodWalk.Rotate(1f, 0f, (float)(System.Math.PI * 2.0), out ndx, out ndz);

        Assert.Equal(1f, ndx, 4);
        Assert.Equal(0f, ndz, 4);
    }

    [Fact]
    public void Rotate_zero_angle_is_identity()
    {
        float ndx, ndz;
        TripodWalk.Rotate(0.6f, 0.8f, 0f, out ndx, out ndz);

        Assert.Equal(0.6f, ndx, 4);
        Assert.Equal(0.8f, ndz, 4);
    }

    [Fact]
    public void BounceAxis_clamps_and_reflects_on_right_boundary()
    {
        float newPos, newDir;
        TripodWalk.BounceAxis(105f, 1f, 100f, out newPos, out newDir);

        Assert.Equal(100f, newPos);
        Assert.Equal(-1f, newDir);
    }

    [Fact]
    public void BounceAxis_clamps_and_reflects_on_left_boundary()
    {
        float newPos, newDir;
        TripodWalk.BounceAxis(-105f, -1f, 100f, out newPos, out newDir);

        Assert.Equal(-100f, newPos);
        Assert.Equal(1f, newDir);
    }

    [Fact]
    public void BounceAxis_right_boundary_reflects_even_when_dir_already_negative()
    {
        // dir was negative but position still overshot beyond half; must become inward (negative) magnitude preserved.
        float newPos, newDir;
        TripodWalk.BounceAxis(101f, -0.5f, 100f, out newPos, out newDir);

        Assert.Equal(100f, newPos);
        Assert.Equal(-0.5f, newDir);
    }

    [Fact]
    public void BounceAxis_within_range_is_unchanged()
    {
        float newPos, newDir;
        TripodWalk.BounceAxis(50f, 1f, 100f, out newPos, out newDir);

        Assert.Equal(50f, newPos);
        Assert.Equal(1f, newDir);
    }

    [Fact]
    public void BounceAxis_at_exact_boundary_is_unchanged()
    {
        float newPos, newDir;
        TripodWalk.BounceAxis(100f, 1f, 100f, out newPos, out newDir);

        Assert.Equal(100f, newPos);
        Assert.Equal(1f, newDir);
    }

    [Fact]
    public void StepComponent_advances_known_value()
    {
        float result = TripodWalk.StepComponent(10f, 1f, 30f, 0.5f);

        Assert.Equal(25f, result);
    }

    [Fact]
    public void StepComponent_negative_direction_moves_backward()
    {
        float result = TripodWalk.StepComponent(10f, -1f, 30f, 0.5f);

        Assert.Equal(-5f, result);
    }

    [Fact]
    public void StepComponent_zero_dt_is_unchanged()
    {
        float result = TripodWalk.StepComponent(10f, 1f, 30f, 0f);

        Assert.Equal(10f, result);
    }
}
