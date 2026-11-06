using FluentAssertions;
using HistoryMap.Api.Configuration;

namespace HistoryMap.Api.Tests;

public class BlobStorageOptionsTests
{
    [Fact]
    public void BuildUrl_JoinsBaseAndPath_WithoutDoubleSlash()
    {
        var opts = new BlobStorageOptions
        {
            ContainerBaseUrl = "https://acct.blob.core.windows.net/photos/"
        };

        opts.BuildUrl("/places/ABC/main.jpg")
            .Should().Be("https://acct.blob.core.windows.net/photos/places/ABC/main.jpg");
    }

    [Fact]
    public void BuildUrl_EmptyPath_ReturnsEmpty()
    {
        var opts = new BlobStorageOptions { ContainerBaseUrl = "https://example/photos" };
        opts.BuildUrl(" ").Should().BeEmpty();
    }
}
