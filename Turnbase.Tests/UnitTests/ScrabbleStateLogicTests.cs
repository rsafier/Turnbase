using NUnit.Framework;
using Turnbase.Rules;
using System.Collections.Generic;

namespace Turnbase.Tests.UnitTests
{
    [TestFixture]
    public class ScrabbleStateLogicTests
    {
        private ScrabbleStateLogic _logic;

        [SetUp]
        public void Setup()
        {
            _logic = new ScrabbleStateLogic();
        }

        [Test]
        public void ValidateMove_ValidFirstMove_ReturnsTrue()
        {
            string currentStateJson = @"{
                ""Board"": [],
                ""Players"": [
                    { ""Id"": ""player1"", ""Rack"": [""H"", ""E"", ""L"", ""L"", ""O"", ""A"", ""B""] }
                ],
                ""CurrentPlayer"": ""player1"",
                ""FirstMove"": true
            }";

            string moveJson = @"{
                ""PlayerId"": ""player1"",
                ""Tiles"": [
                    { ""Letter"": ""H"", ""X"": 7, ""Y"": 7 },
                    { ""Letter"": ""E"", ""X"": 8, ""Y"": 7 },
                    { ""Letter"": ""L"", ""X"": 9, ""Y"": 7 },
                    { ""Letter"": ""L"", ""X"": 10, ""Y"": 7 },
                    { ""Letter"": ""O"", ""X"": 11, ""Y"": 7 }
                ]
            }";

            bool result = _logic.ValidateMove(currentStateJson, moveJson, out string? error);

