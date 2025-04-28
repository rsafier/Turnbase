using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Turnbase.Server.Data;
using Turnbase.Server.Models;
using Turnbase.Server.Hubs;

namespace Turnbase.Server.GameLogic
{
    public class GameEventDispatcher : IGameEventDispatcher
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly GameContext _dbContext;

        public string RoomId { get; set; } = string.Empty;
        public ConcurrentDictionary<string, string> ConnectedPlayers { get; set; } = new ConcurrentDictionary<string, string>();

        public GameEventDispatcher(IHubContext<GameHub> hubContext, GameContext dbContext)
        {
            _hubContext = hubContext;
            _dbContext = dbContext;
        }

        public async Task<bool> BroadcastAsync(string eventJson)
        {
            try
            {
                Console.WriteLine($"Broadcasting event to group {RoomId}: {eventJson}");
                await _hubContext.Clients.Group(RoomId).SendAsync("GameEvent", eventJson);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting event to group {RoomId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendToUserAsync(string userId, string eventJson)
        {
            try
            {
                Console.WriteLine($"Sending event to user {userId}: {eventJson}");
                await _hubContext.Clients.User(userId).SendAsync("GameEvent", eventJson);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending event to user {userId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SaveGameStateAsync(string stateJson)
        {
            try
            {
                var gameId = int.Parse(RoomId);
                var game = await _dbContext.Games.FindAsync(gameId);
                if (game == null)
                {
                    game = new Game 
                    { 
                        Id = gameId,
                        Name = "Game-" + RoomId,
                        CreatedDate = DateTime.UtcNow
                    };
                    _dbContext.Games.Add(game);
                }

                var gameState = new GameState
                {
                    GameId = game.Id,
                    StateJson = stateJson,
                    CreatedDate = DateTime.UtcNow
                };

                _dbContext.GameStates.Add(gameState);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> LoadGameStateAsync(string stateJson)
        {
            // This method might need to be adjusted based on how state loading is implemented
            try
            {
                var gameId = int.Parse(RoomId);
                var latestState = await _dbContext.GameStates
                    .Where(gs => gs.GameId == gameId)
                    .OrderByDescending(gs => gs.CreatedDate)
                    .FirstOrDefaultAsync();

                if (latestState != null)
                {
                    stateJson = latestState.StateJson;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
