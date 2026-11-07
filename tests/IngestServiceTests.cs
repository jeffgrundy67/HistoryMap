using FluentAssertions;
using HistoryMap.Api.Data;
using HistoryMap.Api.Models;
using HistoryMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HistoryMap.Api.Tests;

public class IngestServiceTests
{
    private static HistoryMapDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<HistoryMapDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new HistoryMapDbContext(options);
    }

    [Fact]
    public async Task UpsertPlace_IsIdempotentByRef()
    {
        await using var db = CreateDb(nameof(UpsertPlace_IsIdempotentByRef));
        var ingest = new IngestService(db, NullLogger<IngestService>.Instance);

        var first = await ingest.UpsertPlaceAsync(new UpsertPlaceRequest
        {
            Ref = "R1",
            Name = "One",
            Latitude = 10m,
            Longitude = 20m,
            SummaryText = "First"
        });
        var second = await ingest.UpsertPlaceAsync(new UpsertPlaceRequest
        {
            Ref = "R1",
            Name = "One Updated",
            Latitude = 11m,
            Longitude = 21m,
            SummaryText = "Second"
        });

        first.Created.Should().BeTrue();
        second.Created.Should().BeFalse();
        (await db.Places.CountAsync()).Should().Be(1);
        var place = await db.Places.SingleAsync();
        place.Name.Should().Be("One Updated");
        place.Latitude.Should().Be(11m);
        (await db.Summaries.SingleAsync()).SummaryText.Should().Be("Second");
    }

    [Fact]
    public async Task UpsertPhoto_IsIdempotentByRefAndBlobPath()
    {
        await using var db = CreateDb(nameof(UpsertPhoto_IsIdempotentByRefAndBlobPath));
        var ingest = new IngestService(db, NullLogger<IngestService>.Instance);
        await ingest.UpsertPlaceAsync(new UpsertPlaceRequest { Ref = "R2", Name = "P", Latitude = 1, Longitude = 2 });

        var a = await ingest.UpsertPhotoAsync(new UpsertPhotoRequest
        {
            Ref = "R2",
            BlobPath = "places/R2/a.jpg",
            Caption = "A"
        });
        var b = await ingest.UpsertPhotoAsync(new UpsertPhotoRequest
        {
            Ref = "R2",
            BlobPath = "places/R2/a.jpg",
            Caption = "A2",
            SortOrder = 3
        });

        a.Created.Should().BeTrue();
        b.Created.Should().BeFalse();
        var photo = await db.Photos.SingleAsync();
        photo.Caption.Should().Be("A2");
        photo.SortOrder.Should().Be(3);
    }

    [Fact]
    public async Task UpsertBatch_ReportsPerItemErrors()
    {
        await using var db = CreateDb(nameof(UpsertBatch_ReportsPerItemErrors));
        var ingest = new IngestService(db, NullLogger<IngestService>.Instance);

        var result = await ingest.UpsertBatchAsync(new IngestBatchRequest
        {
            Places = [new UpsertPlaceRequest { Ref = "B1", Name = "Batch", Latitude = 1, Longitude = 2, SummaryText = "Ok" }],
            Photos =
            [
                new UpsertPhotoRequest { Ref = "MISSING", BlobPath = "x.jpg" },
                new UpsertPhotoRequest { Ref = "B1", BlobPath = "places/B1/main.jpg", Caption = "Main" }
            ]
        });

        result.Places.Should().ContainSingle(p => p.Ref == "B1");
        result.Photos.Should().ContainSingle(p => p.Ref == "B1");
        result.Errors.Should().ContainSingle(e => e.Contains("MISSING"));
    }
}
