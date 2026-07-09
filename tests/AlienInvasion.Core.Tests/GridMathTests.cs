using System.Collections.Generic;
using AlienInvasion.Core;
using Xunit;

public class GridMathTests
{
    [Fact]
    public void WorldToCell_maps_origin_to_center()
    {
        Assert.Equal(256, GridMath.WorldToCell(0f));
    }

    [Fact]
    public void WorldToCell_clamps_out_of_range()
    {
        Assert.Equal(0, GridMath.WorldToCell(-100000f));
        Assert.Equal(511, GridMath.WorldToCell(100000f));
    }

    [Fact]
    public void CellIndex_is_row_major()
    {
        Assert.Equal(2 * 512 + 3, GridMath.CellIndex(3, 2));
    }

    [Fact]
    public void CellsInRadius_contains_center_cell()
    {
        var cells = GridMath.CellsInRadius(0f, 0f, 100f);
        int centerIndex = GridMath.CellIndex(256, 256);
        Assert.Contains(centerIndex, cells);
    }

    [Fact]
    public void CellsInRadius_excludes_cells_outside_radius()
    {
        var cells = GridMath.CellsInRadius(0f, 0f, 10f);
        foreach (var idx in cells)
        {
            int cz = idx / 512;
            int cx = idx % 512;
            Assert.InRange(cx, 255, 257);
            Assert.InRange(cz, 255, 257);
        }
    }

    [Fact]
    public void CellsInRadius_indices_are_unique()
    {
        var cells = GridMath.CellsInRadius(0f, 0f, 200f);
        var seen = new HashSet<int>();
        foreach (var idx in cells) Assert.True(seen.Add(idx), "duplicate index " + idx);
    }
}
