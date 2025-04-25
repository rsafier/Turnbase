using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Turnbase.Rules;
using Turnbase.Server;
using Turnbase.Server.Data;
using Turnbase.Server.GameLogic;
using Turnbase.Server.Models;
using Turnbase.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/fairriff.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddDbContext<GameContext>();
builder.Services.AddSingleton<IGameStateLogic, ScrabbleStateLogic>();
builder.Services.AddSingleton<FairnessService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
 app.UseSwagger();
 app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map SignalR hub
app.MapHub<GameHub>("/gamehub");

app.MapPost("/api/games", async (GameContext db) =>
{
    var newGame = new GameState { StateJson = "{}" , CreatedDate = DateTime.UtcNow };
    db.GameStates.Add(newGame);
    await db.SaveChangesAsync();
    return Results.Ok(new { newGame.Id });
});

app.MapGet("/api/games/{id}", async (int id, GameContext db) =>
{
    var game = await db.GameStates.FindAsync(id);
    if (game == null) return Results.NotFound();
    return Results.Ok(game);
});

// Ensure database and migrations are applied at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameContext>();
    db.Database.Migrate();
}

app.Run();