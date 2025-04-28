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
    [Category("GameEventDispatcher")]
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
            _mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(_mockClientProxy.Object);

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

            // Setup the mock to handle the SendCoreAsync call
            _mockClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            bool result = await _dispatcher.SendToUserAsync(userId, eventJson);

            // Assert
            Assert.IsTrue(result); // Current implementation returns true when message is sent successfully
            _mockClients.Verify(c => c.User(userId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("GameEvent", It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == eventJson), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SendToUserAsync_UserNotFound_ReturnsTrue()
        {
            // Arrange
            string userId = "NonExistentUser";
            string eventJson = "{\"EventType\": \"TestEvent\"}";

            // Setup the mock to handle the SendCoreAsync call
            _mockClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            bool result = await _dispatcher.SendToUserAsync(userId, eventJson);

            // Assert
            Assert.IsTrue(result); // Current implementation returns true even if user not found in connected players but user exists
            _mockClients.Verify(c => c.User(userId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("GameEvent", It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == eventJson), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SaveGameStateAsync_ReturnsFalse()
        {
            // Arrange
            string stateJson = "{\"State\": \"TestState\"}";

            // Act
            bool result = await _dispatcher.SaveGameStateAsync(stateJson);

            // Assert
            Assert.IsFalse(result); // Current implementation returns false as it's not fully implemented
            // Note: In a real implementation, you would verify database operations or other persistence mechanisms.
        }

        [Test]
        public async Task LoadGameStateAsync_ReturnsFalse()
        {
            // Arrange
            string stateJson = "{\"State\": \"TestState\"}";

            // Act
            bool result = await _dispatcher.LoadGameStateAsync(stateJson);

            // Assert
            Assert.IsFalse(result); // Current implementation returns false as it's not fully implemented
            // Note: In a real implementation, you would verify database operations or other retrieval mechanisms.
        }

        [Test]
        public async Task BroadcastAsync_ExceptionThrown_ReturnsFalse()
        {
            // Arrange
            string roomId = "TestRoom";
            string eventJson = "{\"EventType\": \"TestEvent\"}";
            _dispatcher.RoomId = roomId;

            // Setup the mock to throw an exception when SendAsync is called
            _mockClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated broadcast error"));

            // Act
            bool result = await _dispatcher.BroadcastAsync(eventJson);

            // Assert
            Assert.IsFalse(result);
            _mockClients.Verify(c => c.Group(roomId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("GameEvent", It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == eventJson), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
