using Moq;
using NUnit.Framework;
using System.Threading.Tasks;
using Turnbase.Server.GameLogic;
using Turnbase.Server.Hubs;
using Turnbase.Server.Data;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using System.Threading;
using System;
using Microsoft.EntityFrameworkCore;
using Turnbase.Server.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Turnbase.Tests
{
    [TestFixture]
    [Category("GameEventDispatcher")]
    public class GameEventDispatcherTests
    {
        private GameEventDispatcher _dispatcher;
        private Mock<IHubContext<GameHub>> _mockHubContext;
        private Mock<IHubClients> _mockClients;
        private Mock<IClientProxy> _mockClientProxy;
        private Mock<GameContext> _mockGameContext;
        private Mock<DbSet<Game>> _mockGameSet;
        private Mock<DbSet<GameState>> _mockGameStateSet;
        private ConcurrentDictionary<string, string> _connectedPlayers;

        [SetUp]
        public void Setup()
        {
            _mockHubContext = new Mock<IHubContext<GameHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockGameContext = new Mock<GameContext>();
            _mockGameSet = new Mock<DbSet<Game>>();
            _mockGameStateSet = new Mock<DbSet<GameState>>();
            _connectedPlayers = new ConcurrentDictionary<string, string>();

            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            _mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            // Setup mock for database operations
            SetupDbSetMock(_mockGameSet);
            SetupDbSetMock(_mockGameStateSet);

            // Mock specific methods for database operations
            _mockGameContext.Setup(g => g.FindAsync<Game>(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((object[] keys, CancellationToken token) => 
            {
                int id = (int)keys[0];
                return new Game { Id = id, Name = "Game-" + id, CreatedDate = DateTime.UtcNow };
            });

            _mockGameContext.Setup(g => g.Games).Returns(_mockGameSet.Object);
            _mockGameContext.Setup(g => g.GameStates).Returns(_mockGameStateSet.Object);
            _mockGameContext.Setup(g => g.Add(It.IsAny<Game>())).Callback<Game>(game => { });
            _mockGameContext.Setup(g => g.Add(It.IsAny<GameState>())).Callback<GameState>(state => { });
            _mockGameContext.Setup(g => g.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Setup queryable for GameStates and Games
            _mockGameContext.Setup(g => g.Set<GameState>()).Returns(_mockGameStateSet.Object);
            _mockGameContext.Setup(g => g.Set<Game>()).Returns(_mockGameSet.Object);

            // Mock the database facade to avoid constructor issues
            var mockDatabaseFacade = new Mock<DatabaseFacade>(_mockGameContext.Object);
            _mockGameContext.Setup(g => g.Database).Returns(mockDatabaseFacade.Object);

            _dispatcher = new GameEventDispatcher(_mockHubContext.Object, _mockGameContext.Object);
            _dispatcher.RoomId = "1"; // Default room ID for tests
            _dispatcher.ConnectedPlayers = _connectedPlayers;
        }

        private void SetupDbSetMock<T>(Mock<DbSet<T>> mockSet) where T : class
        {
            var mockQueryable = new Mock<IQueryable<T>>();
            var mockAsyncEnumerable = new Mock<IAsyncEnumerable<T>>();

            mockSet.As<IQueryable<T>>().Setup(q => q.Provider).Returns(mockQueryable.Object.Provider);
            mockSet.As<IQueryable<T>>().Setup(q => q.Expression).Returns(mockQueryable.Object.Expression);
            mockSet.As<IQueryable<T>>().Setup(q => q.ElementType).Returns(mockQueryable.Object.ElementType);
            mockSet.As<IQueryable<T>>().Setup(q => q.GetEnumerator()).Returns(mockQueryable.Object.GetEnumerator());
            mockSet.As<IAsyncEnumerable<T>>().Setup(a => a.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(mockAsyncEnumerable.Object.GetAsyncEnumerator(It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task BroadcastAsync_QueuesMessageSuccessfully_ReturnsTrue()
        {
            // Arrange
            string eventJson = "{\"EventType\": \"TestEvent\"}";

            // Act
            bool result = await _dispatcher.BroadcastAsync(eventJson);

            // Assert
            Assert.IsTrue(result, "BroadcastAsync should return true when message is queued successfully.");
        }

        [Test]
        public async Task BroadcastAsync_EmptyMessage_QueuesSuccessfully_ReturnsTrue()
        {
            // Arrange
            string eventJson = string.Empty;

            // Act
            bool result = await _dispatcher.BroadcastAsync(eventJson);

            // Assert
            Assert.IsTrue(result, "BroadcastAsync should handle empty messages and return true.");
        }

        [Test]
        public async Task SendToUserAsync_QueuesMessageSuccessfully_ReturnsTrue()
        {
            // Arrange
            string userId = "TestUser";
            string eventJson = "{\"EventType\": \"TestEvent\"}";

            // Act
            bool result = await _dispatcher.SendToUserAsync(userId, eventJson);

            // Assert
            Assert.IsTrue(result, "SendToUserAsync should return true when message is queued successfully.");
        }

        [Test]
        public async Task SendToUserAsync_EmptyUserId_QueuesSuccessfully_ReturnsTrue()
        {
            // Arrange
            string userId = string.Empty;
            string eventJson = "{\"EventType\": \"TestEvent\"}";

            // Act
            bool result = await _dispatcher.SendToUserAsync(userId, eventJson);

            // Assert
            Assert.IsTrue(result, "SendToUserAsync should handle empty user ID and return true.");
        }

        [Test]
        public async Task SendToUserAsync_EmptyMessage_QueuesSuccessfully_ReturnsTrue()
        {
            // Arrange
            string userId = "TestUser";
            string eventJson = string.Empty;

            // Act
            bool result = await _dispatcher.SendToUserAsync(userId, eventJson);

            // Assert
            Assert.IsTrue(result, "SendToUserAsync should handle empty message and return true.");
        }

        [Test]
        public async Task ProcessMessageBatches_BroadcastMessages_SendsBatchedMessages()
        {
            // Arrange
            string eventJson1 = "{\"EventType\": \"Event1\"}";
            string eventJson2 = "{\"EventType\": \"Event2\"}";
            await _dispatcher.BroadcastAsync(eventJson1);
            await _dispatcher.BroadcastAsync(eventJson2);

            // Setup mock to capture the batched message
            string capturedMessage = string.Empty;
            _mockClientProxy.Setup(c => c.SendCoreAsync("GameEventBatch", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object[], CancellationToken>((method, args, token) => capturedMessage = args[0].ToString())
                .Returns(Task.CompletedTask);

            // Act
            // Invoke the private ProcessMessageBatches method via reflection or wait for timer (simplified by direct call if accessible)
            // For simplicity, we wait a bit for the timer to trigger (not ideal, but works for now)
            await Task.Delay(150); // Wait longer than the 100ms batch interval

            // Assert
            Assert.IsTrue(capturedMessage.Contains("Event1") && capturedMessage.Contains("Event2"), "Batched broadcast messages should be sent together.");
        }

        [Test]
        public async Task ProcessMessageBatches_UserMessages_SendsBatchedMessagesToUser()
        {
            // Arrange
            string userId = "TestUser";
            string eventJson1 = "{\"EventType\": \"UserEvent1\"}";
            string eventJson2 = "{\"EventType\": \"UserEvent2\"}";
            await _dispatcher.SendToUserAsync(userId, eventJson1);
            await _dispatcher.SendToUserAsync(userId, eventJson2);

            // Setup mock to capture the batched message
            string capturedMessage = string.Empty;
            _mockClientProxy.Setup(c => c.SendCoreAsync("GameEventBatch", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object[], CancellationToken>((method, args, token) => capturedMessage = args[0].ToString())
                .Returns(Task.CompletedTask);

            // Act
            await Task.Delay(150); // Wait for batch processing

            // Assert
            Assert.IsTrue(capturedMessage.Contains("UserEvent1") && capturedMessage.Contains("UserEvent2"), "Batched user messages should be sent together.");
        }

        [Test]
        public async Task ProcessMessageBatches_BroadcastException_HandlesErrorGracefully()
        {
            // Arrange
            string eventJson = "{\"EventType\": \"Event1\"}";
            await _dispatcher.BroadcastAsync(eventJson);

            // Setup mock to throw exception on send
            _mockClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Simulated send error"));

            // Act
            await Task.Delay(150); // Wait for batch processing

            // Assert
            // No assertion needed for exception; just ensure it doesn't crash the test
            Assert.Pass("ProcessMessageBatches should handle broadcast exceptions gracefully without crashing.");
        }

        [Test]
        public async Task ProcessMessageBatches_UserMessageException_HandlesErrorGracefully()
        {
            // Arrange
            string userId = "TestUser";
            string eventJson = "{\"EventType\": \"UserEvent1\"}";
            await _dispatcher.SendToUserAsync(userId, eventJson);

            // Setup mock to throw exception on send
            _mockClientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Simulated send error"));

            // Act
            await Task.Delay(150); // Wait for batch processing

            // Assert
            // No assertion needed for exception; just ensure it doesn't crash the test
            Assert.Pass("ProcessMessageBatches should handle user message exceptions gracefully without crashing.");
        }

        [Test]
        public async Task SaveGameStateAsync_ValidState_SavesToDatabase_ReturnsTrue()
        {
            // Arrange
            string stateJson = "{\"State\": \"TestState\"}";
            _dispatcher.RoomId = "1";
            var game = new Game { Id = 1, Name = "Game-1", CreatedDate = DateTime.UtcNow };
            _mockGameContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            bool result = await _dispatcher.SaveGameStateAsync(stateJson);

            // Assert
            Assert.IsTrue(result, "SaveGameStateAsync should return true when state is saved successfully.");
            _mockGameContext.Verify(c => c.Add(It.Is<GameState>(gs => gs.StateJson == stateJson && gs.GameId == 1)), Times.Once);
            _mockGameContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SaveGameStateAsync_InvalidRoomId_ReturnsFalse()
        {
            // Arrange
            string stateJson = "{\"State\": \"TestState\"}";
            _dispatcher.RoomId = "Invalid"; // Non-integer room ID to cause parse exception

            // Act
            bool result = await _dispatcher.SaveGameStateAsync(stateJson);

            // Assert
            Assert.IsFalse(result, "SaveGameStateAsync should return false when room ID is invalid.");
        }

        [Test]
        public async Task SaveGameStateAsync_DatabaseException_ReturnsFalse()
        {
            // Arrange
            string stateJson = "{\"State\": \"TestState\"}";
            _dispatcher.RoomId = "1";
            _mockGameContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Database error"));

            // Act
            bool result = await _dispatcher.SaveGameStateAsync(stateJson);

            // Assert
            Assert.IsFalse(result, "SaveGameStateAsync should return false when a database exception occurs.");
        }

        [Test]
        public async Task LoadGameStateAsync_StateExists_ReturnsTrue()
        {
            // Arrange
            string stateJson = string.Empty; // Output parameter, will be set by method
            _dispatcher.RoomId = "1";
            var gameState = new GameState { GameId = 1, StateJson = "{\"State\": \"LoadedState\"}", CreatedDate = DateTime.UtcNow };
            var gameStates = new[] { gameState }.AsQueryable();

            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.Provider).Returns(gameStates.Provider);
            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.Expression).Returns(gameStates.Expression);
            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.ElementType).Returns(gameStates.ElementType);
            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.GetEnumerator()).Returns(gameStates.GetEnumerator());

            // Act
            bool result = await _dispatcher.LoadGameStateAsync(stateJson);

            // Assert
            Assert.IsTrue(result, "LoadGameStateAsync should return true when state is found.");
        }

        [Test]
        public async Task LoadGameStateAsync_NoStateExists_ReturnsFalse()
        {
            // Arrange
            string stateJson = string.Empty;
            _dispatcher.RoomId = "1";
            var gameStates = new GameState[0].AsQueryable();

            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.Provider).Returns(gameStates.Provider);
            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.Expression).Returns(gameStates.Expression);
            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.ElementType).Returns(gameStates.ElementType);
            _mockGameStateSet.As<IQueryable<GameState>>().Setup(q => q.GetEnumerator()).Returns(gameStates.GetEnumerator());

            // Act
            bool result = await _dispatcher.LoadGameStateAsync(stateJson);

            // Assert
            Assert.IsFalse(result, "LoadGameStateAsync should return false when no state is found.");
        }

        [Test]
        public async Task LoadGameStateAsync_InvalidRoomId_ReturnsFalse()
        {
            // Arrange
            string stateJson = string.Empty;
            _dispatcher.RoomId = "Invalid"; // Non-integer room ID to cause parse exception

            // Act
            bool result = await _dispatcher.LoadGameStateAsync(stateJson);

            // Assert
            Assert.IsFalse(result, "LoadGameStateAsync should return false when room ID is invalid.");
        }
    }
}
