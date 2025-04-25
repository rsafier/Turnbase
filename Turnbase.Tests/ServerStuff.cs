using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Turnbase.Server.Data;
using Turnbase.Server.Models;
using Turnbase.Server.GameLogic;
using Microsoft.EntityFrameworkCore;

namespace Turnbase.Server
{
    public class GameHub : Hub
    {
        private readonly GameContext _db;
        private readonly IGameRule _gameRule;

        public GameHub(GameContext db, IGameRule gameRule)
        {
            _db = db;
            _gameRule = gameRule;
        }

        public async Task JoinGame(int gameId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
        }

        public async Task MakeMove(int gameId, string playerId, string moveJson)
        {
            var gameState = await _db.GameStates.FindAsync(gameId);
            if (gameState == null) return;

            string? error;
            if (!_gameRule.ValidateMove(gameState.StateJson, moveJson, out error))
            {
                await Clients.Caller.SendAsync("MoveRejected", error);
                return;
            }

            gameState.StateJson = _gameRule.ApplyMove(gameState.StateJson, moveJson);
            await _db.SaveChangesAsync();

            await Clients.Group($"game-{gameId}").SendAsync("GameStateUpdated", gameState.StateJson);
        }
    }
}