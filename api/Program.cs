using HistoryMap.Api.Configuration;
using HistoryMap.Api.Data;
using HistoryMap.Api.Services;
using HistoryMap.Api.Tools;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<BlobStorageOptions>(
    builder.Configuration.GetSection(BlobStorageOptions.SectionName));

builder.Services.AddDbContext<HistoryMapDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("HistoryMap")
        ?? "Server=(localdb)\\mssqllocaldb;Database=HistoryMap;Trusted_Connection=True;TrustServerCertificate=True";
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 3);
        sql.CommandTimeout(30);
    });
});

builder.Services.AddScoped<IPlacesService, PlacesService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "History Map API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(
                "https://jsgrundy.com",
                "https://www.jsgrundy.com",
                "http://localhost:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5500"));
});

var app = builder.Build();

if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    var code = await SeedImporter.RunAsync(app.Services, args);
    Environment.Exit(code);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();

// Expose for WebApplicationFactory integration tests.
public partial class Program;
