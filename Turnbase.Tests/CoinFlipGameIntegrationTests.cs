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
using Newtonsoft.Json;

namespace Turnbase.Tests
{
    [TestFixture]
    public class CoinFlipGameIntegrationTests
    {
        private IHost _host;
        private HubConnection _player1Connection;
        private HubConnection _player2Connection;
        private string _roomId = "TestRoom";
        private string _player1Id = "Player1";
        private string _player2Id = "Player2";

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
                        services.AddSingleton<IGameInstance>(sp => new CoinFlipGame(sp.GetRequiredService<IGameEventDispatcher>()));
                        services.AddSingleton<IGameEventDispatcher, GameEventDispatcher>();
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.MapHub<GameHub>("/gameHub");
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

            await _player1Connection.StartAsync();
            await _player2Connection.StartAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _player1Connection.StopAsync();
            await _player2Connection.StopAsync();
            await _host.StopAsync();
            _host.Dispose();
        }

        [Test]
        public async Task JoinRoom_PlayersJoinRoom_ReceivesPlayerJoinedEvent()
        {
            // Arrange
            var player1JoinedTask = new TaskCompletionSource<string>();
            var player2JoinedTask = new TaskCompletionSource<string>();

            _player1Connection.On<string>("PlayerJoined", (userId) => player1JoinedTask.SetResult(userId));
            _player2Connection.On<string>("PlayerJoined", (userId) => player2JoinedTask.SetResult(userId));

            // Act
            await _player1Connection.InvokeAsync("JoinRoom", _roomId, _player1Id);
            await _player2Connection.InvokeAsync("JoinRoom", _roomId, _player2Id);

            // Assert
            var player1Result = await player1JoinedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
            var player2Result = await player2JoinedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
            Assert.That(player1Result, Is.EqualTo(_player1Id));
            Assert.That(player2Result, Is.EqualTo(_player2Id));
        }

        [Test]
        public async Task SubmitMove_PlayerMakesMove_ReceivesGameStartedAndCoinFlipResult()
        {
            // Arrange
            var gameStartedTask = new TaskCompletionSource<string>();
            var coinFlipResultTask = new TaskCompletionSource<string>();

            _player1Connection.On<string>("GameStarted", (message) => gameStartedTask.SetResult(message));
            _player1Connection.On<string>("CoinFlipResult", (message) => coinFlipResultTask.SetResult(message));

            await _player1Connection.InvokeAsync("JoinRoom", _roomId, _player1Id);
            await _player2Connection.InvokeAsync("JoinRoom", _roomId, _player2Id);

            // Act
            var moveJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            await _player1Connection.InvokeAsync("SubmitMove", _roomId, _player1Id, moveJson);

            // Assert
            await gameStartedTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
            var resultMessage = await coinFlipResultTask.Task.TimeoutAfter(TimeSpan.FromSeconds(5));
            Assert.IsTrue(resultMessage.Contains("CoinFlipResult"));
            Assert.IsTrue(resultMessage.Contains("Winner"));
        }

        [Test]
        public async Task SubmitMove_GameEnds_StateIsSavedToDatabase()
        {
            // Arrange
            await _player1Connection.InvokeAsync("JoinRoom", _roomId, _player1Id);
            await _player2Connection.InvokeAsync("JoinRoom", _roomId, _player2Id);

            var moveJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            await _player1Connection.InvokeAsync("SubmitMove", _roomId, _player1Id, moveJson);

            // Act
            var dbContextFactory = _host.Services.GetRequiredService<IDbContextFactory<GameContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            var gameState = await dbContext.GameStates.FirstOrDefaultAsync();

            // Assert
            Assert.IsNotNull(gameState);
            Assert.IsTrue(gameState.StateJson.Contains("CoinFlip"));
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
}
