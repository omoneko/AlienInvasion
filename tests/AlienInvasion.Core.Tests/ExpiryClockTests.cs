using System;
using AlienInvasion.Core;
using Xunit;

public class ExpiryClockTests
{
    [Fact]
    public void Not_expired_before_months_elapse()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 2, 28); // under two months, before 1 March
        Assert.False(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 2));
    }

    [Fact]
    public void Expired_exactly_at_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 3, 1); // exactly two months later
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 2));
    }

    [Fact]
    public void Expired_after_boundary()
    {
        var start = new DateTime(2000, 6, 15);
        var now = new DateTime(2000, 9, 1); // past the two-month mark of 15 August
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 2));
    }

    [Fact]
    public void Days_not_elapsed_before_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 1, 14); // before the 14-day mark of 15 January
        Assert.False(ExpiryClock.HasElapsedDays(start.Ticks, now.Ticks, 14));
    }

    [Fact]
    public void Days_elapsed_exactly_at_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 1, 15); // exactly 14 days later
        Assert.True(ExpiryClock.HasElapsedDays(start.Ticks, now.Ticks, 14));
    }

    [Fact]
    public void Days_elapsed_after_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 2, 1); // well past the 14-day mark
        Assert.True(ExpiryClock.HasElapsedDays(start.Ticks, now.Ticks, 14));
    }
}
