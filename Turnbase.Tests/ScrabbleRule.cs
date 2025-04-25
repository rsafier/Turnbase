using System.Text.Json;
using Turnbase.Server.GameLogic;

namespace Turnbase.Rules
{
    

public class ScrabbleRule : IGameRule
    {
        private  readonly HashSet<string> Dictionary = LoadDictionary();

        private static HashSet<string> LoadDictionary()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "../../../../dictionary.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Dictionary file not found: {path}");
            var dictionary = File.ReadAllLines(path)
                .Select(line => line.Trim().ToUpperInvariant())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            return new HashSet<string>(dictionary);
        }

        // --- Data Structures ---
        private class ScrabbleState
        {
            public string[][] Board { get; set; } = new string[15][];
            public Dictionary<string, string[]> PlayerRacks { get; set; } = new();
            public Dictionary<string, int> PlayerScores { get; set; } = new();
            public string[] TileBag { get; set; } = new string[0];
            public string CurrentPlayer { get; set; } = "";
            public List<string> PlayerOrder { get; set; } = new();
            public bool FirstMove { get; set; } = true;
        }

        private class ScrabbleMove
        {
            public string PlayerId { get; set; } = "";
            public List<PlacedTile> Tiles { get; set; } = new();
        }
        private class PlacedTile
        {
            public int Row { get; set; }
            public int Col { get; set; }
            public string Letter { get; set; } = "";
        }

        // --- Scrabble Constants ---
        private static readonly Dictionary<string, int> TileScores = new()
        {
            ["A"] = 1, ["B"] = 3, ["C"] = 3, ["D"] = 2, ["E"] = 1, ["F"] = 4, ["G"] = 2, ["H"] = 4, ["I"] = 1, ["J"] = 8,
            ["K"] = 5, ["L"] = 1, ["M"] = 3, ["N"] = 1, ["O"] = 1, ["P"] = 3, ["Q"] = 10, ["R"] = 1, ["S"] = 1, ["T"] = 1,
            ["U"] = 1, ["V"] = 4, ["W"] = 4, ["X"] = 8, ["Y"] = 4, ["Z"] = 10
        };
        private static readonly int BoardSize = 15;

        // --- Simple Dictionary ---
        private static readonly (int, int) Center = (7, 7);

        // --- Helper: Find all words formed by a move ---
        private List<string> FindWords(string[][] board, List<PlacedTile> moveTiles)
        {
            var words = new List<string>();
            // Main word (along the move direction)
            bool isRow = moveTiles.All(t => t.Row == moveTiles[0].Row);
            bool isCol = moveTiles.All(t => t.Col == moveTiles[0].Col);
            if (!isRow && !isCol) return words;
            int fixedIdx = isRow ? moveTiles[0].Row : moveTiles[0].Col;
            int min = moveTiles.Min(t => isRow ? t.Col : t.Row);
            int max = moveTiles.Max(t => isRow ? t.Col : t.Row);
            // Extend in both directions
            int start = min;
            while (start > 0 && (isRow ? board[fixedIdx][start - 1] : board[start - 1][fixedIdx]) != null)
                start--;
            int end = max;
            while (end < BoardSize - 1 && (isRow ? board[fixedIdx][end + 1] : board[end + 1][fixedIdx]) != null)
                end++;
            var word = "";
            for (int i = start; i <= end; i++)
                word += isRow ? board[fixedIdx][i] : board[i][fixedIdx];
            if (word.Length > 1) words.Add(word);
            // Perpendicular words
            foreach (var tile in moveTiles)
            {
                int r = tile.Row, c = tile.Col;
                int s = isRow ? r : c;
                int perpStart = s, perpEnd = s;
                while (perpStart > 0 && (isRow ? board[perpStart - 1][c] : board[r][perpStart - 1]) != null)
                    perpStart--;
                while (perpEnd < BoardSize - 1 && (isRow ? board[perpEnd + 1][c] : board[r][perpEnd + 1]) != null)
                    perpEnd++;
                if (perpEnd > perpStart)
                {
                    var w = "";
                    for (int i = perpStart; i <= perpEnd; i++)
                        w += isRow ? board[i][c] : board[r][i];
                    if (w.Length > 1) words.Add(w);
                }
            }
            return words.Distinct().ToList();
        }

        // --- Helper: Check move connectivity ---
        private bool IsConnected(string[][] board, List<PlacedTile> moveTiles, bool firstMove)
        {
            if (firstMove)
            {
                // Must cover center
                return moveTiles.Any(t => t.Row == Center.Item1 && t.Col == Center.Item2);
            }
            // Must touch an existing tile
            foreach (var tile in moveTiles)
            {
                foreach (var (dr, dc) in new[] { (-1,0), (1,0), (0,-1), (0,1) })
                {
                    int nr = tile.Row + dr, nc = tile.Col + dc;
                    if (nr >= 0 && nr < BoardSize && nc >= 0 && nc < BoardSize && board[nr][nc] != null)
                        return true;
                }
            }
            return false;
        }

        public bool ValidateMove(string stateJson, string moveJson, out string? error)
        {
            error = null;
            var state = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            var move = JsonSerializer.Deserialize<ScrabbleMove>(moveJson);
            if (state == null || move == null)
            {
                error = "Invalid state or move JSON.";
                return false;
            }
            if (move.PlayerId != state.CurrentPlayer)
            {
                error = "Not this player's turn.";
                return false;
            }
            if (move.Tiles.Count == 0)
            {
                error = "No tiles placed.";
                return false;
            }
            var rack = state.PlayerRacks[move.PlayerId].ToList();
            foreach (var tile in move.Tiles)
            {
                if (!rack.Remove(tile.Letter))
                {
                    error = $"Player does not have tile '{tile.Letter}'.";
                    return false;
                }
                if (tile.Row < 0 || tile.Row >= BoardSize || tile.Col < 0 || tile.Col >= BoardSize)
                {
                    error = "Tile out of board bounds.";
                    return false;
                }
                if (state.Board[tile.Row][tile.Col] != null)
                {
                    error = "Cell already occupied.";
                    return false;
                }
            }
            bool sameRow = move.Tiles.All(t => t.Row == move.Tiles[0].Row);
            bool sameCol = move.Tiles.All(t => t.Col == move.Tiles[0].Col);
            if (!sameRow && !sameCol)
            {
                error = "Tiles must be in a straight line.";
                return false;
            }
            // Connectivity
            if (!IsConnected(state.Board, move.Tiles, state.FirstMove))
            {
                error = state.FirstMove ? "First move must cover center." : "Move must connect to existing tiles.";
                return false;
            }
            // Simulate board after move
            var tempBoard = state.Board.Select(row => row.ToArray()).ToArray();
            foreach (var tile in move.Tiles)
                tempBoard[tile.Row][tile.Col] = tile.Letter;
            var words = FindWords(tempBoard, move.Tiles);
            if (words.Count == 0)
            {
                error = "No valid words formed.";
                return false;
            }
            foreach (var w in words)
            {
                if (!Dictionary.Contains(w.ToUpper()))
                {
                    error = $"Word '{w}' not in dictionary.";
                    return false;
                }
            }
            return true;
        }

        public string ApplyMove(string stateJson, string moveJson)
        {
            var state = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            var move = JsonSerializer.Deserialize<ScrabbleMove>(moveJson);
            if (state == null || move == null) return stateJson;
            // Place tiles
            foreach (var tile in move.Tiles)
            {
                state.Board[tile.Row][tile.Col] = tile.Letter;
                state.PlayerRacks[move.PlayerId] = state.PlayerRacks[move.PlayerId].Where(l => l != tile.Letter).ToArray();
            }
            // Score
            var words = FindWords(state.Board, move.Tiles);
            int score = 0;
            foreach (var w in words)
                score += w.ToUpper().Sum(c => TileScores.TryGetValue(c.ToString(), out var s) ? s : 0);
            if (!state.PlayerScores.ContainsKey(move.PlayerId))
                state.PlayerScores[move.PlayerId] = 0;
            state.PlayerScores[move.PlayerId] += score;
            // Draw tiles from bag
            var rack = state.PlayerRacks[move.PlayerId].ToList();
            var needed = 7 - rack.Count;
            var draw = state.TileBag.Take(needed).ToArray();
            rack.AddRange(draw);
            state.PlayerRacks[move.PlayerId] = rack.ToArray();
            state.TileBag = state.TileBag.Skip(needed).ToArray();
            // Advance turn
            var idx = state.PlayerOrder.IndexOf(state.CurrentPlayer);
            state.CurrentPlayer = state.PlayerOrder[(idx + 1) % state.PlayerOrder.Count];
            state.FirstMove = false;
            return JsonSerializer.Serialize(state);
        }

        public int CalculateScore(string stateJson, string playerId)
        {
            var state = JsonSerializer.Deserialize<ScrabbleState>(stateJson);
            if (state == null || !state.PlayerScores.ContainsKey(playerId)) return 0;
            return state.PlayerScores[playerId];
        }

        public string[] GetBettableEvents()
        {
            return new[] { "win", "score_threshold" };
        }
    }
}
