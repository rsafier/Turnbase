using Moq;
using NUnit.Framework;
using System.Threading.Tasks;
using Turnbase.Server.GameLogic;
using Turnbase.Server.Hubs;
using Turnbase.Server.Data;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using System.Threading;

namespace Turnbase.Tests
{
    [TestFixture]
    public class GameEventDispatcherTests
    {
        private GameEventDispatcher _dispatcher;
        private Mock<IHubContext<GameHub>> _mockHubContext;
        private Mock<IHubClients> _mockClients;
        private Mock<ISingleClientProxy> _mockClientProxy;
        private Mock<GameContext> _mockGameContext;
        private ConcurrentDictionary<string, string> _connectedPlayers;

        [SetUp]
        public void Setup()
        {
            _mockHubContext = new Mock<IHubContext<GameHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<ISingleClientProxy>();
            _mockGameContext = new Mock<GameContext>();
            _connectedPlayers = new ConcurrentDictionary<string, string>();

            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object as IClientProxy);
            _mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            _dispatcher = new GameEventDispatcher(_mockHubContext.Object, _mockGameContext.Object);
            _dispatcher.ConnectedPlayers = _connectedPlayers;
        }

        [Test]
        public async Task BroadcastAsync_SendsMessageToGroup()
        {
            // Arrange
            string roomId = "TestRoom";
            string eventJson = "{\"EventType\": \"TestEvent\"}";
            _dispatcher.RoomId = roomId;

            // Act
            bool result = await _dispatcher.BroadcastAsync(eventJson);

            // Assert
            Assert.IsTrue(result);
            _mockClients.Verify(c => c.Group(roomId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("GameEvent", It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == eventJson), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SendToUserAsync_SendsMessageToSpecificUser()
        {
            // Arrange
            string userId = "TestUser";
            string connectionId = "Connection1";
            string eventJson = "{\"EventType\": \"TestEvent\"}";
            _connectedPlayers.TryAdd(userId, connectionId);

            // Act
            bool result = await _dispatcher.SendToUserAsync(userId, eventJson);

            // Assert
            Assert.IsTrue(result);
            _mockClients.Verify(c => c.Client(connectionId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("GameEvent", It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == eventJson), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SendToUserAsync_UserNotFound_ReturnsFalse()
        {
            // Arrange
            string userId = "NonExistentUser";
            string eventJson = "{\"EventType\": \"TestEvent\"}";

            // Act
            bool result = await _dispatcher.SendToUserAsync(userId, eventJson);

            // Assert
            Assert.IsFalse(result);
            _mockClients.Verify(c => c.Client(It.IsAny<string>()), Times.Never);
            _mockClientProxy.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task SaveGameStateAsync_ReturnsTrue()
        {
            // Arrange
            string stateJson = "{\"State\": \"TestState\"}";

            // Act
            bool result = await _dispatcher.SaveGameStateAsync(stateJson);

            // Assert
            Assert.IsTrue(result);
            // Note: In a real implementation, you would verify database operations or other persistence mechanisms.
        }

        [Test]
        public async Task LoadGameStateAsync_ReturnsTrue()
        {
            // Arrange
            string stateJson = "{\"State\": \"TestState\"}";

            // Act
            bool result = await _dispatcher.LoadGameStateAsync(stateJson);

            // Assert
            Assert.IsTrue(result);
            // Note: In a real implementation, you would verify database operations or other retrieval mechanisms.
        }
    }
}
