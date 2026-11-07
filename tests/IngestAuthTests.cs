using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using HistoryMap.Api.Auth;
using HistoryMap.Api.Data;
using HistoryMap.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HistoryMap.Api.Tests;

public class IngestAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IngestAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpsertPlace_WithoutToken_Returns401()
    {
        var client = CreateClient(authenticate: false);

        var response = await client.PutAsJsonAsync(
            "/api/v1/ingest/places/TEST001",
            new UpsertPlaceRequest
            {
                Name = "Unauthorized Place",
                Latitude = 38.9m,
                Longitude = -77.0m,
                SummaryText = "Should not write"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpsertPlace_WithTestAuth_UpsertsIdempotently()
    {
        var client = CreateClient(authenticate: true);

        var body = new UpsertPlaceRequest
        {
            Name = "Auth Test Place",
            Latitude = 38.9072m,
            Longitude = -77.0369m,
            City = "Washington",
            State = "DC",
            PlaceType = "building",
            IsNhl = false,
            SummaryText = "Created via ingest API test.",
            SourceHeading = "Test",
            Confidence = 0.9m
        };

        var create = await client.PutAsJsonAsync("/api/v1/ingest/places/TEST001", body);
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<UpsertPlaceResult>();
        created.Should().NotBeNull();
        created!.Ref.Should().Be("TEST001");
        created.Created.Should().BeTrue();
        created.SummaryUpdated.Should().BeTrue();

        body.SummaryText = "Updated summary.";
        var update = await client.PutAsJsonAsync("/api/v1/ingest/places/TEST001", body);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<UpsertPlaceResult>();
        updated!.Created.Should().BeFalse();
        updated.SummaryUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task PublicPlaces_RemainAnonymous()
    {
        var client = CreateClient(authenticate: false);

        var response = await client.GetAsync("/api/v1/places/with-photos");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpsertPhoto_WithAuth_RequiresExistingPlace()
    {
        var client = CreateClient(authenticate: true);

        var placeResponse = await client.PutAsJsonAsync(
            "/api/v1/ingest/places/PHOTO001",
            new UpsertPlaceRequest
            {
                Name = "Photo Place",
                Latitude = 1m,
                Longitude = 2m,
                SummaryText = "Has a photo soon."
            });
        placeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await placeResponse.Content.ReadAsStringAsync());

        var photoResponse = await client.PutAsJsonAsync(
            "/api/v1/ingest/photos",
            new UpsertPhotoRequest
            {
                Ref = "PHOTO001",
                BlobPath = "places/PHOTO001/main.jpg",
                Caption = "Main",
                Source = "test",
                WidthPx = 800,
                HeightPx = 600,
                SortOrder = 0
            });

        photoResponse.StatusCode.Should().Be(HttpStatusCode.OK, await photoResponse.Content.ReadAsStringAsync());
        var photo = await photoResponse.Content.ReadFromJsonAsync<UpsertPhotoResult>();
        photo!.Created.Should().BeTrue();
        photo.BlobPath.Should().Be("places/PHOTO001/main.jpg");
    }

    private HttpClient CreateClient(bool authenticate)
    {
        var dbName = $"IngestAuth_{Guid.NewGuid():N}";
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                RemoveDbContextRegistrations(services);
                services.AddDbContext<HistoryMapDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });
            });
        });

        var client = factory.CreateClient();
        if (authenticate)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "test");
        return client;
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        services.RemoveAll(typeof(HistoryMapDbContext));
        services.RemoveAll(typeof(DbContextOptions<HistoryMapDbContext>));
        services.RemoveAll(typeof(IDbContextOptionsConfiguration<HistoryMapDbContext>));

        // Remove any leftover EF options configuration descriptors for this context.
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions)
                || (d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                    && d.ServiceType.GenericTypeArguments[0] == typeof(HistoryMapDbContext)))
            .ToList();
        foreach (var descriptor in toRemove)
            services.Remove(descriptor);
    }
}

/// <summary>Test scheme that authenticates when an Authorization header is present.</summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ingest-test"),
            new Claim(ClaimTypes.Role, AuthPolicies.IngestWriteRole),
            new Claim("roles", AuthPolicies.IngestWriteRole)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
