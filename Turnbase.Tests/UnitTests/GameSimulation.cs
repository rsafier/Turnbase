using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Turnbase.Rules;

namespace Turnbase.Tests.UnitTests
{
    [TestFixture]
    public class GameSimulationTests
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

        // Initial tile distribution for a standard Scrabble game
        private static readonly string[] InitialTileBag = new string[]
        {
            "A", "A", "A", "A", "A", "A", "A", "A", "A",
            "B", "B",
            "C", "C",
            "D", "D", "D", "D",
            "E", "E", "E", "E", "E", "E", "E", "E", "E", "E", "E", "E",
            "F", "F",
            "G", "G", "G",
            "H", "H",
            "I", "I", "I", "I", "I", "I", "I", "I", "I",
            "J",
            "K",
            "L", "L", "L", "L",
            "M", "M",
            "N", "N", "N", "N", "N", "N",
            "O", "O", "O", "O", "O", "O", "O", "O",
            "P", "P",
            "Q",
            "R", "R", "R", "R", "R", "R",
            "S", "S", "S", "S",
            "T", "T", "T", "T", "T", "T",
            "U", "U", "U", "U",
            "V", "V",
            "W", "W",
            "X",
            "Y", "Y",
            "Z"
        };

        [SetUp]
        public void Setup()
        {
            _logic = new ScrabbleStateLogic();
            // Load dictionary words
            if (DictionaryWords.Count == 0)
            {
                string dictionaryPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../dictionary.csv");
                if (File.Exists(dictionaryPath))
                {
                    foreach (var line in File.ReadAllLines(dictionaryPath))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            DictionaryWords.Add(line.Trim().ToUpper());
                        }
                    }
                }
                else
                {
                    TestContext.WriteLine("Dictionary file not found, using empty dictionary.");
                }
            }
        }

        [Test]
        public void SimulateGameFor10Turns_WithRetries()
        {
            // Initialize game state with two players
            var state = new ScrabbleState
            {
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player1", Rack = new string[0] },
                    new PlayerInfo { Id = "player2", Rack = new string[0] }
                },
                CurrentPlayer = "player1",
                FirstMove = true,
                PlayerOrder = new List<string> { "player1", "player2" },
                PlayerScores = new Dictionary<string, int> { { "player1", 0 }, { "player2", 0 } },
                TileBag = InitialTileBag.ToArray()
            };

            // Distribute initial tiles to players
            state = DistributeInitialTiles(state);

            string stateJson = JsonSerializer.Serialize(state);
            int maxTurns = 10;
            int turnCount = 0;
            var failedCombinations = new HashSet<string>();

            while (turnCount < maxTurns)
            {
                turnCount++;
                var currentState = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
                string currentPlayer = currentState.CurrentPlayer;
                int retryCount = 0;
                bool moveSuccessful = false;

                while (retryCount < 100 && !moveSuccessful)
                {
                    retryCount++;
                    var move = GenerateMove(currentState, currentPlayer, failedCombinations);
                    string moveJson = JsonSerializer.Serialize(move);

                    bool isValid = _logic.ValidateMove(stateJson, moveJson, out string? error);
                    if (isValid)
                    {
                        stateJson = _logic.ApplyMove(stateJson, moveJson, out error);
                        moveSuccessful = true;
                        TestContext.WriteLine($"Turn {turnCount}: {currentPlayer} made a successful move.");
                        failedCombinations.Clear(); // Reset failed combinations on successful move
                        break;
                    }
                    else
                    {
                        TestContext.WriteLine($"Turn {turnCount}: {currentPlayer} retry {retryCount} failed: {error}");
                    }
                }

                if (!moveSuccessful)
                {
                    TestContext.WriteLine($"Turn {turnCount}: {currentPlayer} skipped turn after 100 retries.");
                    // Skip turn by advancing to the next player without applying a move
                    var updatedState = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
                    var idx = updatedState.PlayerOrder.IndexOf(updatedState.CurrentPlayer);
                    updatedState.CurrentPlayer = updatedState.PlayerOrder[(idx + 1) % updatedState.PlayerOrder.Count];
                    stateJson = JsonSerializer.Serialize(updatedState);
                }
            }

            // Output final scores
            var finalState = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            TestContext.WriteLine("Final Scores:");
            foreach (var player in finalState.PlayerOrder)
            {
                TestContext.WriteLine($"{player}: {finalState.PlayerScores[player]}");
            }

            Assert.Pass("Simulation completed for 10 turns.");
        }

        private ScrabbleState DistributeInitialTiles(ScrabbleState state)
        {
            foreach (var player in state.Players)
            {
                var rack = new List<string>();
                for (int i = 0; i < 7 && state.TileBag.Length > 0; i++)
                {
                    rack.Add(state.TileBag[0]);
                    state.TileBag = state.TileBag.Skip(1).ToArray();
                }
                player.Rack = rack.ToArray();
            }
            return state;
        }

        private static HashSet<string> DictionaryWords { get; set; } = new HashSet<string>();

        private ScrabbleMove GenerateMove(ScrabbleState state, string playerId, HashSet<string> failedCombinations)
        {
            var player = state.Players.First(p => p.Id == playerId);
            var rack = player.Rack.ToList();
            var move = new ScrabbleMove { PlayerId = playerId, Tiles = new List<PlacedTile>() };
            var random = new Random();

            if (state.FirstMove)
            {
                // First move must cover the center (7,7)
                if (rack.Count >= 2)
                {
                    var possibleWords = FindPossibleWords(rack, 2, 5);
                    if (possibleWords.Count > 0)
                    {
                        var word = possibleWords[random.Next(possibleWords.Count)];
                        for (int i = 0; i < word.Length; i++)
                        {
                            move.Tiles.Add(new PlacedTile { Letter = word[i].ToString(), X = 7 + i, Y = 7 });
                        }
                    }
                    else
                    {
                        // Fallback if no valid word found
                        move.Tiles.Add(new PlacedTile { Letter = rack[0], X = 7, Y = 7 });
                        move.Tiles.Add(new PlacedTile { Letter = rack[1], X = 8, Y = 7 });
                    }
                }
                else if (rack.Count == 1)
                {
                    move.Tiles.Add(new PlacedTile { Letter = rack[0], X = 7, Y = 7 });
                }
            }
            else
            {
                // Find a position to connect to existing tiles
                var anchorPoints = FindAnchorPoints(state.Board);
                if (anchorPoints.Count > 0 && rack.Count > 0)
                {
                    bool placed = false;
                    // Shuffle anchor points to try different positions
                    anchorPoints = anchorPoints.OrderBy(_ => random.Next()).ToList();
                    foreach (var anchor in anchorPoints)
                    {
                        // Try all directions: right, left, down, up
                        var directions = new List<(int dx, int dy, string direction)> 
                        {
                            (1, 0, "right"), (-1, 0, "left"), (0, 1, "down"), (0, -1, "up")
                        };
                        directions = directions.OrderBy(_ => random.Next()).ToList();
                        
                        foreach (var (dx, dy, direction) in directions)
                        {
                            int newX = anchor.X + dx;
                            int newY = anchor.Y + dy;
                            if (newX >= 0 && newX < 15 && newY >= 0 && newY < 15 && state.Board[newY][newX] == null)
                            {
                                // Try to build a word in this direction
                                var existingLetters = new List<(char letter, int pos)>();
                                int pos = 0;
                                if (direction == "right" || direction == "left")
                                {
                                    int startX = direction == "right" ? anchor.X : anchor.X - 1;
                                    int endX = direction == "right" ? 14 : 0;
                                    for (int x = startX; direction == "right" ? x <= endX : x >= endX; x += dx)
                                    {
                                        if (x >= 0 && x < 15 && state.Board[anchor.Y][x] != null)
                                        {
                                            existingLetters.Add((state.Board[anchor.Y][x][0], pos));
                                        }
                                        pos++;
                                    }
                                }
                                else // up or down
                                {
                                    int startY = direction == "down" ? anchor.Y : anchor.Y - 1;
                                    int endY = direction == "down" ? 14 : 0;
                                    for (int y = startY; direction == "down" ? y <= endY : y >= endY; y += dy)
                                    {
                                        if (y >= 0 && y < 15 && state.Board[y][anchor.X] != null)
                                        {
                                            existingLetters.Add((state.Board[y][anchor.X][0], pos));
                                        }
                                        pos++;
                                    }
                                }
                                
                                // Try to form a word using rack and existing letters
                                var possibleWords = FindPossibleWords(rack, 2, 5, existingLetters);
                                if (possibleWords.Count > 0)
                                {
                                    var word = possibleWords[random.Next(possibleWords.Count)];
                                    int startPos = 0;
                                    if (direction == "right")
                                    {
                                        startPos = anchor.X + 1 - word.Length;
                                        if (startPos < 0) startPos = 0;
                                        for (int i = 0; i < word.Length; i++)
                                        {
                                            int tx = startPos + i;
                                            if (tx >= 0 && tx < 15 && state.Board[anchor.Y][tx] == null)
                                            {
                                                move.Tiles.Add(new PlacedTile { Letter = word[i].ToString(), X = tx, Y = anchor.Y });
                                            }
                                        }
                                    }
                                    else if (direction == "left")
                                    {
                                        startPos = anchor.X - 1;
                                        for (int i = 0; i < word.Length; i++)
                                        {
                                            int tx = startPos - i;
                                            if (tx >= 0 && tx < 15 && state.Board[anchor.Y][tx] == null)
                                            {
                                                move.Tiles.Add(new PlacedTile { Letter = word[word.Length - 1 - i].ToString(), X = tx, Y = anchor.Y });
                                            }
                                        }
                                    }
                                    else if (direction == "down")
                                    {
                                        startPos = anchor.Y + 1 - word.Length;
                                        if (startPos < 0) startPos = 0;
                                        for (int i = 0; i < word.Length; i++)
                                        {
                                            int ty = startPos + i;
                                            if (ty >= 0 && ty < 15 && state.Board[ty][anchor.X] == null)
                                            {
                                                move.Tiles.Add(new PlacedTile { Letter = word[i].ToString(), X = anchor.X, Y = ty });
                                            }
                                        }
                                    }
                                    else if (direction == "up")
                                    {
                                        startPos = anchor.Y - 1;
                                        for (int i = 0; i < word.Length; i++)
                                        {
                                            int ty = startPos - i;
                                            if (ty >= 0 && ty < 15 && state.Board[ty][anchor.X] == null)
                                            {
                                                move.Tiles.Add(new PlacedTile { Letter = word[word.Length - 1 - i].ToString(), X = anchor.X, Y = ty });
                                            }
                                        }
                                    }
                                    placed = true;
                                    break;
                                }
                            }
                        }
                        if (placed) break;
                    }
                    // If no placement found, fall back to first anchor
                    if (!placed && rack.Count > 0)
                    {
                        var anchor = anchorPoints[0];
                        if (anchor.X + 1 < 15 && state.Board[anchor.Y][anchor.X + 1] == null)
                        {
                            move.Tiles.Add(new PlacedTile { Letter = rack[0], X = anchor.X + 1, Y = anchor.Y });
                        }
                        else if (anchor.Y + 1 < 15 && state.Board[anchor.Y + 1][anchor.X] == null)
                        {
                            move.Tiles.Add(new PlacedTile { Letter = rack[0], X = anchor.X, Y = anchor.Y + 1 });
                        }
                    }
                }
                else if (rack.Count > 0)
                {
                    // If no anchors (shouldn't happen after first move), place near center
                    int startX = 7, startY = 7;
                    while (startY < 15 && state.Board[startY][startX] != null)
                    {
                        startY++;
                    }
                    if (startY < 15)
                    {
                        move.Tiles.Add(new PlacedTile { Letter = rack[0], X = startX, Y = startY });
                    }
                }
            }

            // Record the attempted combination to avoid retrying it
            string combination = string.Join("", move.Tiles.Select(t => t.Letter));
            failedCombinations.Add(combination);

            return move;
        }

        private List<string> FindPossibleWords(List<string> rack, int minLength, int maxLength, List<(char letter, int pos)> existingLetters = null)
        {
            var possibleWords = new List<string>();
            var rackLetters = rack.Select(r => r[0]).ToList();
            var usedPositions = existingLetters?.Select(e => e.pos).ToHashSet() ?? new HashSet<int>();
            
            // Generate all possible combinations from rack
            for (int len = Math.Min(maxLength, rackLetters.Count); len >= minLength; len--)
            {
                var combinations = GetCombinations(rackLetters, len);
                foreach (var combo in combinations)
                {
                    string word = new string(combo.ToArray());
                    if (DictionaryWords.Contains(word))
                    {
                        bool matchesExisting = true;
                        if (existingLetters != null && existingLetters.Count > 0)
                        {
                            foreach (var (letter, pos) in existingLetters)
                            {
                                if (pos < word.Length && word[pos] != letter)
                                {
                                    matchesExisting = false;
                                    break;
                                }
                            }
                        }
                        if (matchesExisting)
                        {
                            possibleWords.Add(word);
                        }
                    }
                }
                if (possibleWords.Count > 0) break; // Return early if we found some words
            }
            
            return possibleWords;
        }

        private IEnumerable<List<char>> GetCombinations(List<char> letters, int length)
        {
            if (length == 0) return new List<List<char>> { new List<char>() };
            if (letters.Count == 0) return new List<List<char>>();
            
            var result = new List<List<char>>();
            for (int i = 0; i < letters.Count; i++)
            {
                var letter = letters[i];
                var remaining = new List<char>(letters);
                remaining.RemoveAt(i);
                var subCombinations = GetCombinations(remaining, length - 1);
                foreach (var subCombo in subCombinations)
                {
                    subCombo.Insert(0, letter);
                    result.Add(subCombo);
                }
            }
            return result;
        }

        private List<PlacedTile> FindAnchorPoints(string[][] board)
        {
            var anchors = new List<PlacedTile>();
            for (int y = 0; y < 15; y++)
            {
                for (int x = 0; x < 15; x++)
                {
                    if (board[y][x] != null)
                    {
                        anchors.Add(new PlacedTile { X = x, Y = y, Letter = board[y][x] });
                    }
                }
            }
            return anchors;
        }
    }
}
