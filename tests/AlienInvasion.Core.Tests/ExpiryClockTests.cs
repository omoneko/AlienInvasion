using System;
using AlienInvasion.Core;
using Xunit;

public class ExpiryClockTests
{
    [Fact]
    public void Not_expired_before_months_elapse()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 2, 28); // 2か月未満(3/1より前)
        Assert.False(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 2));
    }

    [Fact]
    public void Expired_exactly_at_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 3, 1); // ちょうど2か月後
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 2));
    }

    [Fact]
    public void Expired_after_boundary()
    {
        var start = new DateTime(2000, 6, 15);
        var now = new DateTime(2000, 9, 1); // 2か月境界(8/15)を過ぎている
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 2));
    }
}
