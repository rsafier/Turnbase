using Microsoft.AspNetCore.SignalR;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;
using Turnbase.Server.Hubs;
using Turnbase.Server.GameLogic;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading;

namespace Turnbase.Tests
{
    [TestFixture]
    public class GameHubTests
    {
        private Mock<IHubCallerClients> _mockClients;
        private Mock<IHubContext<GameHub>> _mockHubContext;
        private Mock<IGroupManager> _mockGroups;
        private Mock<IGameEventDispatcher> _mockEventDispatcher;
        private GameHub _gameHub;
        private Mock<HubCallerContext> _mockContext;

        [SetUp]
        public void Setup()
        {
            _mockClients = new Mock<IHubCallerClients>();
            _mockGroups = new Mock<IGroupManager>();
            _mockHubContext = new Mock<IHubContext<GameHub>>();
            _mockEventDispatcher = new Mock<IGameEventDispatcher>();
            _mockContext = new Mock<HubCallerContext>();

            // Setup mock for ConnectedPlayers
            var connectedPlayers = new ConcurrentDictionary<string, string>();
            _mockEventDispatcher.Setup(d => d.ConnectedPlayers).Returns(connectedPlayers);
            // Use a local variable to store RoomId for testing purposes
            string currentRoomId = string.Empty;
            _mockEventDispatcher.SetupSet(d => d.RoomId = It.IsAny<string>()).Callback<string>(r => currentRoomId = r);

            // Setup mock context with a user identity
            var claims = new[] { new Claim(ClaimTypes.Name, "TestUser1") };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _mockContext.Setup(c => c.User).Returns(principal);
            _mockContext.Setup(c => c.ConnectionId).Returns("Connection1");

            // Setup mock clients and groups - Avoid unnecessary casting
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockHubContext.Setup(h => h.Groups).Returns(_mockGroups.Object);

            _gameHub = new GameHub(_mockEventDispatcher.Object)
            {
                Context = _mockContext.Object,
                Clients = _mockClients.Object,
                Groups = _mockGroups.Object
            };
        }

        [Test]
        public async Task JoinRoom_AddsUserToGroupAndBroadcastsPlayerJoined()
        {
            // Arrange
            string roomId = "TestRoom1";
            string gameType = "CoinFlip";
            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(c => c.Group(roomId)).Returns(mockGroupClients.Object);

            // Act
            await _gameHub.JoinRoom(roomId, gameType);

            // Assert
            _mockGroups.Verify(g => g.AddToGroupAsync("Connection1", roomId, CancellationToken.None), Times.Once);
            _mockEventDispatcher.VerifySet(d => d.RoomId = roomId, Times.Once);
            _mockEventDispatcher.Verify(d => d.ConnectedPlayers.TryAdd("TestUser1", "Connection1"), Times.Once);
            mockGroupClients.Verify(c => c.SendCoreAsync("PlayerJoined", new object[] { "TestUser1" }, CancellationToken.None), Times.Once);
        }
    }
}
