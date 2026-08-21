using Minimal.Share.Extensions;

namespace Minimal.App.Tests.Unit.Extensions;

/// <summary>
/// Pins [D586-1]'s log-sanitisation fix: a single <c>char.IsControl</c> pass over the whole control range,
/// replacing the old body's partial `\n`/`\t`/`\r`/`\f`/`\0` handling (some removed, some space-substituted,
/// several control ranges — VT, ESC, DEL, NEL — untouched entirely). <see cref="SanitizeForLoggingExtensions"/>
/// has no callers in the repo today, so it is exercised directly.
/// </summary>
public class SanitizeForLoggingExtensionsTests
{
    #region Methods

    [Theory]
    [InlineData('\b', "backspace")] // U+0008
    [InlineData('\t', "tab")] // U+0009
    [InlineData('\n', "line feed")] // U+000A
    [InlineData('\v', "vertical tab")] // U+000B
    [InlineData('\f', "form feed")] // U+000C
    [InlineData('\r', "carriage return")] // U+000D
    [InlineData('\u001B', "escape")] // U+001B
    [InlineData('\u007F', "delete")] // U+007F
    [InlineData('\u0085', "next line")] // U+0085
    public void SanitizeForLogging_RemovesControlCharacter_WithoutLeavingASpace(char control, string name)
    {
        var value = $"Acme{control}Ltd";

        var result = value.SanitizeForLogging();

        // Regression guard: the old body substituted some of these (e.g. \0, \f, \r) with a space instead of
        // removing them — a survivor here would silently reappear as "Acme Ltd" rather than "AcmeLtd".
        result.ShouldBe("AcmeLtd", $"{name} (U+{(int)control:X4}) must be removed, not replaced with a space");
    }

    [Fact]
    public void SanitizeForLogging_ValueOfOnlyControlCharacters_ReturnsEmpty()
    {
        var value = "\0\f\r";

        var result = value.SanitizeForLogging();

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void SanitizeForLogging_ControlCharactersAtBothEnds_RemovesControlsAndTrimsSurroundingSpaces()
    {
        // Both mechanisms in the same call: control-character removal opens up plain spaces at the edges,
        // which Trim() must then also remove.
        var value = " Acme Pte Ltd ";

        var result = value.SanitizeForLogging();

        result.ShouldBe("Acme Pte Ltd");
    }

    [Fact]
    public void SanitizeForLogging_PlainValueWithInternalSpaces_SurvivesByteForByte()
    {
        // The fix must not cheat by stripping whitespace generally — only control characters are removed.
        const string value = "Acme Pte Ltd";

        var result = value.SanitizeForLogging();

        result.ShouldBe(value);
    }

    #endregion
}
