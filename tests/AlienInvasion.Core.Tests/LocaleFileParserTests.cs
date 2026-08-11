using System.Collections.Generic;
using AlienInvasion.Core;
using Xunit;

public class LocaleFileParserTests
{
    [Fact]
    public void Reads_key_equals_value()
    {
        Dictionary<string, string> map = LocaleFileParser.Parse("Options_Volume = Volume");
        Assert.Equal("Volume", map["Options_Volume"]);
    }

    [Fact]
    public void Trims_whitespace_around_both_sides()
    {
        Dictionary<string, string> map = LocaleFileParser.Parse("   Key   =   value   ");
        Assert.Equal("value", map["Key"]);
    }

    [Fact]
    public void Skips_blank_lines_and_comments()
    {
        Dictionary<string, string> map = LocaleFileParser.Parse(
            "# a comment = not a key\n\n   \nReal = yes\n");

        Assert.Single(map);
        Assert.Equal("yes", map["Real"]);
    }

    [Fact]
    public void Only_the_first_equals_splits_the_line()
    {
        // Values legitimately contain '=' - "Ratio = a = b" must not lose the tail.
        Dictionary<string, string> map = LocaleFileParser.Parse("Key = a = b");
        Assert.Equal("a = b", map["Key"]);
    }

    [Fact]
    public void Ignores_a_line_with_no_separator_or_an_empty_key()
    {
        Dictionary<string, string> map = LocaleFileParser.Parse("no separator here\n= orphan value\n");
        Assert.Empty(map);
    }

    [Fact]
    public void The_last_entry_for_a_key_wins()
    {
        // A hand-edited file with a duplicated key should be harmless, not an error.
        Dictionary<string, string> map = LocaleFileParser.Parse("Key = first\nKey = second");
        Assert.Equal("second", map["Key"]);
    }

    [Fact]
    public void Handles_crlf_line_endings()
    {
        // Translators edit these on Windows; a stray \r must not end up inside the value.
        Dictionary<string, string> map = LocaleFileParser.Parse("A = one\r\nB = two\r\n");
        Assert.Equal("one", map["A"]);
        Assert.Equal("two", map["B"]);
    }

    [Fact]
    public void Null_or_empty_input_yields_an_empty_map()
    {
        Assert.Empty(LocaleFileParser.Parse(null));
        Assert.Empty(LocaleFileParser.Parse(""));
    }

    [Fact]
    public void Unescapes_the_newline_escape()
    {
        Dictionary<string, string> map = LocaleFileParser.Parse(@"Key = first\nsecond");
        Assert.Equal("first\nsecond", map["Key"]);
    }

    [Fact]
    public void A_doubled_backslash_stays_a_literal_backslash()
    {
        Assert.Equal(@"a\nb", LocaleFileParser.Unescape(@"a\\nb"));
        Assert.Equal(@"C:\path", LocaleFileParser.Unescape(@"C:\\path"));
    }

    [Fact]
    public void An_unknown_escape_is_left_alone()
    {
        Assert.Equal(@"a\tb", LocaleFileParser.Unescape(@"a\tb"));
        Assert.Equal(@"trailing\", LocaleFileParser.Unescape(@"trailing\"));
    }

    [Fact]
    public void Unescape_leaves_a_value_with_no_backslash_untouched()
    {
        string plain = "nothing to do here";
        Assert.Same(plain, LocaleFileParser.Unescape(plain));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("two\nlines")]
    [InlineData("windows\r\nlines")]
    [InlineData(@"a backslash \ and a \n escape")]
    [InlineData("")]
    public void Escape_then_parse_round_trips_the_value(string original)
    {
        // This is what makes the generated template safe: a multi-line default written out by
        // LocaleLoader.EnsureTemplate must come back identical when the file is read again.
        string line = "Key = " + LocaleFileParser.Escape(original);
        Dictionary<string, string> map = LocaleFileParser.Parse(line);

        string expected = original.Replace("\r\n", "\n");
        Assert.Equal(expected, map.ContainsKey("Key") ? map["Key"] : "");
    }

    [Fact]
    public void Escape_turns_null_into_an_empty_string()
    {
        Assert.Equal("", LocaleFileParser.Escape(null));
    }

    [Fact]
    public void Escape_never_emits_a_raw_newline()
    {
        // One entry per line is the whole format; an escaped value must stay on its line.
        string escaped = LocaleFileParser.Escape("a\nb\r\nc");
        Assert.DoesNotContain("\n", escaped);
        Assert.DoesNotContain("\r", escaped);
    }
}
