using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArmaFit_API.Controllers;
using ArmaFit_API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql
        .MapEnum<UserRole>("user_role")
        .MapEnum<BiologicalSex>("biological_sex")
        .MapEnum<ActivityLevel>("activity_level")
        .MapEnum<InvitationStatus>("invitation_status"))
    .UseSnakeCaseNamingConvention());

// Access tokens are JWTs signed with Jwt:Key (HS256 needs at least 32 bytes). Set Jwt__Key outside development.
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
    builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.")));
if (jwtKey.KeySize < 256) throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");
builder.Services.AddSingleton(new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false; // keep the short claim names from the token: sub, role, sid
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = AuthController.Issuer,
        ValidAudience = AuthController.Issuer,
        IssuerSigningKey = jwtKey,
        NameClaimType = "sub",
        RoleClaimType = "role",
    };
    o.Events = new JwtBearerEvents
    {
        // Logging out revokes the session, which makes its access token invalid at once instead of when it expires.
        // ponytail: one primary-key lookup per request, cache it if it ever shows up in profiling.
        OnTokenValidated = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!Guid.TryParse(context.Principal!.FindFirstValue("sid"), out var sessionId) ||
                !await db.Sessions.AnyAsync(s => s.Id == sessionId && s.RevokedAt == null && s.User!.IsActive))
                context.Fail("The session is no longer valid.");
        },
    };
});

// Every endpoint requires a logged-in user unless it is marked [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Enums as snake_case strings (same labels as in the database), numbers as plain JSON numbers.
void ConfigureJson(JsonSerializerOptions json)
{
    json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    json.NumberHandling = JsonNumberHandling.Strict;
}
builder.Services.AddControllers().AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions));
// The OpenAPI generator reads these options instead of the MVC ones above.
builder.Services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));

// Every error response is an RFC 7807 problem details body (application/problem+json).
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddOperationTransformer((operation, _, _) =>
{
    // Document success bodies as JSON and error bodies as RFC 7807 problem details.
    foreach (var (status, response) in operation.Responses ?? new())
    {
        if (response.Content is not { Count: > 0 } content) continue;
        var media = content.Values.First();
        content.Clear();
        content[status.StartsWith('2') ? "application/json" : "application/problem+json"] = media;
    }
    return Task.CompletedTask;
}).AddDocumentTransformer((document, _, _) =>
{
    // Bearer scheme, so Swagger UI gets an "Authorize" button and sends the access token.
    document.Components ??= new();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] =
        new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" };
    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));

var app = builder.Build();

// Creates the database, tables and enum types on first run, then fills them with sample data.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedData.Seed(db);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi().AllowAnonymous();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "ArmaFit API"));

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
