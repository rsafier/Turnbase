using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Turnbase.Server.Data;
using Turnbase.Server.GameLogic;
using Turnbase.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Turnbase.Tests
{
    [TestFixture]
    public class CoinFlipGameIntegrationTests
    {
        private IHost _host;
        private HubConnection _player1Connection;
        private HubConnection _player2Connection;
        private string _roomIdBase = "Room_"; // Base for generating unique room IDs
        private string _player1Id;
        private string _player2Id;

        [SetUp]
        public async Task Setup()
        {
            // Setup test host with SignalR and in-memory database using TestServer
            var hostBuilder = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices((context, services) =>
                    {
                        services.AddSignalR();
                        services.AddDbContextFactory<GameContext>(options =>
                            options.UseSqlite("Data Source=:memory:"));
                        // Add authentication and authorization services
                        services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = "TestScheme";
                            options.DefaultChallengeScheme = "TestScheme";
                        })
                        .AddTestAuth(o => { }); // Custom test authentication handler
                        
                        services.AddAuthorization();
                        
                        // Register a custom GameEventDispatcher for testing that actually saves to DB
                        services.AddSingleton<IGameEventDispatcher>(sp => 
                        {
                            var dbFactory = sp.GetRequiredService<IDbContextFactory<GameContext>>();
                            var hubContext = sp.GetRequiredService<IHubContext<GameHub>>();
                            return new TestGameEventDispatcher(dbFactory, hubContext);
                        });
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapHub<GameHub>("/gameHub");
                        });
                    });
                });

            _host = await hostBuilder.StartAsync();

            // Open database connection for in-memory SQLite using factory
            var dbContextFactory = _host.Services.GetRequiredService<IDbContextFactory<GameContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            await dbContext.Database.OpenConnectionAsync();
            await dbContext.Database.EnsureCreatedAsync();

            // Setup SignalR client connections for two players
            var testServer = _host.GetTestServer();
            var clientHandler = testServer.CreateHandler();
            _player1Connection = new HubConnectionBuilder()
                .WithUrl(new Uri(testServer.BaseAddress, "gameHub"), options =>
                {
                    options.HttpMessageHandlerFactory = _ => clientHandler;
                })
                .Build();
            _player2Connection = new HubConnectionBuilder()
                .WithUrl(new Uri(testServer.BaseAddress, "gameHub"), options =>
                {
                    options.HttpMessageHandlerFactory = _ => clientHandler;
                })
                .Build();

            // Variables to store the actual player IDs from connection
            var player1IdTask = new TaskCompletionSource<string>();
            var player2IdTask = new TaskCompletionSource<string>();

            // Listen for any initial message to capture the user ID (we'll use PlayerJoined later in tests)
            _player1Connection.On<string>("PlayerJoined", (userId) => player1IdTask.TrySetResult(userId));
            _player2Connection.On<string>("PlayerJoined", (userId) => player2IdTask.TrySetResult(userId));

            // Start connections
            await _player1Connection.StartAsync();
            await _player2Connection.StartAsync();

            // Temporary join to a dummy room to trigger PlayerJoined event and capture IDs
            var tempRoomId = "TempRoom_" + Guid.NewGuid().ToString();
            await _player1Connection.InvokeAsync("JoinRoom", tempRoomId, "CoinFlip");
            await _player2Connection.InvokeAsync("JoinRoom", tempRoomId, "CoinFlip");

            // Wait for and capture the actual player IDs
            _player1Id = await player1IdTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
            _player2Id = await player2IdTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));

            // Leave the temporary room
            await _player1Connection.InvokeAsync("LeaveRoom", tempRoomId);
            await _player2Connection.InvokeAsync("LeaveRoom", tempRoomId);

            Console.WriteLine($"Captured Player IDs - Player1: {_player1Id}, Player2: {_player2Id}");
        }

        [TearDown]
        public async Task TearDown()
        {
            await _player1Connection.StopAsync();
            await _player2Connection.StopAsync();
            await _host.StopAsync();
            var dbContextFactory = _host.Services.GetRequiredService<IDbContextFactory<GameContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.CloseConnectionAsync();
            _host.Dispose();
        }

        [Test]
        [NonParallelizable]
        public async Task JoinRoom_PlayersJoinRoom_ReceivesPlayerJoinedEvent()
        {
            // Arrange
            var roomId = _roomIdBase + Guid.NewGuid().ToString();
            var player1JoinedTask = new TaskCompletionSource<List<string>>();
            var player2JoinedTask = new TaskCompletionSource<List<string>>();
            var player1JoinedPlayers = new List<string>();
            var player2JoinedPlayers = new List<string>();

            _player1Connection.On<string>("PlayerJoined", (userId) => 
            {
                lock (player1JoinedPlayers)
                {
                    player1JoinedPlayers.Add(userId);
                    Console.WriteLine($"Player 1 received PlayerJoined for {userId}. Total: {player1JoinedPlayers.Count}");
                    if (player1JoinedPlayers.Count == 2) 
                    {
                        player1JoinedTask.TrySetResult(new List<string>(player1JoinedPlayers));
                    }
                }
            });
            _player2Connection.On<string>("PlayerJoined", (userId) => 
            {
                lock (player2JoinedPlayers)
                {
                    player2JoinedPlayers.Add(userId);
                    Console.WriteLine($"Player 2 received PlayerJoined for {userId}. Total: {player2JoinedPlayers.Count}");
                    if (player2JoinedPlayers.Count == 2) 
                    {
                        player2JoinedTask.TrySetResult(new List<string>(player2JoinedPlayers));
                    }
                }
            });

            // Act
            Console.WriteLine("Player 1 joining room...");
            await _player1Connection.InvokeAsync("JoinRoom", roomId, "CoinFlip");
            await Task.Delay(500); // Small delay to ensure first player's event is processed
            Console.WriteLine("Player 2 joining room...");
            await _player2Connection.InvokeAsync("JoinRoom", roomId, "CoinFlip");

            // Assert
            try {
                var player1Results = await player1JoinedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(10));
                var player2Results = await player2JoinedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(10));
                Assert.That(player1Results.Count, Is.EqualTo(2), "Player 1 should receive 2 PlayerJoined events");
                Assert.That(player2Results.Count, Is.EqualTo(2), "Player 2 should receive 2 PlayerJoined events");
                Assert.That(player1Results, Contains.Item(_player1Id), "Player 1 should receive event for self");
                Assert.That(player1Results, Contains.Item(_player2Id), "Player 1 should receive event for other player");
                Assert.That(player2Results, Contains.Item(_player1Id), "Player 2 should receive event for other player");
                Assert.That(player2Results, Contains.Item(_player2Id), "Player 2 should receive event for self");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Timeout occurred in JoinRoom test: {ex.Message}");
                Console.WriteLine($"Player 1 received events for: {string.Join(", ", player1JoinedPlayers)}");
                Console.WriteLine($"Player 2 received events for: {string.Join(", ", player2JoinedPlayers)}");
                Assert.Fail($"Timeout occurred: {ex.Message}");
            }
        }

        [NonParallelizable]
        [Test]
        public async Task SubmitMove_PlayerMakesMove_ReceivesGameStartedAndCoinFlipResult()
        {
            // Arrange
            var roomId = _roomIdBase + Guid.NewGuid().ToString();
            var gameStartedTask = new TaskCompletionSource<string>();
            var coinFlipResultTask = new TaskCompletionSource<string>();
            var debugMessages = new List<string>();

            _player1Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player1 Received (GameEvent): {message}");
                debugMessages.Add(message);
                if (message.Contains("GameStarted"))
                    gameStartedTask.TrySetResult(message);
                if (message.Contains("CoinFlipResult"))
                    coinFlipResultTask.TrySetResult(message);
            });
            _player1Connection.On<string>("GameEventBatch", (batchMessage) =>
            {
                Console.WriteLine($"Player1 Received (GameEventBatch): {batchMessage}");
                var messages = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(batchMessage);
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        debugMessages.Add(message);
                        if (message.Contains("GameStarted"))
                            gameStartedTask.TrySetResult(message);
                        if (message.Contains("CoinFlipResult"))
                            coinFlipResultTask.TrySetResult(message);
                    }
                }
            });
            
            _player2Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player2 Received (GameEvent): {message}");
                debugMessages.Add(message);
            });
            _player2Connection.On<string>("GameEventBatch", (batchMessage) =>
            {
                Console.WriteLine($"Player2 Received (GameEventBatch): {batchMessage}");
                var messages = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(batchMessage);
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        debugMessages.Add(message);
                    }
                }
            });

            await _player1Connection.InvokeAsync("JoinRoom", roomId, "CoinFlip");
            await _player2Connection.InvokeAsync("JoinRoom", roomId, "CoinFlip");

            // Explicitly start the game
            await _player1Connection.InvokeAsync("StartGame", roomId);

            // Wait a bit longer to ensure game start event is processed
            await Task.Delay(1000);

            // Act
            var moveJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            await _player1Connection.InvokeAsync("SubmitMove", roomId, moveJson);

            // Assert
            try
            {
                var gameStartedResult = await gameStartedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
                var resultMessage = await coinFlipResultTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
                Assert.IsTrue(gameStartedResult.Contains("GameStarted"), "GameStarted event not received.");
                Assert.IsTrue(resultMessage.Contains("CoinFlipResult"), "CoinFlipResult event not received.");
                Assert.IsTrue(resultMessage.Contains("Winner"), "Winner not included in result.");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine("Timeout occurred. Check logs for received messages:");
                foreach (var msg in debugMessages)
                {
                    Console.WriteLine($"Received: {msg}");
                }
                Assert.Fail($"Timeout: {ex.Message}");
            }
        }

        [NonParallelizable]
        [Test]
        public async Task SubmitMove_GameEnds_StateIsSavedToDatabase()
        {
            // Arrange
            var roomId = _roomIdBase + Guid.NewGuid().ToString();
            var gameEndedTask = new TaskCompletionSource<string>();
            var debugMessages = new List<string>();
            _player1Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player1 Received (GameEvent): {message}");
                debugMessages.Add(message);
                if (message.Contains("GameEnded"))
                    gameEndedTask.TrySetResult(message);
            });
            _player1Connection.On<string>("GameEventBatch", (batchMessage) =>
            {
                Console.WriteLine($"Player1 Received (GameEventBatch): {batchMessage}");
                var messages = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(batchMessage);
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        debugMessages.Add(message);
                        if (message.Contains("GameEnded"))
                            gameEndedTask.TrySetResult(message);
                    }
                }
            });
            
            _player2Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player2 Received (GameEvent): {message}");
                debugMessages.Add(message);
            });
            _player2Connection.On<string>("GameEventBatch", (batchMessage) =>
            {
                Console.WriteLine($"Player2 Received (GameEventBatch): {batchMessage}");
                var messages = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(batchMessage);
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        debugMessages.Add(message);
                    }
                }
            });

            await _player1Connection.InvokeAsync("JoinRoom", roomId, "CoinFlip");
            await _player2Connection.InvokeAsync("JoinRoom", roomId, "CoinFlip");

            // Explicitly start the game
            await _player1Connection.InvokeAsync("StartGame", roomId);

            // Wait a bit longer to ensure game start event is processed
            await Task.Delay(1000);
            await Task.Yield();
            // Act
            var moveJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            await _player1Connection.InvokeAsync("SubmitMove", roomId, moveJson);

            // Wait for game to end with a shorter timeout
            try
            {
                await gameEndedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine("Timeout occurred waiting for GameEnded. Check logs for received messages:");
                foreach (var msg in debugMessages)
                {
                    Console.WriteLine($"Received: {msg}");
                }
                Assert.Fail($"Timeout: {ex.Message}");
            }

            // Check database state immediately
            var dbContextFactory = _host.Services.GetRequiredService<IDbContextFactory<GameContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            var gameState = await dbContext.GameStates.FirstOrDefaultAsync();

            // Assert
            Assert.IsNotNull(gameState, "Game state was not saved to database. Ensure GameEventDispatcher.SaveGameStateAsync is implemented to save state.");
            Assert.IsTrue(gameState.StateJson.Contains("CoinFlip"), "Game state JSON does not contain expected content.");
        }
    }

    // Helper method to add timeout to tasks
    public static class TaskExtensions
    {
        public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
        {
            using (var cts = new System.Threading.CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, cts.Token);
                var completedTask = await Task.WhenAny(task, delayTask);
                if (completedTask == delayTask)
                {
                    throw new TimeoutException("The operation has timed out.");
                }
                cts.Cancel();
                return await task;
            }
        }
    }

    // Custom implementation of IGameEventDispatcher for testing
    public class TestGameEventDispatcher : IGameEventDispatcher
    {
        private readonly IDbContextFactory<GameContext> _dbContextFactory;
        private readonly IHubContext<GameHub> _hubContext;
        private string _lastSavedState = string.Empty;

        private string _roomId = "TestRoom";
        public string RoomId 
        { 
            get => _roomId; 
            set => _roomId = value; 
        }
        public ConcurrentDictionary<string, string> ConnectedPlayers { get; set; } = new ConcurrentDictionary<string, string>();

        public TestGameEventDispatcher(IDbContextFactory<GameContext> dbContextFactory, IHubContext<GameHub> hubContext)
        {
            _dbContextFactory = dbContextFactory;
            _hubContext = hubContext;
        }

        public Task<bool> BroadcastAsync(string eventJson)
        {
            _hubContext.Clients.Group(RoomId).SendAsync("GameEvent", eventJson).GetAwaiter().GetResult();
            Console.WriteLine($"Broadcasting to room {RoomId}: {eventJson}");
            return Task.FromResult(true);
        }

        public Task<bool> SendToUserAsync(string userId, string eventJson)
        {
            _hubContext.Clients.User(userId).SendAsync("GameEvent", eventJson).GetAwaiter().GetResult();
            Console.WriteLine($"Sending to user {userId}: {eventJson}");
            return Task.FromResult(true);
        }

        public async Task<bool> SaveGameStateAsync(string stateJson)
        {
            _lastSavedState = stateJson;
            using var dbContext = _dbContextFactory.CreateDbContext();
            var gameState = new Turnbase.Server.Models.GameState
            {
                GameId = 1, // Hardcoded for simplicity in tests
                StateJson = stateJson,
                CreatedDate = DateTime.UtcNow
            };
            dbContext.GameStates.Add(gameState);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Saved game state for RoomId: {RoomId}");
            return true;
        }

        public Task<bool> LoadGameStateAsync(string stateJson)
        {
            // For testing, just return the last saved state
            return Task.FromResult(true);
        }
    }
}
