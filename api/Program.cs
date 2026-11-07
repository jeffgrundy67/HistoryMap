using System.Security.Claims;
using HistoryMap.Api.Auth;
using HistoryMap.Api.Configuration;
using HistoryMap.Api.Data;
using HistoryMap.Api.Services;
using HistoryMap.Api.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;

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
builder.Services.AddScoped<IIngestService, IngestService>();

var azureAdSection = builder.Configuration.GetSection("AzureAd");
var azureAdConfigured =
    !string.IsNullOrWhiteSpace(azureAdSection["TenantId"])
    && !string.IsNullOrWhiteSpace(azureAdSection["ClientId"]);

if (azureAdConfigured)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(azureAdSection);
}
else
{
    // Local/seed without Entra: JWT scheme present so [Authorize] returns 401 instead of crashing.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = false,
                SignatureValidator = (token, _) => new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token)
            };
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.IngestWrite, policy =>
        policy.RequireAssertion(ctx =>
            HasRole(ctx.User, AuthPolicies.IngestWriteRole)
            || HasScope(ctx.User, AuthPolicies.AccessAsApplicationScope)));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "History Map API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Entra ID JWT. Acquire via client_credentials, then: Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(document =>
    {
        var requirement = new OpenApiSecurityRequirement();
        var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document);
        requirement.Add(schemeRef, new List<string>());
        return requirement;
    });
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

// Enabled in all environments so Azure Production serves /swagger.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool HasRole(ClaimsPrincipal user, string role) =>
    user.IsInRole(role)
    || user.HasClaim("roles", role)
    || user.HasClaim(ClaimTypes.Role, role);

static bool HasScope(ClaimsPrincipal user, string scope)
{
    var scp = user.FindFirst("scp")?.Value
              ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
    if (string.IsNullOrWhiteSpace(scp))
        return user.HasClaim("roles", scope);

    return scp.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Contains(scope, StringComparer.Ordinal);
}

// Expose for WebApplicationFactory integration tests.
public partial class Program;
