using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HistoryMap.Api.Tests;

public class SwaggerEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SwaggerEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // Avoid depending on a live SQL instance; Swagger does not query the DB.
            builder.UseSetting(
                "ConnectionStrings:HistoryMap",
                "Server=(localdb)\\mssqllocaldb;Database=HistoryMapSwaggerTest;Trusted_Connection=True;TrustServerCertificate=True");
        });
    }

    [Fact]
    public async Task SwaggerUi_IsAvailable_InProduction()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        response.IsSuccessStatusCode.Should().BeTrue(
            because: "Swagger UI must be served outside Development (including Azure Production)");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("swagger", because: "response should be the Swagger UI page");
    }

    [Fact]
    public async Task SwaggerJson_IsAvailable_InProduction()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.IsSuccessStatusCode.Should().BeTrue();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("History Map API");
    }
}
