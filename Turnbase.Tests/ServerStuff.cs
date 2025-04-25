using Microsoft.AspNetCore.SignalR;
using Turnbase.Server.Data;
using Turnbase.Server.GameLogic;

namespace Turnbase.Server
{
    public class GameHub(GameContext db, IGameStateLogic gameStateLogic) : Hub
    {
        public async Task JoinGame(int gameId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
        }

        public async Task MakeMove(int gameId, string playerId, string moveJson)
        {
            var gameState = await db.GameStates.FindAsync(gameId);
            if (gameState == null) return;

            if (!gameStateLogic.ValidateMove(gameState.StateJson, moveJson, out var error))
            {
                await Clients.Caller.SendAsync("MoveRejected", error);
                return;
            }

            gameState.StateJson = gameStateLogic.ApplyMove(gameState.StateJson, moveJson, out var error2);
            await db.SaveChangesAsync();

            await Clients.Group($"game-{gameId}").SendAsync("GameStateUpdated", gameState.StateJson);
        }
    }
}