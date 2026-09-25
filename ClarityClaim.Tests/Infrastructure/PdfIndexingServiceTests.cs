using ClarityClaim.Infrastructure.Pdf;
using FluentAssertions;
using Xunit;

namespace ClarityClaim.Tests.Infrastructure;

// INF-20 -- LCD_MAP matches the corrected IDs, not the stale L35062/L34067/L35101 values
public class PdfIndexingServiceTests
{
    [Fact]
    public void LcdMap_UsesCorrectedIds()
    {
        PdfIndexingService.LCD_MAP.Keys.Should().BeEquivalentTo(
            ["L34220", "L36839", "L39266", "A56903", "A57206", "A59036"]);
    }

    [Fact]
    public void LcdMap_DoesNotContainStaleIds()
    {
        var allText = string.Join(" ", PdfIndexingService.LCD_MAP.Keys.Concat(PdfIndexingService.LCD_MAP.Values));
        allText.Should().NotContain("L35062");
        allText.Should().NotContain("L34067");
        allText.Should().NotContain("L35101");
    }
}
