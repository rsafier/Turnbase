using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Turnbase.Server.Data;
using Turnbase.Server.Models;
using Turnbase.Server.Hubs;
using System.Collections.Generic;
using System.Threading;

namespace Turnbase.Server.GameLogic
{
    public class GameEventDispatcher : IGameEventDispatcher
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly GameContext _dbContext;
        private readonly ConcurrentQueue<string> _broadcastMessageQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _userMessageQueues = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private readonly Timer _batchTimer;
        private const int BatchIntervalMs = 100; // Batch messages every 100ms

        public string RoomId { get; set; } = string.Empty;
        public ConcurrentDictionary<string, string> ConnectedPlayers { get; set; } = new ConcurrentDictionary<string, string>();

        public GameEventDispatcher(IHubContext<GameHub> hubContext, GameContext dbContext)
        {
            _hubContext = hubContext;
            _dbContext = dbContext;
            _batchTimer = new Timer(ProcessMessageBatches, null, BatchIntervalMs, BatchIntervalMs);
        }

        private void ProcessMessageBatches(object state)
        {
            // Process broadcast messages
            if (_broadcastMessageQueue.Count > 0)
            {
                var messagesToSend = new List<string>();
                while (_broadcastMessageQueue.TryDequeue(out var message))
                {
                    messagesToSend.Add(message);
                }

                if (messagesToSend.Count > 0)
                {
                    // Combine messages into a single payload or send as a list
                    string batchedMessage = "[" + string.Join(",", messagesToSend) + "]";
                    try
                    {
                        _hubContext.Clients.Group(RoomId).SendAsync("GameEventBatch", batchedMessage).GetAwaiter().GetResult();
                        Console.WriteLine($"Broadcasting batched events to group {RoomId}: {messagesToSend.Count} messages");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error broadcasting batched events to group {RoomId}: {ex.Message}");
                    }
                }
            }

            // Process user-specific messages
            foreach (var userQueue in _userMessageQueues)
            {
                var userId = userQueue.Key;
                var queue = userQueue.Value;
                
                if (queue.Count > 0)
                {
                    var messagesToSend = new List<string>();
                    while (queue.TryDequeue(out var message))
                    {
                        messagesToSend.Add(message);
                    }

                    if (messagesToSend.Count > 0)
                    {
                        string batchedMessage = "[" + string.Join(",", messagesToSend) + "]";
                        try
                        {
                            _hubContext.Clients.User(userId).SendAsync("GameEventBatch", batchedMessage).GetAwaiter().GetResult();
                            Console.WriteLine($"Sending batched events to user {userId}: {messagesToSend.Count} messages");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error sending batched events to user {userId}: {ex.Message}");
                        }
                    }
                }
            }
        }

        public async Task<bool> BroadcastAsync(string eventJson)
        {
            try
            {
                _broadcastMessageQueue.Enqueue(eventJson);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error queuing broadcast event to group {RoomId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendToUserAsync(string userId, string eventJson)
        {
            try
            {
                var userQueue = _userMessageQueues.GetOrAdd(userId, _ => new ConcurrentQueue<string>());
                userQueue.Enqueue(eventJson);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error queuing event to user {userId}: {ex.Message}");
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
                await _dbContext.SaveChangesAsync(CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving game state for room {RoomId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LoadGameStateAsync(string stateJson)
        {
            try
            {
                var gameId = int.Parse(RoomId);
                var latestState = await _dbContext.GameStates
                    .AsNoTracking()
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading game state for room {RoomId}: {ex.Message}");
                return false;
            }
        }
    }
}
