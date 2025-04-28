using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Threading.Tasks;
using Turnbase.Server.GameLogic;

namespace Turnbase.Tests
{
    [TestFixture]
    public class CoinFlipGameTests
    {
        private CoinFlipGame _game;
        private Mock<IGameEventDispatcher> _mockDispatcher;

        [SetUp]
        public void Setup()
        {
            _mockDispatcher = new Mock<IGameEventDispatcher>();
            _game = new CoinFlipGame();
            _game.EventDispatcher = _mockDispatcher.Object;
        }

        [Test]
        public async Task StartAsync_InitializesGameAndBroadcastsStartEvent_ReturnsTrue()
        {
            // Arrange
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>())).ReturnsAsync(true);

            // Act
            var result = await _game.StartAsync();

            // Assert
            Assert.IsTrue(result);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("GameStarted"))), Times.Once);
        }

        [Test]
        public async Task StopAsync_BroadcastsEndEventAndStopsGame_ReturnsTrue()
        {
            // Arrange
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>())).ReturnsAsync(true);

            // Act
            var result = await _game.StopAsync();

            // Assert
            Assert.IsTrue(result);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("GameEnded"))), Times.Once);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_NotPlayersTurn_SendsErrorMessage()
        {
            // Arrange
            await _game.StartAsync(); // Start game to make it active
            string userId = "User1";
            string otherUserId = "User2";
            var moveJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Simulate another player's turn by processing a move for them first
            // This test assumes turn logic, so we might need to adjust based on implementation details

            // Act
            await _game.ProcessPlayerEventAsync(otherUserId, moveJson); // First player sets turn
            await _game.ProcessPlayerEventAsync(userId, moveJson); // Second player tries to play out of turn

            // Assert
            _mockDispatcher.Verify(d => d.SendToUserAsync(userId, It.Is<string>(s => s.Contains("Not your turn"))), Times.Once);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_ValidCoinFlip_BroadcastsResultAndEndsGame()
        {
            // Arrange
            await _game.StartAsync(); // Start game to make it active
            string userId = "User1";
            var moveJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>())).ReturnsAsync(true);

            // Act
            await _game.ProcessPlayerEventAsync(userId, moveJson);

            // Assert
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("CoinFlipResult"))), Times.Once);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("GameEnded"))), Times.Once);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_InvalidAction_SendsErrorMessage()
        {
            // Arrange
            await _game.StartAsync(); // Start game to make it active
            string userId = "User1";
            var moveJson = JsonConvert.SerializeObject(new { Action = "InvalidAction" });
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act
            await _game.ProcessPlayerEventAsync(userId, moveJson);

            // Assert
            _mockDispatcher.Verify(d => d.SendToUserAsync(userId, It.Is<string>(s => s.Contains("Error"))), Times.Once);
        }
    }
}
