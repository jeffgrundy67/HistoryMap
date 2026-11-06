using FluentAssertions;
using HistoryMap.Api.Controllers;

namespace HistoryMap.Api.Tests;

public class BboxParsingTests
{
    [Fact]
    public void TryParseBbox_Valid_ParsesInvariantCulture()
    {
        var ok = PlacesController.TryParseBbox(
            "-77.1,38.85,-76.9,38.95",
            out var minLng,
            out var minLat,
            out var maxLng,
            out var maxLat,
            out var error);

        ok.Should().BeTrue();
        error.Should().BeEmpty();
        minLng.Should().Be(-77.1m);
        minLat.Should().Be(38.85m);
        maxLng.Should().Be(-76.9m);
        maxLat.Should().Be(38.95m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("a,b,c,d")]
    [InlineData("10,0,0,1")]
    [InlineData("-200,0,0,1")]
    public void TryParseBbox_Invalid_ReturnsFalse(string? bbox)
    {
        var ok = PlacesController.TryParseBbox(bbox, out _, out _, out _, out _, out var error);
        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }
}
