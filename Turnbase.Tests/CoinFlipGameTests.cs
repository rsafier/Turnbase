using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Threading.Tasks;
using Turnbase.Server.GameLogic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

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
            _mockDispatcher.Setup(d => d.ConnectedPlayers).Returns(new ConcurrentDictionary<string, string>());
            var mockLogger = new Mock<ILogger<BaseGameInstance>>();
            _game = new CoinFlipGame(_mockDispatcher.Object, mockLogger.Object);
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
        public async Task ProcessPlayerEventAsync_GameNotActive_DoesNotProcessEvent()
        {
            // Arrange
            var userId = "Player1";
            var messageJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act
            await _game.ProcessPlayerEventAsync(userId, messageJson);

            // Assert
            _mockDispatcher.Verify(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_GameInactiveAfterFirstMove_DoesNotProcessSecondMove()
        {
            // Arrange
            var userId = "Player1";
            var otherUserId = "Player2";
            var messageJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>())).ReturnsAsync(true);
            _mockDispatcher.Setup(d => d.ConnectedPlayers).Returns(new ConcurrentDictionary<string, string>(
                new System.Collections.Generic.Dictionary<string, string> { { userId, "" }, { otherUserId, "" } }));

            // Act
            await _game.StartAsync(); // Start the game to set it as active
            await _game.ProcessPlayerEventAsync(otherUserId, messageJson); // First player makes move and game ends
            await _game.ProcessPlayerEventAsync(userId, messageJson); // Second player tries to play but game is inactive

            // Assert
            // Since the game ends after the first flip, the second move is ignored due to game inactivity
            _mockDispatcher.Verify(d => d.SendToUserAsync(userId, It.IsAny<string>()), Times.Never);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("CoinFlipResult"))), Times.Once);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("GameEnded"))), Times.Once);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_ValidCoinFlip_BroadcastsResultAndEndsGame()
        {
            // Arrange
            var userId = "Player1";
            var messageJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>())).ReturnsAsync(true);
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act
            await _game.StartAsync();
            await _game.ProcessPlayerEventAsync(userId, messageJson);

            // Assert
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("CoinFlipResult"))), Times.Once);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => s.Contains("GameEnded"))), Times.Once);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_InvalidAction_DoesNotSendErrorMessage()
        {
            // Arrange
            var userId = "Player1";
            var messageJson = JsonConvert.SerializeObject(new { Action = "InvalidAction" });
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act
            await _game.StartAsync();
            await _game.ProcessPlayerEventAsync(userId, messageJson);

            // Assert
            _mockDispatcher.Verify(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_MalformedJson_SendsErrorMessage()
        {
            // Arrange
            var userId = "Player1";
            var messageJson = "invalid json";
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Act
            await _game.StartAsync();
            await _game.ProcessPlayerEventAsync(userId, messageJson);

            // Assert
            _mockDispatcher.Verify(d => d.SendToUserAsync(userId, It.Is<string>(s => s.Contains("Error"))), Times.Once);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_OpponentDetermination_WorksWithMultiplePlayers()
        {
            // Arrange
            var userId = "Player1";
            var opponentId = "Player2";
            var messageJson = JsonConvert.SerializeObject(new { Action = "FlipCoin" });
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>())).ReturnsAsync(true);
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _mockDispatcher.Setup(d => d.ConnectedPlayers).Returns(new ConcurrentDictionary<string, string>(
                new System.Collections.Generic.Dictionary<string, string> { { userId, "" }, { opponentId, "" } }));

            // Act
            await _game.StartAsync();
            await _game.ProcessPlayerEventAsync(userId, messageJson);

            // Assert
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.Is<string>(s => 
                s.Contains("CoinFlipResult") && (s.Contains(userId) || s.Contains(opponentId)))), Times.Once);
        }
    }
}
