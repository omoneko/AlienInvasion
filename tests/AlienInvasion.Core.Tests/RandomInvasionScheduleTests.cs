using System;
using AlienInvasion.Core;
using Xunit;

public class RandomInvasionScheduleTests
{
    private static long Day(double days)
    {
        return new DateTime(2000, 1, 1).AddDays(days).Ticks;
    }

    [Fact]
    public void A_check_is_due_once_a_full_in_game_day_has_passed()
    {
        long start = Day(0);

        Assert.False(RandomInvasionSchedule.IsCheckDue(start, Day(0)));
        Assert.False(RandomInvasionSchedule.IsCheckDue(start, Day(0.5)));
        Assert.False(RandomInvasionSchedule.IsCheckDue(start, Day(0.999)));
        Assert.True(RandomInvasionSchedule.IsCheckDue(start, Day(1)));
        Assert.True(RandomInvasionSchedule.IsCheckDue(start, Day(30)));
    }

    [Fact]
    public void A_clock_that_moved_backwards_is_never_due()
    {
        // What loading a different save looks like. The caller re-primes on level load; this is
        // the second line of defence.
        Assert.False(RandomInvasionSchedule.IsCheckDue(Day(100), Day(1)));
    }

    [Fact]
    public void The_threshold_matches_the_configured_average()
    {
        // 1 day => every check fires; 10 days => one in ten.
        Assert.True(RandomInvasionSchedule.ShouldFire(1, 9999));
        Assert.True(RandomInvasionSchedule.ShouldFire(10, 999));
        Assert.False(RandomInvasionSchedule.ShouldFire(10, 1000));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(365)]
    public void The_firing_rate_is_one_in_averageDays(int averageDays)
    {
        int fired = 0;
        for (int roll = 0; roll < RandomInvasionSchedule.RollRange; roll++)
        {
            if (RandomInvasionSchedule.ShouldFire(averageDays, roll)) fired++;
        }

        // Exactly the integer threshold: no rounding surprises either way.
        Assert.Equal(RandomInvasionSchedule.RollRange / averageDays, fired);

        // And that threshold is never generous - a destructive event must not come out more
        // often than the label promises.
        double actualMeanDays = (double)RandomInvasionSchedule.RollRange / fired;
        Assert.True(actualMeanDays >= averageDays,
            "mean interval " + actualMeanDays + " days is shorter than the configured " + averageDays);
    }

    [Fact]
    public void A_non_positive_average_never_fires()
    {
        // A corrupt or hand-edited setting must not become an invasion every day.
        for (int roll = 0; roll < RandomInvasionSchedule.RollRange; roll += 137)
        {
            Assert.False(RandomInvasionSchedule.ShouldFire(0, roll));
            Assert.False(RandomInvasionSchedule.ShouldFire(-1, roll));
        }
    }

    [Fact]
    public void A_roll_outside_the_range_never_fires()
    {
        Assert.False(RandomInvasionSchedule.ShouldFire(10, -1));
        Assert.False(RandomInvasionSchedule.ShouldFire(10, RandomInvasionSchedule.RollRange));
        Assert.False(RandomInvasionSchedule.ShouldFire(1, RandomInvasionSchedule.RollRange));
    }

    [Fact]
    public void The_average_is_clamped_into_the_slider_range()
    {
        Assert.Equal(RandomInvasionSchedule.MinAverageDays, RandomInvasionSchedule.ClampAverageDays(0));
        Assert.Equal(RandomInvasionSchedule.MinAverageDays, RandomInvasionSchedule.ClampAverageDays(-99));
        Assert.Equal(RandomInvasionSchedule.MaxAverageDays, RandomInvasionSchedule.ClampAverageDays(99999));
        Assert.Equal(30, RandomInvasionSchedule.ClampAverageDays(30));
    }

    [Fact]
    public void The_default_sits_inside_the_offered_range()
    {
        Assert.InRange(RandomInvasionSchedule.DefaultAverageDays,
            RandomInvasionSchedule.MinAverageDays, RandomInvasionSchedule.MaxAverageDays);
        // The slider steps in fives, so the default must land on a step.
        Assert.Equal(0, RandomInvasionSchedule.DefaultAverageDays % 5);
    }

    [Fact]
    public void Every_offered_average_produces_a_reachable_rate()
    {
        // A day count so large that the threshold floors to zero would be a switch that is on
        // and can never fire - exactly the bug this replaces.
        for (int days = RandomInvasionSchedule.MinAverageDays; days <= RandomInvasionSchedule.MaxAverageDays; days++)
        {
            Assert.True(RandomInvasionSchedule.RollRange / days >= 1,
                days + " days floors the threshold to zero");
        }
    }
}
