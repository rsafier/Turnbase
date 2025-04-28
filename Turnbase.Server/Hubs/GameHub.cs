using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Turnbase.Server.GameLogic;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Turnbase.Server.Hubs
{
    [Authorize]
    public class GameHub : Hub
    {
        private readonly IGameEventDispatcher _eventDispatcher;
        private static readonly ConcurrentDictionary<string, IGameInstance> _gameInstances = new ConcurrentDictionary<string, IGameInstance>();

        public GameHub(IGameEventDispatcher eventDispatcher)
        {
            _eventDispatcher = eventDispatcher;
        }

        public async Task JoinRoom(string roomId, string gameType)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            _eventDispatcher.RoomId = roomId;

            // Add player to connected players
            _eventDispatcher.ConnectedPlayers.TryAdd(userId, Context.ConnectionId);

            // Create or get game instance based on game type and room
            IGameInstance gameInstance = _gameInstances.GetOrAdd(roomId, key =>
            {
                if (gameType == "Battleship")
                    return new BattleshipGame(_eventDispatcher);
                else
                    return new CoinFlipGame(_eventDispatcher);
            });

            gameInstance.RoomId = roomId;
            gameInstance.EventDispatcher.RoomId = roomId;

            // Send PlayerJoined event to all clients in the group, including the caller
            await Clients.Group(roomId).SendAsync("PlayerJoined", userId);
            
            // Also send the list of existing players to the new player
            var existingPlayers = _eventDispatcher.ConnectedPlayers.Keys.ToList();
            foreach (var player in existingPlayers)
            {
                if (player != userId)
                {
                    await Clients.User(userId).SendAsync("PlayerJoined", player);
                }
            }
        }

        public async Task LeaveRoom(string roomId)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            _eventDispatcher.ConnectedPlayers.TryRemove(userId, out _);
            await Clients.Group(roomId).SendAsync("PlayerLeft", userId);

            // If no players left in room, remove game instance
            if (_eventDispatcher.ConnectedPlayers.IsEmpty)
            {
                _gameInstances.TryRemove(roomId, out _);
            }
        }

        public async Task SubmitMove(string roomId, string moveJson)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            if (_gameInstances.TryGetValue(roomId, out var gameInstance))
            {
                await gameInstance.ProcessPlayerEventAsync(userId, moveJson);
            }
            else
            {
                await Clients.User(userId).SendAsync("GameEvent", JsonConvert.SerializeObject(
                    new { EventType = "Error", Message = "Game instance not found for this room" }));
            }
        }

        public async Task StartGame(string roomId)
        {
            if (_gameInstances.TryGetValue(roomId, out var gameInstance))
            {
                await gameInstance.StartAsync();
            }
        }

        public async Task CreateRoom(string gameType)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            var roomId = Guid.NewGuid().ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            _eventDispatcher.RoomId = roomId;

            // Add player to connected players
            _eventDispatcher.ConnectedPlayers.TryAdd(userId, Context.ConnectionId);

            // Create game instance based on game type
            IGameInstance gameInstance = _gameInstances.GetOrAdd(roomId, key =>
            {
                if (gameType == "Battleship")
                    return new BattleshipGame(_eventDispatcher);
                else
                    return new CoinFlipGame(_eventDispatcher);
            });

            gameInstance.RoomId = roomId;
            gameInstance.EventDispatcher.RoomId = roomId;

            await Clients.Caller.SendAsync("RoomCreated", roomId);
            await Clients.Group(roomId).SendAsync("PlayerJoined", userId);
        }

        public async Task ListRooms()
        {
            var rooms = _gameInstances.Keys.ToList();
            await Clients.Caller.SendAsync("RoomList", rooms);
        }
    }
}
