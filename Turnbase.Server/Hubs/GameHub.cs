using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Turnbase.Server.GameLogic;

namespace Turnbase.Server.Hubs
{
    public class GameHub : Hub
    {
        private readonly IGameInstance _gameInstance;

        public GameHub(IGameInstance gameInstance)
        {
            _gameInstance = gameInstance;
        }

        public async Task JoinRoom(string roomId, string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            _gameInstance.RoomId = roomId;
            _gameInstance.EventDispatcher.RoomId = roomId;
            
            // Add player to connected players
            _gameInstance.EventDispatcher.ConnectedPlayers.TryAdd(userId, string.Empty);
            
            await Clients.Group(roomId).SendAsync("PlayerJoined", userId);
        }

        public async Task LeaveRoom(string roomId, string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            _gameInstance.EventDispatcher.ConnectedPlayers.TryRemove(userId, out _);
            await Clients.Group(roomId).SendAsync("PlayerLeft", userId);
        }

        public async Task SubmitMove(string roomId, string userId, string moveJson)
        {
            await _gameInstance.ProcessPlayerEventAsync(userId, moveJson);
        }
    }
}
