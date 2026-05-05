using Microsoft.EntityFrameworkCore;
using RogueNet.Api.Contracts;
using RogueNet.Domain.Entities;
using RogueNet.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();

// Add DbContext
if (!builder.Services.Any(x => x.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure()));
}

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG_LOG] Request Error: {ex}");
        throw;
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Player endpoints
app.MapPost("/players", async (CreatePlayerRequest request, ApplicationDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Username))
    {
        return Results.BadRequest(new { Error = "Username is required" });
    }

    var player = new Player
    {
        Id = Guid.NewGuid(),
        Username = request.Username,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    var profile = new PlayerProfile
    {
        Id = Guid.NewGuid(),
        PlayerId = player.Id,
        ExperiencePoints = 0,
        Level = 1,
        CashBalance = 1000m, // Starting cash
        Reputation = 0,
        Version = 1,
        UpdatedAt = DateTime.UtcNow
    };

    db.Players.Add(player);
    db.PlayerProfiles.Add(profile);

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        return Results.Conflict(new { Error = "Username already exists" });
    }

    return Results.Created(
        $"/players/{player.Id}/profile",
        new PlayerResponse(player.Id, player.Username, player.CreatedAt));
})
.WithName("CreatePlayer");

app.MapGet("/players/{playerId:guid}/profile", async (Guid playerId, ApplicationDbContext db) =>
{
    var player = await db.Players
        .Include(p => p.Profile)
        .FirstOrDefaultAsync(p => p.Id == playerId);

    if (player?.Profile is null)
    {
        return Results.NotFound(new { Error = "Player not found" });
    }

    return Results.Ok(new PlayerProfileResponse(
        player.Id,
        player.Username,
        player.Profile.ExperiencePoints,
        player.Profile.Level,
        player.Profile.CashBalance,
        player.Profile.Reputation,
        player.Profile.Version,
        player.Profile.UpdatedAt));
})
.WithName("GetPlayerProfile");

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
