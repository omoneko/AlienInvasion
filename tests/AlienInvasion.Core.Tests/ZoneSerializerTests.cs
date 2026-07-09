using System.Collections.Generic;
using AlienInvasion.Core;
using Xunit;

public class ZoneSerializerTests
{
    [Fact]
    public void Round_trips_zones()
    {
        var zones = new List<ContaminationZone>
        {
            new ContaminationZone(100f, -200f, 60f, 630000000000000000L),
            new ContaminationZone(0f, 0f, 40f, 630000000000000001L),
        };
        byte[] bytes = ZoneSerializer.Serialize(zones);
        List<ContaminationZone> back = ZoneSerializer.Deserialize(bytes);

        Assert.Equal(2, back.Count);
        Assert.Equal(100f, back[0].CenterX);
        Assert.Equal(-200f, back[0].CenterZ);
        Assert.Equal(60f, back[0].Radius);
        Assert.Equal(630000000000000000L, back[0].StartTicks);
        Assert.Equal(630000000000000001L, back[1].StartTicks);
    }

    [Fact]
    public void Empty_list_round_trips()
    {
        byte[] bytes = ZoneSerializer.Serialize(new List<ContaminationZone>());
        Assert.Empty(ZoneSerializer.Deserialize(bytes));
    }

    [Fact]
    public void Null_input_returns_empty()
    {
        Assert.Empty(ZoneSerializer.Deserialize(null));
    }

    [Fact]
    public void Corrupt_input_returns_empty_without_throwing()
    {
        Assert.Empty(ZoneSerializer.Deserialize(new byte[] { 9, 9, 9 }));
    }
}
