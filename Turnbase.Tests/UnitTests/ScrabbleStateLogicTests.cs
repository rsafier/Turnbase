using NUnit.Framework;
using Turnbase.Rules;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace Turnbase.Tests.UnitTests
{
    [TestFixture]
    public class ScrabbleStateLogicTests
    {
        private ScrabbleStateLogic _logic;

        // Define the state and move classes as they are in ScrabbleStateLogic
        private class PlayerInfo
        {
            public string Id { get; set; } = "";
            public string[] Rack { get; set; } = new string[0];
        }

        private class ScrabbleState
        {
            private string[][] _board = InitializeBoard();
            public string[][] Board
            {
                get => _board;
                set => _board = value ?? InitializeBoard();
            }
            public List<PlayerInfo> Players { get; set; } = new();
            public Dictionary<string, int> PlayerScores { get; set; } = new();
            public string[] TileBag { get; set; } = new string[0];
            public string CurrentPlayer { get; set; } = "";
            public List<string> PlayerOrder { get; set; } = new();
            public bool FirstMove { get; set; } = true;
            public List<PlacedTile> BoardTiles
            {
                set
                {
                    if (value != null && value.Count > 0)
                    {
                        _board = InitializeBoard();
                        foreach (var tile in value)
                        {
                            if (tile.X >= 0 && tile.X < 15 && tile.Y >= 0 && tile.Y < 15)
                            {
                                _board[tile.Y][tile.X] = tile.Letter;
                            }
                        }
                    }
                }
            }
        }

        private static string[][] InitializeBoard()
        {
            var board = new string[15][];
            for (int i = 0; i < 15; i++)
            {
                board[i] = new string[15];
            }
            return board;
        }

        private class ScrabbleMove
        {
            public string PlayerId { get; set; } = "";
            public List<PlacedTile> Tiles { get; set; } = new();
        }

        private class PlacedTile
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Letter { get; set; } = "";
        }

        [SetUp]
        public void Setup()
        {
            _logic = new ScrabbleStateLogic();
        }

        [Test]
        public void ValidateMove_ValidFirstMove_ReturnsTrue()
        {
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "H", "E", "L", "L", "O", "A", "B" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true
            };

            var move = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "H", X = 7, Y = 7 },
                    new PlacedTile { Letter = "E", X = 8, Y = 7 },
                    new PlacedTile { Letter = "L", X = 9, Y = 7 },
                    new PlacedTile { Letter = "L", X = 10, Y = 7 },
                    new PlacedTile { Letter = "O", X = 11, Y = 7 }
                }
            };

            string stateJson = JsonSerializer.Serialize(state);
            string moveJson = JsonSerializer.Serialize(move);

            bool result = _logic.ValidateMove(stateJson, moveJson, out string? error);

            Assert.IsTrue(result, error);
            Assert.IsNull(error);
        }

        [Test]
        public void ValidateMove_FirstMoveNotOnCenter_ReturnsFalse()
        {
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "H", "E", "L", "L", "O", "A", "B" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true
            };

            var move = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "H", X = 0, Y = 0 },
                    new PlacedTile { Letter = "E", X = 1, Y = 0 }
                }
            };

            string stateJson = JsonSerializer.Serialize(state);
            string moveJson = JsonSerializer.Serialize(move);

            bool result = _logic.ValidateMove(stateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("center"));
        }

        [Test]
        public void ValidateMove_InvalidWord_ReturnsFalse()
        {
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "Q", "Z", "X", "Y", "P", "A", "B" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true
            };

            var move = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "Q", X = 7, Y = 7 },
                    new PlacedTile { Letter = "Z", X = 8, Y = 7 }
                }
            };

            string stateJson = JsonSerializer.Serialize(state);
            string moveJson = JsonSerializer.Serialize(move);

            bool result = _logic.ValidateMove(stateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("invalid word"));
        }

        [Test]
        public void ValidateMove_NotCurrentPlayer_ReturnsFalse()
        {
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "H", "E", "L", "L", "O", "A", "B" } },
                    new PlayerInfo { Id = "player2", Rack = new[] { "T", "E", "S", "T", "I", "N", "G" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true
            };

            var move = new ScrabbleMove
            {
                PlayerId = "player2",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "T", X = 7, Y = 7 },
                    new PlacedTile { Letter = "E", X = 8, Y = 7 }
                }
            };

            string stateJson = JsonSerializer.Serialize(state);
            string moveJson = JsonSerializer.Serialize(move);

            bool result = _logic.ValidateMove(stateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("not your turn"));
        }

        [Test]
        public void ValidateMove_TilesNotInRack_ReturnsFalse()
        {
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "A", "B", "C", "D", "E", "F", "G" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true
            };

            var move = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "H", X = 7, Y = 7 },
                    new PlacedTile { Letter = "I", X = 8, Y = 7 }
                }
            };

            string stateJson = JsonSerializer.Serialize(state);
            string moveJson = JsonSerializer.Serialize(move);

            bool result = _logic.ValidateMove(stateJson, moveJson, out string? error);

            Assert.IsFalse(result);
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("not in rack"));
        }

        [Test]
        public void ApplyMove_ValidMove_UpdatesState()
        {
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "H", "E", "L", "L", "O", "A", "B" } },
                    new PlayerInfo { Id = "player2", Rack = new[] { "T", "E", "S", "T", "I", "N", "G" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true
            };

            var move = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "H", X = 7, Y = 7 },
                    new PlacedTile { Letter = "E", X = 8, Y = 7 },
                    new PlacedTile { Letter = "L", X = 9, Y = 7 },
                    new PlacedTile { Letter = "L", X = 10, Y = 7 },
                    new PlacedTile { Letter = "O", X = 11, Y = 7 }
                }
            };

            string stateJson = JsonSerializer.Serialize(state);
            string moveJson = JsonSerializer.Serialize(move);

            string newStateJson = _logic.ApplyMove(stateJson, moveJson, out string? error);

            Assert.IsNull(error);
            Assert.IsNotNull(newStateJson);
            Assert.That(newStateJson, Does.Contain("player2"));
            Assert.That(newStateJson, Does.Not.Contain("FirstMove\": true"));
        }

        [Test]
        public void CalculateScores_ReturnsCorrectScores()
        {
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "A", "B" } },
                    new PlayerInfo { Id = "player2", Rack = new[] { "T", "E", "S", "T", "I", "N", "G" } }
                },
                CurrentPlayer = "player2",
                FirstMove = false,
                PlayerOrder = new List<string> { "player1", "player2" },
                PlayerScores = new Dictionary<string, int> { { "player1", 8 } }
            };

            state.BoardTiles = new List<PlacedTile>
            {
                new PlacedTile { Letter = "H", X = 7, Y = 7 },
                new PlacedTile { Letter = "E", X = 8, Y = 7 },
                new PlacedTile { Letter = "L", X = 9, Y = 7 },
                new PlacedTile { Letter = "L", X = 10, Y = 7 },
                new PlacedTile { Letter = "O", X = 11, Y = 7 }
            };

            string stateJson = JsonSerializer.Serialize(state);

            IDictionary<string, long> scores = _logic.CalculateScores(stateJson);

            Assert.IsNotNull(scores);
            Assert.That(scores, Contains.Key("player1"));
            Assert.That(scores["player1"], Is.EqualTo(8));
        }

        [Test]
        public void SimulateGame_5Moves_UpdatesStateCorrectly()
        {
            // Initial state with two players
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "H", "E", "L", "L", "O", "A", "B" } },
                    new PlayerInfo { Id = "player2", Rack = new[] { "T", "E", "S", "T", "I", "N", "G" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true,
                PlayerOrder = new List<string> { "player1", "player2" },
                PlayerScores = new Dictionary<string, int> { { "player1", 0 }, { "player2", 0 } },
                TileBag = new[] { "A", "R", "M", "P", "D", "C", "K", "Y", "W" }
            };

            string stateJson = JsonSerializer.Serialize(state);

            // Move 1: Player 1 plays "HELLO" horizontally on center
            var move1 = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "H", X = 7, Y = 7 },
                    new PlacedTile { Letter = "E", X = 8, Y = 7 },
                    new PlacedTile { Letter = "L", X = 9, Y = 7 },
                    new PlacedTile { Letter = "L", X = 10, Y = 7 },
                    new PlacedTile { Letter = "O", X = 11, Y = 7 }
                }
            };
            string move1Json = JsonSerializer.Serialize(move1);
            bool valid1 = _logic.ValidateMove(stateJson, move1Json, out string? error1);
            Assert.IsTrue(valid1, error1);
            stateJson = _logic.ApplyMove(stateJson, move1Json, out error1);
            Assert.IsNull(error1);
            var updatedState1 = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            Assert.AreEqual("player2", updatedState1.CurrentPlayer);
            Assert.IsFalse(updatedState1.FirstMove);
            // After move, player1 should have drawn tiles from the bag to refill rack to 7
            Assert.AreEqual(7, updatedState1.Players.Find(p => p.Id == "player1").Rack.Length);

            // Move 2: Player 2 plays "TEST" vertically connecting to 'E' in "HELLO"
            var move2 = new ScrabbleMove
            {
                PlayerId = "player2",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "T", X = 8, Y = 6 },
                    new PlacedTile { Letter = "S", X = 8, Y = 8 },
                    new PlacedTile { Letter = "T", X = 8, Y = 9 }
                }
            };
            string move2Json = JsonSerializer.Serialize(move2);
            bool valid2 = _logic.ValidateMove(stateJson, move2Json, out string? error2);
            Assert.IsTrue(valid2, error2);
            stateJson = _logic.ApplyMove(stateJson, move2Json, out error2);
            Assert.IsNull(error2);
            var updatedState2 = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            Assert.AreEqual("player1", updatedState2.CurrentPlayer);

            // Move 3: Player 1 plays a word vertically connecting to 'L' in "HELLO" at position (9,7)
            var updatedPlayer1Move3 = updatedState2.Players.Find(p => p.Id == "player1");
            var rackTilesMove3 = updatedPlayer1Move3.Rack.ToList();
            var move3 = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>()
            };
            
            // Log the rack for debugging
            string rackStringMove3 = string.Join(", ", rackTilesMove3);
            TestContext.WriteLine($"Player 1 Rack for Move 3: [{rackStringMove3}]");
            
            // Try to form a valid word with available tiles connecting to "L" at (9,7) downwards
            // Assuming rack has A, B, A, R, M, P, D - try to form "LAD" which is a valid word
            var letterA = rackTilesMove3.FirstOrDefault(t => t == "A");
            var letterD = rackTilesMove3.FirstOrDefault(t => t == "D");
            if (letterA != null && letterD != null)
            {
                move3.Tiles.Add(new PlacedTile { Letter = "A", X = 9, Y = 8 });
                move3.Tiles.Add(new PlacedTile { Letter = "D", X = 9, Y = 9 });
                TestContext.WriteLine($"Attempting move with tiles A at (9,8) and D at (9,9) to form 'LAD'");
            }
            else if (rackTilesMove3.Count >= 2)
            {
                move3.Tiles.Add(new PlacedTile { Letter = rackTilesMove3[0], X = 9, Y = 8 });
                move3.Tiles.Add(new PlacedTile { Letter = rackTilesMove3[1], X = 9, Y = 9 });
                TestContext.WriteLine($"Attempting move with tiles {rackTilesMove3[0]} at (9,8) and {rackTilesMove3[1]} at (9,9)");
            }
            else if (rackTilesMove3.Count >= 1)
            {
                move3.Tiles.Add(new PlacedTile { Letter = rackTilesMove3[0], X = 9, Y = 8 });
                TestContext.WriteLine($"Attempting move with tile {rackTilesMove3[0]} at (9,8)");
            }
            else
            {
                Assert.Fail("Not enough tiles in rack to make a move.");
            }
            
            string move3Json = JsonSerializer.Serialize(move3);
            bool valid3 = _logic.ValidateMove(stateJson, move3Json, out string? error3);
            if (!valid3)
            {
                TestContext.WriteLine($"Move 3 validation failed: {error3}");
            }
            Assert.IsTrue(valid3, $"Move 3 validation failed: {error3}");
            stateJson = _logic.ApplyMove(stateJson, move3Json, out error3);
            Assert.IsNull(error3, $"Move 3 application failed: {error3}");
            var updatedState3 = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            Assert.AreEqual("player2", updatedState3.CurrentPlayer);

            // Move 4: Player 2 plays a word horizontally connecting to 'O' in "HELLO" at position (11,7)
            var updatedPlayer2Move4 = updatedState3.Players.Find(p => p.Id == "player2");
            var rackTilesMove4 = updatedPlayer2Move4.Rack.ToList();
            var move4 = new ScrabbleMove
            {
                PlayerId = "player2",
                Tiles = new List<PlacedTile>()
            };
            
            // Log the rack for debugging
            string rackStringMove4 = string.Join(", ", rackTilesMove4);
            TestContext.WriteLine($"Player 2 Rack for Move 4: [{rackStringMove4}]");
            
            // Try to form a valid word with available tiles connecting to "O" at (11,7) to the right
            // Assuming rack has E, I, N, G, C, K, Y - try to form "ON" which is a valid word
            var letterN = rackTilesMove4.FirstOrDefault(t => t == "N");
            if (letterN != null)
            {
                move4.Tiles.Add(new PlacedTile { Letter = "N", X = 12, Y = 7 });
                TestContext.WriteLine($"Attempting move with tile N at (12,7) to form 'ON'");
            }
            else if (rackTilesMove4.Count >= 1)
            {
                move4.Tiles.Add(new PlacedTile { Letter = rackTilesMove4[0], X = 12, Y = 7 });
                TestContext.WriteLine($"Attempting move with tile {rackTilesMove4[0]} at (12,7)");
            }
            else
            {
                Assert.Fail("Not enough tiles in rack to make a move.");
            }
            
            string move4Json = JsonSerializer.Serialize(move4);
            bool valid4 = _logic.ValidateMove(stateJson, move4Json, out string? error4);
            if (!valid4)
            {
                TestContext.WriteLine($"Move 4 validation failed: {error4}");
            }
            Assert.IsTrue(valid4, $"Move 4 validation failed: {error4}");
            stateJson = _logic.ApplyMove(stateJson, move4Json, out error4);
            Assert.IsNull(error4, $"Move 4 application failed: {error4}");
            var updatedState4 = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            Assert.AreEqual("player1", updatedState4.CurrentPlayer);

            // Move 5: Player 1 plays a new word using drawn tiles
            var updatedPlayer1 = updatedState4.Players.Find(p => p.Id == "player1");
            var rackTiles = updatedPlayer1.Rack.ToList();
            var move5 = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>()
            };
            
            // Log the rack for debugging
            string rackString = string.Join(", ", rackTiles);
            TestContext.WriteLine($"Player 1 Rack for Move 5: [{rackString}]");
            
            // Try to form a valid word with available tiles connecting to "L" at (10,7) downwards
            // Assuming rack might have B, R, M, P - try to form "LAP" if possible
            var letterA = rackTiles.FirstOrDefault(t => t == "A");
            var letterP = rackTiles.FirstOrDefault(t => t == "P");
            if (letterA != null && letterP != null)
            {
                move5.Tiles.Add(new PlacedTile { Letter = "A", X = 10, Y = 8 });
                move5.Tiles.Add(new PlacedTile { Letter = "P", X = 10, Y = 9 });
                TestContext.WriteLine($"Attempting move with tiles A at (10,8) and P at (10,9) to form 'LAP'");
            }
            else if (rackTiles.Count >= 2)
            {
                move5.Tiles.Add(new PlacedTile { Letter = rackTiles[0], X = 10, Y = 8 });
                move5.Tiles.Add(new PlacedTile { Letter = rackTiles[1], X = 10, Y = 9 });
                TestContext.WriteLine($"Attempting move with tiles {rackTiles[0]} at (10,8) and {rackTiles[1]} at (10,9)");
            }
            else if (rackTiles.Count >= 1)
            {
                move5.Tiles.Add(new PlacedTile { Letter = rackTiles[0], X = 10, Y = 8 });
                TestContext.WriteLine($"Attempting move with tile {rackTiles[0]} at (10,8)");
            }
            else
            {
                Assert.Fail("Not enough tiles in rack to make a move.");
            }
            
            stateJson = JsonSerializer.Serialize(updatedState4);
            string move5Json = JsonSerializer.Serialize(move5);
            bool valid5 = _logic.ValidateMove(stateJson, move5Json, out string? error5);
            if (!valid5)
            {
                TestContext.WriteLine($"Move 5 validation failed: {error5}");
            }
            Assert.IsTrue(valid5, $"Move 5 validation failed: {error5}");
            stateJson = _logic.ApplyMove(stateJson, move5Json, out error5);
            Assert.IsNull(error5, $"Move 5 application failed: {error5}");
            var finalState = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            Assert.AreEqual("player2", finalState.CurrentPlayer);

            // Check final scores (rough estimation since exact scoring might vary)
            var finalScores = _logic.CalculateScores(stateJson);
            Assert.That(finalScores["player1"], Is.GreaterThan(0));
            Assert.That(finalScores["player2"], Is.GreaterThan(0));
        }

        [Test]
        public void SimulateGame_InvalidMoveAfterSeveralMoves_ReturnsFalse()
        {
            // Initial state with two players
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new[] { "H", "E", "L", "L", "O", "A", "B" } },
                    new PlayerInfo { Id = "player2", Rack = new[] { "T", "E", "S", "T", "I", "N", "G" } }
                },
                CurrentPlayer = "player1",
                FirstMove = true,
                PlayerOrder = new List<string> { "player1", "player2" },
                PlayerScores = new Dictionary<string, int> { { "player1", 0 }, { "player2", 0 } },
                TileBag = new[] { "A", "R", "M", "P", "D", "C", "K", "Y", "W" }
            };

            string stateJson = JsonSerializer.Serialize(state);

            // Move 1: Player 1 plays "HELLO"
            var move1 = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "H", X = 7, Y = 7 },
                    new PlacedTile { Letter = "E", X = 8, Y = 7 },
                    new PlacedTile { Letter = "L", X = 9, Y = 7 },
                    new PlacedTile { Letter = "L", X = 10, Y = 7 },
                    new PlacedTile { Letter = "O", X = 11, Y = 7 }
                }
            };
            string move1Json = JsonSerializer.Serialize(move1);
            stateJson = _logic.ApplyMove(stateJson, move1Json, out _);

            // Move 2: Player 2 plays "TEST"
            var move2 = new ScrabbleMove
            {
                PlayerId = "player2",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "T", X = 8, Y = 6 },
                    new PlacedTile { Letter = "S", X = 8, Y = 8 },
                    new PlacedTile { Letter = "T", X = 8, Y = 9 }
                }
            };
            string move2Json = JsonSerializer.Serialize(move2);
            stateJson = _logic.ApplyMove(stateJson, move2Json, out _);

            // Move 3: Player 1 attempts an invalid move (disconnected from existing tiles)
            var move3 = new ScrabbleMove
            {
                PlayerId = "player1",
                Tiles = new List<PlacedTile>
                {
                    new PlacedTile { Letter = "A", X = 0, Y = 0 },
                    new PlacedTile { Letter = "B", X = 1, Y = 0 }
                }
            };
            string move3Json = JsonSerializer.Serialize(move3);
            bool valid3 = _logic.ValidateMove(stateJson, move3Json, out string? error3);
            Assert.IsFalse(valid3);
            Assert.IsNotNull(error3);
            Assert.That(error3, Does.Contain("connect"));
        }
    }
}
