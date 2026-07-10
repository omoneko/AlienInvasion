using System.Collections.Generic;
using AlienInvasion.Core;
using Xunit;

public class MtlParserTests
{
    // Assert.Equal(double, double, int) takes decimal-place precision, not a raw epsilon.
    private const int Precision = 4;

    [Fact]
    public void Parses_two_materials_with_Kd_and_d()
    {
        string mtl =
            "newmtl MetallicGray\n" +
            "Kd 0.036653 0.036653 0.036653\n" +
            "d 1.000000\n" +
            "newmtl マテリアル\n" +
            "Kd 0.182665 0.791351 0.800007\n" +
            "d 0.900000\n";

        Dictionary<string, MtlColor> materials = MtlParser.Parse(mtl);

        Assert.Equal(2, materials.Count);

        MtlColor gray = materials["MetallicGray"];
        Assert.Equal(0.036653, gray.R, Precision);
        Assert.Equal(0.036653, gray.G, Precision);
        Assert.Equal(0.036653, gray.B, Precision);
        Assert.Equal(1.0, gray.Alpha, Precision);

        MtlColor jp = materials["マテリアル"];
        Assert.Equal(0.182665, jp.R, Precision);
        Assert.Equal(0.791351, jp.G, Precision);
        Assert.Equal(0.800007, jp.B, Precision);
        Assert.Equal(0.900000, jp.Alpha, Precision);
    }

    [Fact]
    public void Material_without_d_line_defaults_to_opaque()
    {
        string mtl =
            "newmtl Solid\n" +
            "Kd 0.5 0.5 0.5\n";

        Dictionary<string, MtlColor> materials = MtlParser.Parse(mtl);

        Assert.Equal(1.0f, materials["Solid"].Alpha);
    }

    [Fact]
    public void NonAscii_material_name_with_dot_is_preserved_exactly()
    {
        string mtl =
            "newmtl マテリアル.001\n" +
            "Kd 0.1 0.2 0.3\n" +
            "d 0.5\n";

        Dictionary<string, MtlColor> materials = MtlParser.Parse(mtl);

        Assert.True(materials.ContainsKey("マテリアル.001"));
    }

    [Fact]
    public void Empty_input_returns_empty_dictionary_without_throwing()
    {
        Dictionary<string, MtlColor> materials = MtlParser.Parse("");

        Assert.Empty(materials);
    }

    [Fact]
    public void CRLF_line_endings_parse_identically_to_LF()
    {
        string lf =
            "newmtl Mat1\n" +
            "Kd 0.1 0.2 0.3\n" +
            "d 0.75\n";
        string crlf = lf.Replace("\n", "\r\n");

        Dictionary<string, MtlColor> a = MtlParser.Parse(lf);
        Dictionary<string, MtlColor> b = MtlParser.Parse(crlf);

        Assert.Equal(a["Mat1"].R, b["Mat1"].R, Precision);
        Assert.Equal(a["Mat1"].Alpha, b["Mat1"].Alpha, Precision);
    }

    [Fact]
    public void Malformed_Kd_line_is_skipped_without_throwing()
    {
        string mtl =
            "newmtl Mat1\n" +
            "Kd not a number\n" +
            "d 0.5\n";

        Dictionary<string, MtlColor> materials = MtlParser.Parse(mtl);

        // Kd failed to parse, so R/G/B stay at the default (1,1,1); d still applies.
        MtlColor mat = materials["Mat1"];
        Assert.Equal(1.0f, mat.R);
        Assert.Equal(1.0f, mat.G);
        Assert.Equal(1.0f, mat.B);
        Assert.Equal(0.5, mat.Alpha, Precision);
    }
}
