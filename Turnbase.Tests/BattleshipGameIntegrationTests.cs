using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turnbase.Server.Data;
using Turnbase.Server.GameLogic;
using Turnbase.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace Turnbase.Tests
{
    [TestFixture]
    public class BattleshipGameIntegrationTests
    {
        private IHost _host;
        private HubConnection _player1Connection;
        private HubConnection _player2Connection;
        private string _roomIdBase = "BattleshipRoom_"; // Base for generating unique room IDs
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

            // Start connections and set player IDs
            await _player1Connection.StartAsync();
            await _player2Connection.StartAsync();
            
            // Log connection IDs for debugging
            Console.WriteLine($"Player 1 Connection ID: {_player1Connection.ConnectionId}");
            Console.WriteLine($"Player 2 Connection ID: {_player2Connection.ConnectionId}");
            
            // Set player IDs - use deterministic IDs for testing
            _player1Id = "TestConnection_Player1";
            _player2Id = "TestConnection_Player2"; // Fixed to use different IDs for players
            Console.WriteLine($"Expected Player 1 ID: {_player1Id}");
            Console.WriteLine($"Expected Player 2 ID: {_player2Id}");
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
            var player1JoinedIds = new List<string>();
            var player2JoinedIds = new List<string>();

            _player1Connection.On<string>("PlayerJoined", (userId) => 
            {
                Console.WriteLine($"Player 1 received PlayerJoined for {userId}");
                player1JoinedIds.Add(userId);
                if (player1JoinedIds.Count == 2)
                    player1JoinedTask.SetResult(player1JoinedIds);
            });
            _player2Connection.On<string>("PlayerJoined", (userId) => 
            {
                Console.WriteLine($"Player 2 received PlayerJoined for {userId}");
                player2JoinedIds.Add(userId);
                if (player2JoinedIds.Count == 2)
                    player2JoinedTask.SetResult(player2JoinedIds);
            });

            // Act
            Console.WriteLine("Player 1 joining room...");
            await _player1Connection.InvokeAsync("JoinRoom", roomId, "Battleship");
            Console.WriteLine("Player 2 joining room...");
            await _player2Connection.InvokeAsync("JoinRoom", roomId, "Battleship");

            // Assert
            try
            {
                var player1Results = await player1JoinedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(20));
                var player2Results = await player2JoinedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(20));
                Assert.That(player1Results, Contains.Item(_player1Id), "Player 1 did not receive join event for itself.");
                Assert.That(player1Results, Contains.Item(_player2Id), "Player 1 did not receive join event for Player 2.");
                Assert.That(player2Results, Contains.Item(_player1Id), "Player 2 did not receive join event for Player 1.");
                Assert.That(player2Results, Contains.Item(_player2Id), "Player 2 did not receive join event for itself.");
                Console.WriteLine("JoinRoom test passed.");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Timeout occurred in JoinRoom test: {ex.Message}");
                Console.WriteLine($"Player 1 received events for: {string.Join(", ", player1JoinedIds)}");
                Console.WriteLine($"Player 2 received events for: {string.Join(", ", player2JoinedIds)}");
                Assert.Fail($"Timeout: {ex.Message}");
            }
        }

        [Test]
        [NonParallelizable]
        public async Task StartGame_PlayersJoinRoom_GameStartsSuccessfully()
        {
            // Arrange
            var roomId = _roomIdBase + Guid.NewGuid().ToString();
            var gameStartedTaskP1 = new TaskCompletionSource<string>();
            var gameStartedTaskP2 = new TaskCompletionSource<string>();
            var debugMessages = new List<string>();

            _player1Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player1 Received (GameEvent): {message}");
                debugMessages.Add(message);
                if (message.Contains("GameStarted"))
                    gameStartedTaskP1.TrySetResult(message);
            });
            
            _player2Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player2 Received (GameEvent): {message}");
                debugMessages.Add(message);
                if (message.Contains("GameStarted"))
                    gameStartedTaskP2.TrySetResult(message);
            });

            await _player1Connection.InvokeAsync("JoinRoom", roomId, "Battleship");
            await _player2Connection.InvokeAsync("JoinRoom", roomId, "Battleship");

            // Act
            await _player1Connection.InvokeAsync("StartGame", roomId);

            // Assert
            try
            {
                var gameStartedResultP1 = await gameStartedTaskP1.Task.TimeoutAfter(TimeSpan.FromSeconds(10));
                var gameStartedResultP2 = await gameStartedTaskP2.Task.TimeoutAfter(TimeSpan.FromSeconds(10));
                Assert.IsTrue(gameStartedResultP1.Contains("GameStarted"), "GameStarted event not received by Player 1.");
                Assert.IsTrue(gameStartedResultP2.Contains("GameStarted"), "GameStarted event not received by Player 2.");
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

        [Test]
        [NonParallelizable]
        public async Task PlaceShip_PlayersPlaceShips_ReceivesShipPlacedEvent()
        {
            // Arrange
            var roomId = _roomIdBase + Guid.NewGuid().ToString();
            var gameStartedTask = new TaskCompletionSource<string>();
            var shipPlacedTask = new TaskCompletionSource<string>();
            var debugMessages = new List<string>();

            _player1Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player1 Received (GameEvent): {message}");
                debugMessages.Add(message);
                if (message.Contains("GameStarted"))
                    gameStartedTask.TrySetResult(message);
                if (message.Contains("ShipPlaced"))
                    shipPlacedTask.TrySetResult(message);
            });
            
            _player2Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player2 Received (GameEvent): {message}");
                debugMessages.Add(message);
            });

            await _player1Connection.InvokeAsync("JoinRoom", roomId, "Battleship");
            await _player2Connection.InvokeAsync("JoinRoom", roomId, "Battleship");

            // Start the game
            await _player1Connection.InvokeAsync("StartGame", roomId);

            // Wait for game start event
            await gameStartedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(10));

            // Act
            var moveJson = JsonConvert.SerializeObject(new 
            { 
                Action = "PlaceShip", 
                ShipType = "Carrier", 
                StartX = 0, 
                StartY = 0, 
                IsHorizontal = true 
            });
            await _player1Connection.InvokeAsync("SubmitMove", roomId, moveJson);

            // Assert
            try
            {
                var shipPlacedResult = await shipPlacedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(10));
                Assert.IsTrue(shipPlacedResult.Contains("ShipPlaced"), "ShipPlaced event not received.");
                Assert.IsTrue(shipPlacedResult.Contains("Carrier"), "Placed ship type not correct.");
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

        [Test]
        [NonParallelizable]
        public async Task Attack_ValidAttack_ReceivesAttackResultEvent()
        {
            // Arrange
            var roomId = _roomIdBase + Guid.NewGuid().ToString();
            var gameStartedTask = new TaskCompletionSource<string>();
            var shipPlacedTaskP1 = new TaskCompletionSource<string>();
            var shipPlacedTaskP2 = new TaskCompletionSource<string>();
            var attackResultTaskP1 = new TaskCompletionSource<string>();
            var attackResultTaskP2 = new TaskCompletionSource<string>();
            var debugMessages = new List<string>();
            var shipsToPlace = new[] { "Carrier", "Battleship", "Cruiser", "Submarine", "Destroyer" };
            var placedShipsP1 = new ConcurrentBag<string>();
            var placedShipsP2 = new ConcurrentBag<string>();

            _player1Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player1 Received (GameEvent): {message}");
                debugMessages.Add(message);
                if (message.Contains("GameStarted"))
                    gameStartedTask.TrySetResult(message);
                if (message.Contains("ShipPlaced") && message.Contains(_player1Id))
                {
                    var shipType = ExtractShipTypeFromMessage(message);
                    if (shipType != null)
                        placedShipsP1.Add(shipType);
                    shipPlacedTaskP1.TrySetResult(message);
                }
                if (message.Contains("AttackResult"))
                    attackResultTaskP1.TrySetResult(message);
            });
            
            _player2Connection.On<string>("GameEvent", (message) =>
            {
                Console.WriteLine($"Player2 Received (GameEvent): {message}");
                debugMessages.Add(message);
                if (message.Contains("ShipPlaced") && message.Contains(_player2Id))
                {
                    var shipType = ExtractShipTypeFromMessage(message);
                    if (shipType != null)
                        placedShipsP2.Add(shipType);
                    shipPlacedTaskP2.TrySetResult(message);
                }
                if (message.Contains("AttackResult"))
                    attackResultTaskP2.TrySetResult(message);
            });

            await _player1Connection.InvokeAsync("JoinRoom", roomId, "Battleship");
            await _player2Connection.InvokeAsync("JoinRoom", roomId, "Battleship");

            // Start the game
            await _player1Connection.InvokeAsync("StartGame", roomId);

            // Wait for game start event
            await gameStartedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(20));

            // Place all ships for both players
            var attackPhaseTaskP1 = new TaskCompletionSource<string>();
            var attackPhaseTaskP2 = new TaskCompletionSource<string>();

            _player1Connection.On<string>("GameEvent", (message) =>
            {
                if (message.Contains("AttackPhaseStarted"))
                    attackPhaseTaskP1.TrySetResult(message);
            });
            
            _player2Connection.On<string>("GameEvent", (message) =>
            {
                if (message.Contains("AttackPhaseStarted"))
                    attackPhaseTaskP2.TrySetResult(message);
            });
            int yOffset = 0;
            foreach (var ship in shipsToPlace)
            {
                shipPlacedTaskP1 = new TaskCompletionSource<string>(); // Reset for each ship
                var shipJsonP1 = JsonConvert.SerializeObject(new 
                { 
                    Action = "PlaceShip", 
                    ShipType = ship, 
                    StartX = 0, 
                    StartY = yOffset, 
                    IsHorizontal = true 
                });
                Console.WriteLine($"Player 1 placing {ship} at (0, {yOffset})");
                await _player1Connection.InvokeAsync("SubmitMove", roomId, shipJsonP1);
                await shipPlacedTaskP1.Task.TimeoutAfter(TimeSpan.FromSeconds(20));
                yOffset++;
            }

            yOffset = 0;
            foreach (var ship in shipsToPlace)
            {
                shipPlacedTaskP2 = new TaskCompletionSource<string>(); // Reset for each ship
                var shipJsonP2 = JsonConvert.SerializeObject(new 
                { 
                    Action = "PlaceShip", 
                    ShipType = ship, 
                    StartX = 0, 
                    StartY = yOffset, 
                    IsHorizontal = true 
                });
                Console.WriteLine($"Player 2 placing {ship} at (0, {yOffset})");
                await _player2Connection.InvokeAsync("SubmitMove", roomId, shipJsonP2);
                await shipPlacedTaskP2.Task.TimeoutAfter(TimeSpan.FromSeconds(20));
                yOffset++;
            }

            // Act - Player 1 attacks
            var attackJson = JsonConvert.SerializeObject(new 
            { 
                Action = "Attack", 
                X = 0, 
                Y = 0 
            });
            // Wait for attack phase to start before attacking
            await attackPhaseTaskP1.Task.TimeoutAfter(TimeSpan.FromSeconds(40));
            await attackPhaseTaskP2.Task.TimeoutAfter(TimeSpan.FromSeconds(40));
            Console.WriteLine("Player 1 attacking (0, 0)");
            await _player1Connection.InvokeAsync("SubmitMove", roomId, attackJson);

            // Assert
            try
            {
                var attackResultP1 = await attackResultTaskP1.Task.TimeoutAfter(TimeSpan.FromSeconds(40));
                var attackResultP2 = await attackResultTaskP2.Task.TimeoutAfter(TimeSpan.FromSeconds(40));
                Assert.IsTrue(attackResultP1.Contains("AttackResult"), "AttackResult event not received by Player 1.");
                Assert.IsTrue(attackResultP2.Contains("AttackResult"), "AttackResult event not received by Player 2.");
                Assert.IsTrue(attackResultP1.Contains("Hit") || attackResultP1.Contains("Miss"), "Attack result for Player 1 does not contain Hit or Miss status.");
                Assert.IsTrue(attackResultP2.Contains("Hit") || attackResultP2.Contains("Miss"), "Attack result for Player 2 does not contain Hit or Miss status.");
                Console.WriteLine("Attack test passed.");
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

        private string ExtractShipTypeFromMessage(string message)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<dynamic>(message);
                return json.ShipType;
            }
            catch
            {
                return null;
            }
        }
    }
}
