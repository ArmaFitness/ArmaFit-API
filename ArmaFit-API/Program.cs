using System.Text.Json;
using System.Text.Json.Serialization;
using ArmaFit_API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql
        .MapEnum<UserRole>("user_role")
        .MapEnum<BiologicalSex>("biological_sex")
        .MapEnum<ActivityLevel>("activity_level")
        .MapEnum<InvitationStatus>("invitation_status"))
    .UseSnakeCaseNamingConvention());

// Enums as snake_case strings (same labels as in the database), numbers as plain JSON numbers.
void ConfigureJson(JsonSerializerOptions json)
{
    json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
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

app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "ArmaFit API"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