            Assert.IsTrue(result, error);
            Assert.IsNull(error);
        }

        [Test]
        public void ValidateMove_FirstMoveNotOnCenter_ReturnsFalse()
        {
            string currentStateJson = @"{
                ""Board"": [],
                ""Players"": [
                    { ""Id"": ""player1"", ""Rack"": [""H"", ""E"", ""L"", ""L"", ""O"", ""A"", ""B""] }
                ],
                ""CurrentPlayer"": ""player1"",
                ""FirstMove"": true
            }";

            string moveJson = @"{
                ""PlayerId"": ""player1"",
                ""Tiles"": [
                    { ""Letter"": ""H"", ""X"": 0, ""Y"": 0 },
                    { ""Letter"": ""E"", ""X"": 1, ""Y"": 0 }
                ]
            }";

            bool result = _logic.ValidateMove(currentStateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("center"));
        }

        [Test]
        public void ValidateMove_InvalidWord_ReturnsFalse()
        {
            string currentStateJson = @"{
                ""Board"": [],
                ""Players"": [
                    { ""Id"": ""player1"", ""Rack"": [""Q"", ""Z"", ""X"", ""Y"", ""P"", ""A"", ""B""] }
                ],
                ""CurrentPlayer"": ""player1"",
                ""FirstMove"": true
            }";

            string moveJson = @"{
                ""PlayerId"": ""player1"",
                ""Tiles"": [
                    { ""Letter"": ""Q"", ""X"": 7, ""Y"": 7 },
                    { ""Letter"": ""Z"", ""X"": 8, ""Y"": 7 }
                ]
            }";

            bool result = _logic.ValidateMove(currentStateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("invalid word"));
        }

        [Test]
        public void ValidateMove_NotCurrentPlayer_ReturnsFalse()
        {
            string currentStateJson = @"{
                ""Board"": [],
                ""Players"": [
                    { ""Id"": ""player1"", ""Rack"": [""H"", ""E"", ""L"", ""L"", ""O"", ""A"", ""B""] },
                    { ""Id"": ""player2"", ""Rack"": [""T"", ""E"", ""S"", ""T"", ""I"", ""N"", ""G""] }
                ],
                ""CurrentPlayer"": ""player1"",
                ""FirstMove"": true
            }";

            string moveJson = @"{
                ""PlayerId"": ""player2"",
                ""Tiles"": [
                    { ""Letter"": ""T"", ""X"": 7, ""Y"": 7 },
                    { ""Letter"": ""E"", ""X"": 8, ""Y"": 7 }
                ]
            }";

            bool result = _logic.ValidateMove(currentStateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("not your turn"));
        }

        [Test]
        public void ValidateMove_TilesNotInRack_ReturnsFalse()
        {
            string currentStateJson = @"{
                ""Board"": [],
                ""Players"": [
                    { ""Id"": ""player1"", ""Rack"": [""A"", ""B"", ""C"", ""D"", ""E"", ""F"", ""G""] }
                ],
                ""CurrentPlayer"": ""player1"",
                ""FirstMove"": true
            }";

            string moveJson = @"{
                ""PlayerId"": ""player1"",
                ""Tiles"": [
                    { ""Letter"": ""H"", ""X"": 7, ""Y"": 7 },
                    { ""Letter"": ""I"", ""X"": 8, ""Y"": 7 }
                ]
            }";

            bool result = _logic.ValidateMove(currentStateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("not in rack"));
        }

        [Test]
        public void ApplyMove_ValidMove_UpdatesState()
        {
            string currentStateJson = @"{
                ""Board"": [],
                ""Players"": [
                    { ""Id"": ""player1"", ""Rack"": [""H"", ""E"", ""L"", ""L"", ""O"", ""A"", ""B""] },
                    { ""Id"": ""player2"", ""Rack"": [""T"", ""E"", ""S"", ""T"", ""I"", ""N"", ""G""] }
                ],
                ""CurrentPlayer"": ""player1"",
                ""FirstMove"": true
            }";

            string moveJson = @"{
                ""PlayerId"": ""player1"",
                ""Tiles"": [
                    { ""Letter"": ""H"", ""X"": 7, ""Y"": 7 },
                    { ""Letter"": ""E"", ""X"": 8, ""Y"": 7 },
                    { ""Letter"": ""L"", ""X"": 9, ""Y"": 7 },
                    { ""Letter"": ""L"", ""X"": 10, ""Y"": 7 },
                    { ""Letter"": ""O"", ""X"": 11, ""Y"": 7 }
                ]
            }";

            string newStateJson = _logic.ApplyMove(currentStateJson, moveJson, out string? error);

            Assert.IsNull(error);
            Assert.IsNotNull(newStateJson);
            Assert.That(newStateJson, Does.Contain("player2"));
            Assert.That(newStateJson, Does.Not.Contain("FirstMove\": true"));
        }

        [Test]
        public void CalculateScores_ReturnsCorrectScores()
        {
            string currentStateJson = @"{
                ""BoardTiles"": [
                    { ""Letter"": ""H"", ""X"": 7, ""Y"": 7 },
                    { ""Letter"": ""E"", ""X"": 8, ""Y"": 7 },
                    { ""Letter"": ""L"", ""X"": 9, ""Y"": 7 },
                    { ""Letter"": ""L"", ""X"": 10, ""Y"": 7 },
                    { ""Letter"": ""O"", ""X"": 11, ""Y"": 7 }
                ],
                ""Players"": [
                    { ""Id"": ""player1"", ""Rack"": [""A"", ""B""] },
                    { ""Id"": ""player2"", ""Rack"": [""T"", ""E"", ""S"", ""T"", ""I"", ""N"", ""G""] }
                ],
                ""CurrentPlayer"": ""player2"",
                ""FirstMove"": false,
                ""PlayerOrder"": [""player1"", ""player2""],
                ""PlayerScores"": { ""player1"": 8 }
            }";

            IDictionary<string, long> scores = _logic.CalculateScores(currentStateJson);

            Assert.IsNotNull(scores);
            Assert.That(scores, Contains.Key("player1"));
            Assert.That(scores["player1"], Is.EqualTo(8));
        }
    }
}
