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
using Turnbase.Server;
using Turnbase.Server.Data;
using Turnbase.Rules;
using Turnbase.Server.Services;
using Turnbase.Server.GameLogic;

namespace Turnbase.Tests.IntegrationTests
{
    [TestFixture]
    public class ServerIntegrationTest
    {
        private TestServer _server;
        private HttpClient _client;
        private IHost _host;

        [SetUp]
        public async Task Setup()
        {
            // Create a test host builder
            var builder = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer()
                           .UseUrls("http://localhost:5000")
                           .ConfigureServices((context, services) =>
                           {
                               // Configure in-memory database for testing
                               services.AddDbContext<GameContext>(options =>
                                   options.UseSqlite("DataSource=:memory:"));
                               
                               // Add other necessary services from Program.cs
                               services.AddSignalR();
                               services.AddSingleton<IGameStateLogic, ScrabbleStateLogic>();
                               services.AddSingleton<FairnessService>();
                           })
                           .Configure(app =>
                           {
                               // Configure the app as in Program.cs
                               app.UseRouting();
                               app.UseEndpoints(endpoints =>
                               {
                                   endpoints.MapHub<GameHub>("/gamehub");
                                   endpoints.MapPost("/api/games", async (GameContext db) =>
                                   {
                                       var newGame = new GameState { StateJson = "{}", CreatedDate = DateTime.UtcNow };
                                       db.GameStates.Add(newGame);
                                       await db.SaveChangesAsync();
                                       return Results.Ok(new { Id = newGame.Id });
                                   });
                               });
                           });
                });

            _host = await builder.StartAsync();
            _server = _host.GetTestServer();
            _client = _host.GetTestClient();
        }

        [TearDown]
        public async Task TearDown()
        {
            _client.Dispose();
            _server.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }

        [Test]
        public async Task CanConnectToSignalRHub()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/gamehub", options => 
                {
                    options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                })
                .Build();

            // Act
            await connection.StartAsync();

            // Assert
            Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));

            // Clean up
            await connection.StopAsync();
        }

        [Test]
        public async Task CanCreateGameViaApi()
        {
            // Act
            var response = await _client.PostAsync("/api/games", null);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Empty);
            Assert.That(content, Does.Contain("Id"));
        }
    }
}
