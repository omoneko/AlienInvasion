using System;
using AlienInvasion.Core;
using Xunit;

public class ExpiryClockTests
{
    [Fact]
    public void Not_expired_before_years_elapse()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 12, 31);
        Assert.False(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 1));
    }

    [Fact]
    public void Expired_exactly_at_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2001, 1, 1);
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 1));
    }

    [Fact]
    public void Expired_after_boundary()
    {
        var start = new DateTime(2000, 6, 15);
        var now = new DateTime(2002, 1, 1);
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 1));
    }
}
