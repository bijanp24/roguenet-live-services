using Microsoft.EntityFrameworkCore;
using RogueNet.Api.Contracts;
using RogueNet.Application.MissionCompletions;
using RogueNet.Domain.Entities;
using RogueNet.Infrastructure.Data;
using RogueNet.Infrastructure.Repositories;
using Scalar.AspNetCore;

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

// Mission completion: Dapper repo + Application service. Guarded so integration tests can swap them.
if (builder.Services.All(x => x.ServiceType != typeof(IMissionCompletionRepository)))
{
    builder.Services.AddSingleton<IMissionCompletionRepository>(_ =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
        return new MissionCompletionRepository(connectionString);
    });
}

if (builder.Services.All(x => x.ServiceType != typeof(IMissionCompletionService)))
{
    builder.Services.AddScoped<IMissionCompletionService, MissionCompletionService>();
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
    app.MapScalarApiReference();
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

app.MapPost("/players/{playerId:guid}/mission-completions", async (
    Guid playerId,
    MissionCompletionRequest request,
    IMissionCompletionService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.BadRequest(new { Error = "Request body is required" });
    }

    if (request.CompletionId == Guid.Empty)
    {
        return Results.BadRequest(new { Error = "completionId is required" });
    }

    if (string.IsNullOrWhiteSpace(request.MissionId))
    {
        return Results.BadRequest(new { Error = "missionId is required" });
    }

    if (string.IsNullOrWhiteSpace(request.Difficulty))
    {
        return Results.BadRequest(new { Error = "difficulty is required" });
    }

    if (request.Score < 0)
    {
        return Results.BadRequest(new { Error = "score must be non-negative" });
    }

    if (request.DurationSeconds < 0)
    {
        return Results.BadRequest(new { Error = "durationSeconds must be non-negative" });
    }

    var command = new CompleteMissionCommand(
        playerId,
        request.CompletionId,
        request.MissionId,
        request.Score,
        request.DurationSeconds,
        request.Difficulty);

    var outcome = await service.ExecuteAsync(command, cancellationToken);

    return outcome switch
    {
        CompleteMissionOutcome.Success success when !success.WasReplay =>
            Results.Created(
                $"/players/{playerId}/mission-completions/{success.Result.CompletionId}",
                MissionCompletionResponse.FromResult(success.Result)),
        CompleteMissionOutcome.Success success =>
            Results.Ok(MissionCompletionResponse.FromResult(success.Result)),
        CompleteMissionOutcome.PlayerNotFound =>
            Results.NotFound(new { Error = "Player not found" }),
        CompleteMissionOutcome.IdempotencyConflict =>
            Results.Conflict(new { Error = "completionId was reused with a different request body" }),
        _ => Results.Problem("Unexpected mission completion outcome"),
    };
})
.WithName("CompleteMission");

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
