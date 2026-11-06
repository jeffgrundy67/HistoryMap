using FluentAssertions;
using HistoryMap.Api.Configuration;
using HistoryMap.Api.Data;
using HistoryMap.Api.Models;
using HistoryMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HistoryMap.Api.Tests;

public class PlacesServiceTests
{
    private static HistoryMapDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<HistoryMapDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new HistoryMapDbContext(options);
    }

    private static PlacesService CreateService(HistoryMapDbContext db, string baseUrl = "https://cdn.example/photos")
    {
        var opts = Options.Create(new BlobStorageOptions { ContainerBaseUrl = baseUrl });
        return new PlacesService(db, opts);
    }

    [Fact]
    public async Task GetPlacesInBbox_FiltersByViewport()
    {
        await using var db = CreateDb(nameof(GetPlacesInBbox_FiltersByViewport));
        db.Places.AddRange(
            new Place { Ref = "IN1", Name = "Inside", Latitude = 38.9m, Longitude = -77.0m, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Place { Ref = "OUT1", Name = "Outside", Latitude = 40.0m, Longitude = -74.0m, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var pins = await service.GetPlacesInBboxAsync(-77.1m, 38.8m, -76.9m, 39.0m, 12);

        pins.Should().ContainSingle(p => p.Ref == "IN1");
        pins.Should().NotContain(p => p.Ref == "OUT1");
    }

    [Fact]
    public async Task GetPlaceByRef_ReturnsBlobUrlsNotRawPaths()
    {
        await using var db = CreateDb(nameof(GetPlaceByRef_ReturnsBlobUrlsNotRawPaths));
        db.Places.Add(new Place
        {
            Ref = "P1",
            Name = "Place One",
            Latitude = 1m,
            Longitude = 2m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.Summaries.Add(new Summary
        {
            Ref = "P1",
            SummaryText = "Hello",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.Photos.Add(new Photo
        {
            Ref = "P1",
            BlobPath = "places/P1/a.jpg",
            SortOrder = 0,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var detail = await CreateService(db).GetPlaceByRefAsync("P1");

        detail.Should().NotBeNull();
        detail!.Summary!.SummaryText.Should().Be("Hello");
        detail.Photos.Should().ContainSingle()
            .Which.Url.Should().Be("https://cdn.example/photos/places/P1/a.jpg");
    }

    [Fact]
    public async Task GetPlaceRefsWithPhotos_ReturnsDistinctRefs()
    {
        await using var db = CreateDb(nameof(GetPlaceRefsWithPhotos_ReturnsDistinctRefs));
        db.Places.Add(new Place { Ref = "A", Name = "A", Latitude = 0, Longitude = 0, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        db.Places.Add(new Place { Ref = "B", Name = "B", Latitude = 0, Longitude = 0, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        db.Photos.Add(new Photo { Ref = "A", BlobPath = "a/1.jpg", CreatedAtUtc = DateTime.UtcNow });
        db.Photos.Add(new Photo { Ref = "A", BlobPath = "a/2.jpg", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPlaceRefsWithPhotosAsync();
        result.Refs.Should().Equal("A");
    }
}
