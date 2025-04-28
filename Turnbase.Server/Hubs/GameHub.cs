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
        private readonly ILogger<GameHub> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private static readonly ConcurrentDictionary<string, IGameInstance> _gameInstances = new ConcurrentDictionary<string, IGameInstance>();

        public GameHub(IGameEventDispatcher eventDispatcher, ILogger<GameHub> logger, ILoggerFactory loggerFactory)
        {
            _eventDispatcher = eventDispatcher;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        public async Task JoinRoom(string roomId, string gameType)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                _eventDispatcher.RoomId = roomId;

                // Add player to connected players
                _eventDispatcher.ConnectedPlayers.TryAdd(userId, Context.ConnectionId);

                // Create or get game instance based on game type and room
                IGameInstance gameInstance = _gameInstances.GetOrAdd(roomId, key =>
                {
                    if (gameType == "Battleship")
                        return new BattleshipGame(_eventDispatcher, _loggerFactory.CreateLogger<BaseGameInstance>());
                    else
                        return new CoinFlipGame(_eventDispatcher, _loggerFactory.CreateLogger<BaseGameInstance>());
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
                        await Clients.Client(Context.ConnectionId).SendAsync("PlayerJoined", player);
                    }
                }

                _logger.LogInformation("User {UserId} joined room {RoomId} with game type {GameType}", userId, roomId, gameType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while user {UserId} tried to join room {RoomId}", userId, roomId);
                throw;
            }
        }

        public async Task LeaveRoom(string roomId)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
                _eventDispatcher.ConnectedPlayers.TryRemove(userId, out _);
                await Clients.Group(roomId).SendAsync("PlayerLeft", userId);

                // If no players left in room, remove game instance
                if (_eventDispatcher.ConnectedPlayers.IsEmpty)
                {
                    _gameInstances.TryRemove(roomId, out _);
                }

                _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while user {UserId} tried to leave room {RoomId}", userId, roomId);
                throw;
            }
        }

        public async Task SubmitMove(string roomId, string moveJson)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            try
            {
                if (_gameInstances.TryGetValue(roomId, out var gameInstance))
                {
                    await gameInstance.ProcessPlayerEventAsync(userId, moveJson);
                    _logger.LogInformation("User {UserId} submitted move in room {RoomId}", userId, roomId);
                }
                else
                {
                    await Clients.User(userId).SendAsync("GameEvent", JsonConvert.SerializeObject(
                        new { EventType = "Error", Message = "Game instance not found for this room" }));
                    _logger.LogWarning("Game instance not found for room {RoomId} when user {UserId} submitted move", roomId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing move for user {UserId} in room {RoomId}", userId, roomId);
                throw;
            }
        }

        public async Task StartGame(string roomId)
        {
            try
            {
                if (_gameInstances.TryGetValue(roomId, out var gameInstance))
                {
                    await gameInstance.StartAsync();
                    _logger.LogInformation("Game started in room {RoomId}", roomId);
                }
                else
                {
                    _logger.LogWarning("Game instance not found for room {RoomId} when attempting to start game", roomId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while starting game in room {RoomId}", roomId);
                throw;
            }
        }

        public async Task CreateRoom(string gameType)
        {
            var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
            try
            {
                var roomId = Guid.NewGuid().ToString();
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                _eventDispatcher.RoomId = roomId;

                // Add player to connected players
                _eventDispatcher.ConnectedPlayers.TryAdd(userId, Context.ConnectionId);

                // Create game instance based on game type
                IGameInstance gameInstance = _gameInstances.GetOrAdd(roomId, key =>
                {
                    if (gameType == "Battleship")
                        return new BattleshipGame(_eventDispatcher, _loggerFactory.CreateLogger<BaseGameInstance>());
                    else
                        return new CoinFlipGame(_eventDispatcher, _loggerFactory.CreateLogger<BaseGameInstance>());
                });

                gameInstance.RoomId = roomId;
                gameInstance.EventDispatcher.RoomId = roomId;

                await Clients.Caller.SendAsync("RoomCreated", roomId);
                await Clients.Group(roomId).SendAsync("PlayerJoined", userId);

                _logger.LogInformation("User {UserId} created room {RoomId} with game type {GameType}", userId, roomId, gameType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while user {UserId} tried to create room with game type {GameType}", userId, gameType);
                throw;
            }
        }

        public async Task ListRooms()
        {
            try
            {
                var rooms = _gameInstances.Keys.ToList();
                await Clients.Caller.SendAsync("RoomList", rooms);
                _logger.LogInformation("User requested list of rooms. Total rooms: {RoomCount}", rooms.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while listing rooms for user");
                throw;
            }
        }
    }
}
