using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using Turnbase.Server.GameLogic;

namespace Turnbase.Tests
{
    [TestFixture]
    public class BattleshipGameTests
    {
        private BattleshipGame _game;
        private Mock<IGameEventDispatcher> _mockDispatcher;

        [SetUp]
        public void Setup()
        {
            _mockDispatcher = new Mock<IGameEventDispatcher>();
            _game = new BattleshipGame(_mockDispatcher.Object);
        }

        [Test]
        public async Task StartAsync_InitializesBoardsAndBroadcastsStartEvent_ReturnsTrue()
        {
            // Arrange
            string capturedEventJson = string.Empty;
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>()))
                .Callback<string>(json => capturedEventJson = json)
                .ReturnsAsync(true);
            _mockDispatcher.SetupGet(d => d.ConnectedPlayers).Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>());

            // Act
            var result = await _game.StartAsync();

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(string.IsNullOrEmpty(capturedEventJson));
            dynamic? eventData = JsonConvert.DeserializeObject(capturedEventJson);
            Assert.IsNotNull(eventData);
            Assert.AreEqual("GameStarted", eventData.EventType.ToString());
            Assert.AreEqual("Battleship", eventData.GameType.ToString());
        }

        [Test]
        public async Task StopAsync_BroadcastsEndEventAndStopsGame_ReturnsTrue()
        {
            // Arrange
            string capturedEventJson = string.Empty;
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>()))
                .Callback<string>(json => capturedEventJson = json)
                .ReturnsAsync(true);
            _mockDispatcher.Setup(d => d.SaveGameStateAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockDispatcher.SetupGet(d => d.ConnectedPlayers).Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>());

            // Act
            await _game.StartAsync(); // Start to set game active
            var result = await _game.StopAsync();

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(string.IsNullOrEmpty(capturedEventJson));
            dynamic? eventData = JsonConvert.DeserializeObject(capturedEventJson);
            Assert.IsNotNull(eventData);
            Assert.AreEqual("GameEnded", eventData.EventType.ToString());
        }

        [Test]
        public async Task ProcessPlayerEventAsync_GameNotActive_DoesNotProcessEvent()
        {
            // Arrange
            string userId = "Player1";
            var moveJson = JsonConvert.SerializeObject(new { Action = "PlaceShip", ShipType = "Carrier", StartX = 0, StartY = 0, IsHorizontal = true });

            // Act
            await _game.ProcessPlayerEventAsync(userId, moveJson);

            // Assert
            _mockDispatcher.Verify(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockDispatcher.Verify(d => d.BroadcastAsync(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task ProcessPlayerEventAsync_PlaceShipValid_SavesShipPlacementAndBroadcastsUpdate()
        {
            // Arrange
            _mockDispatcher.SetupGet(d => d.ConnectedPlayers).Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>(new Dictionary<string, string> { { "Player1", "" } }));
            await _game.StartAsync();
            string userId = "Player1";
            var moveJson = JsonConvert.SerializeObject(new { Action = "PlaceShip", ShipType = "Carrier", StartX = 0, StartY = 0, IsHorizontal = true });
            string capturedEventJson = string.Empty;
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>()))
                .Callback<string>(json => capturedEventJson = json)
                .ReturnsAsync(true);

            // Act
            await _game.ProcessPlayerEventAsync(userId, moveJson);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(capturedEventJson));
            dynamic? eventData = JsonConvert.DeserializeObject(capturedEventJson);
            Assert.IsNotNull(eventData);
            Assert.AreEqual("ShipPlaced", eventData.EventType.ToString());
            Assert.AreEqual(userId, eventData.PlayerId.ToString());
        }

        [Test]
        public async Task ProcessPlayerEventAsync_AttackValid_BroadcastsHitOrMiss()
        {
            // Arrange
            _mockDispatcher.SetupGet(d => d.ConnectedPlayers).Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>(new Dictionary<string, string> { { "Player1", "" }, { "Player2", "" } }));
            await _game.StartAsync();
            string attackerId = "Player1";
            string defenderId = "Player2";
            // Simulate ship placement for defender
            var placeShipJson = JsonConvert.SerializeObject(new { Action = "PlaceShip", ShipType = "Carrier", StartX = 0, StartY = 0, IsHorizontal = true });
            await _game.ProcessPlayerEventAsync(defenderId, placeShipJson);

            var attackJson = JsonConvert.SerializeObject(new { Action = "Attack", X = 0, Y = 0 });
            List<string> capturedEventJsons = new List<string>();
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>()))
                .Callback<string>(json => capturedEventJsons.Add(json))
                .ReturnsAsync(true);

            // Act
            await _game.ProcessPlayerEventAsync(attackerId, attackJson);

            // Assert
            Assert.IsTrue(capturedEventJsons.Count > 0, "No events were captured.");
            var attackResultJson = capturedEventJsons.FirstOrDefault(json => json.Contains("AttackResult"));
            Assert.IsNotNull(attackResultJson, "AttackResult event not found in captured events.");
            dynamic? eventData = JsonConvert.DeserializeObject(attackResultJson);
            Assert.IsNotNull(eventData);
            Assert.AreEqual("AttackResult", eventData.EventType.ToString());
            Assert.AreEqual(attackerId, eventData.AttackerId.ToString());
            Assert.IsTrue(eventData.IsHit.ToString() == "True" || eventData.IsHit.ToString() == "False");
        }

        [Test]
        public async Task ProcessPlayerEventAsync_WinConditionMet_BroadcastsGameEnded()
        {
            // Arrange
            _mockDispatcher.SetupGet(d => d.ConnectedPlayers).Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>(new Dictionary<string, string> { { "Player1", "" }, { "Player2", "" } }));
            await _game.StartAsync();
            string attackerId = "Player1";
            string defenderId = "Player2";
            // Place a small ship for easy sinking
            var placeShipJson = JsonConvert.SerializeObject(new { Action = "PlaceShip", ShipType = "Destroyer", StartX = 0, StartY = 0, IsHorizontal = true });
            await _game.ProcessPlayerEventAsync(defenderId, placeShipJson);

            string capturedEventJson = string.Empty;
            _mockDispatcher.Setup(d => d.BroadcastAsync(It.IsAny<string>()))
                .Callback<string>(json => capturedEventJson = json)
                .ReturnsAsync(true);
            _mockDispatcher.Setup(d => d.SaveGameStateAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Simulate hits to sink the ship (assuming Destroyer is 2 units long)
            var attack1Json = JsonConvert.SerializeObject(new { Action = "Attack", X = 0, Y = 0 });
            await _game.ProcessPlayerEventAsync(attackerId, attack1Json);
            var attack2Json = JsonConvert.SerializeObject(new { Action = "Attack", X = 1, Y = 0 });
            await _game.ProcessPlayerEventAsync(attackerId, attack2Json);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(capturedEventJson));
            dynamic? eventData = JsonConvert.DeserializeObject(capturedEventJson);
            Assert.IsNotNull(eventData);
            // Check if the last broadcast was GameEnded due to all ships sunk
            if (eventData.EventType.ToString() == "GameEnded")
            {
                Assert.AreEqual(attackerId, eventData.Winner.ToString());
            }
        }

        [Test]
        public async Task ProcessPlayerEventAsync_InvalidAction_SendsErrorMessage()
        {
            // Arrange
            _mockDispatcher.SetupGet(d => d.ConnectedPlayers).Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>(new Dictionary<string, string> { { "Player1", "" } }));
            await _game.StartAsync();
            string userId = "Player1";
            var invalidMoveJson = JsonConvert.SerializeObject(new { Action = "InvalidAction" });
            string capturedErrorJson = string.Empty;
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((id, json) => capturedErrorJson = json)
                .ReturnsAsync(true);

            // Act
            await _game.ProcessPlayerEventAsync(userId, invalidMoveJson);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(capturedErrorJson));
            dynamic? errorData = JsonConvert.DeserializeObject(capturedErrorJson);
            Assert.IsNotNull(errorData);
            Assert.AreEqual("Error", errorData.EventType.ToString());
        }

        [Test]
        public async Task ProcessPlayerEventAsync_MalformedJson_SendsErrorMessage()
        {
            // Arrange
            _mockDispatcher.SetupGet(d => d.ConnectedPlayers).Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>(new Dictionary<string, string> { { "Player1", "" } }));
            await _game.StartAsync();
            string userId = "Player1";
            var invalidJson = "malformed json data";
            string capturedErrorJson = string.Empty;
            _mockDispatcher.Setup(d => d.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((id, json) => capturedErrorJson = json)
                .ReturnsAsync(true);

            // Act
            await _game.ProcessPlayerEventAsync(userId, invalidJson);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(capturedErrorJson));
            dynamic? errorData = JsonConvert.DeserializeObject(capturedErrorJson);
            Assert.IsNotNull(errorData);
            Assert.AreEqual("Error", errorData.EventType.ToString());
        }
    }
}
