using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Turnbase.Server.GameLogic
{
    public class BattleshipGame : BaseGameInstance
    {
        private bool _isGameActive = false;
        private string _currentPlayer = string.Empty;
        private string _winner = string.Empty;
        private Dictionary<string, PlayerBoard> _playerBoards = new Dictionary<string, PlayerBoard>();

        private readonly int _boardSize = 10;
        private readonly Dictionary<string, int> _shipSizes = new Dictionary<string, int>
        {
            { "Carrier", 5 },
            { "Battleship", 4 },
            { "Cruiser", 3 },
            { "Submarine", 3 },
            { "Destroyer", 2 }
        };

        public BattleshipGame(IGameEventDispatcher eventDispatcher) : base(eventDispatcher)
        {
        }

        public override async Task<bool> StartAsync()
        {
            await base.StartAsync();
            _isGameActive = true;
            _playerBoards.Clear();

            // Initialize boards for connected players, if any
            if (EventDispatcher.ConnectedPlayers != null)
            {
                foreach (var playerId in EventDispatcher.ConnectedPlayers.Keys)
                {
                    _playerBoards[playerId] = new PlayerBoard(_boardSize, _shipSizes.Keys.ToList());
                }
            }

            // Notify players that the game has started
            var startEvent = new { EventType = "GameStarted", GameType = "Battleship", BoardSize = _boardSize, Ships = _shipSizes.Keys };
            await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(startEvent));

            return true;
        }

        public override async Task<bool> StopAsync()
        {
            _isGameActive = false;
            var endEvent = new { EventType = "GameEnded", Winner = _winner };
            await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(endEvent));

            // Save the final game state
            var finalState = new
            {
                GameType = "Battleship",
                Winner = _winner,
                IsActive = _isGameActive,
                TurnCount = TurnCount,
                PlayerBoards = _playerBoards.Select(pb => new { PlayerId = pb.Key, Board = pb.Value.Serialize() })
            };
            await EventDispatcher.SaveGameStateAsync(JsonConvert.SerializeObject(finalState));

            return await base.StopAsync();
        }

        public override async Task ProcessPlayerEventAsync(string userId, string messageJson)
        {
            if (!_isGameActive) return;

            try
            {
                dynamic? move = JsonConvert.DeserializeObject(messageJson);
                string? action = move?.Action?.ToString();

                if (action == "PlaceShip")
                {
                    string? shipType = move?.ShipType?.ToString();
                    int startX = move?.StartX != null ? Convert.ToInt32(move.StartX) : 0;
                    int startY = move?.StartY != null ? Convert.ToInt32(move.StartY) : 0;
                    bool isHorizontal = move?.IsHorizontal != null ? Convert.ToBoolean(move.IsHorizontal) : false;

                    if (!_playerBoards.ContainsKey(userId))
                    {
                        await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                            new { EventType = "Error", Message = "Player not found in game" }));
                        return;
                    }

                    var board = _playerBoards[userId];
                    if (board.PlaceShip(shipType, startX, startY, isHorizontal))
                    {
                        var shipPlacedEvent = new { EventType = "ShipPlaced", PlayerId = userId, ShipType = shipType };
                        await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(shipPlacedEvent));

                        // Check if all ships are placed to start attack phase
                        if (board.AllShipsPlaced())
                        {
                            var readyEvent = new { EventType = "PlayerReady", PlayerId = userId };
                            await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(readyEvent));

                            // If all players are ready, start the attack phase
                            if (_playerBoards.Values.All(b => b.AllShipsPlaced()))
                            {
                                _currentPlayer = EventDispatcher.ConnectedPlayers.Keys.First();
                                var attackPhaseEvent = new { EventType = "AttackPhaseStarted", FirstTurn = _currentPlayer };
                                await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(attackPhaseEvent));
                            }
                        }
                    }
                    else
                    {
                        await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                            new { EventType = "Error", Message = "Invalid ship placement" }));
                    }
                }
                else if (action == "Attack")
                {
                    if (_currentPlayer != userId && !string.IsNullOrEmpty(_currentPlayer))
                    {
                        await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                            new { EventType = "Error", Message = "Not your turn" }));
                        return;
                    }

                    int x = move?.X != null ? Convert.ToInt32(move.X) : -1;
                    int y = move?.Y != null ? Convert.ToInt32(move.Y) : -1;
                    string opponentId = GetOpponent(userId);

                    if (string.IsNullOrEmpty(opponentId) || !_playerBoards.ContainsKey(opponentId))
                    {
                        await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                            new { EventType = "Error", Message = "No opponent found" }));
                        return;
                    }

                    TurnCount++;
                    var opponentBoard = _playerBoards[opponentId];
                    bool isHit = opponentBoard.Attack(x, y);
                    string sunkShip = opponentBoard.GetSunkShip(x, y);

                    var attackResult = new
                    {
                        EventType = "AttackResult",
                        AttackerId = userId,
                        DefenderId = opponentId,
                        X = x,
                        Y = y,
                        IsHit = isHit,
                        SunkShip = sunkShip ?? string.Empty,
                        TurnCount = TurnCount
                    };
                    await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(attackResult));

                    // Switch turns
                    _currentPlayer = opponentId;

                    // Check for win condition
                    if (opponentBoard.AllShipsSunk())
                    {
                        _winner = userId;
                        await StopAsync();
                    }
                    else
                    {
                        var turnEvent = new { EventType = "TurnChanged", CurrentPlayer = _currentPlayer };
                        await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(turnEvent));
                    }
                }
                else
                {
                    await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                        new { EventType = "Error", Message = "Invalid action" }));
                }
            }
            catch (Exception ex)
            {
                await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                    new { EventType = "Error", Message = ex.Message }));
            }
        }

        private string GetOpponent(string userId)
        {
            foreach (var player in EventDispatcher.ConnectedPlayers.Keys)
            {
                if (player != userId)
                    return player;
            }
            return string.Empty;
        }
    }

    public class PlayerBoard
    {
        private readonly int _size;
        private readonly List<string> _shipTypes;
        private readonly Dictionary<string, int> _shipSizes;
        private readonly char[,] _board; // ' ' = empty, 'S' = ship, 'H' = hit, 'M' = miss
        private readonly Dictionary<string, List<(int X, int Y)>> _shipPositions;
        private readonly Dictionary<string, int> _shipHits;

        public PlayerBoard(int size, List<string> shipTypes, Dictionary<string, int>? shipSizes = null)
        {
            _size = size;
            _shipTypes = shipTypes;
            _shipSizes = shipSizes ?? new Dictionary<string, int>
            {
                { "Carrier", 5 },
                { "Battleship", 4 },
                { "Cruiser", 3 },
                { "Submarine", 3 },
                { "Destroyer", 2 }
            };
            _board = new char[size, size];
            _shipPositions = new Dictionary<string, List<(int X, int Y)>>();
            _shipHits = new Dictionary<string, int>();

            // Initialize board with empty cells
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    _board[i, j] = ' ';
                }
            }
        }

        public bool PlaceShip(string? shipType, int startX, int startY, bool isHorizontal)
        {
            if (string.IsNullOrEmpty(shipType) || !_shipTypes.Contains(shipType) || _shipPositions.ContainsKey(shipType))
                return false;

            int length = _shipSizes[shipType];
            List<(int X, int Y)> positions = new List<(int X, int Y)>();

            for (int i = 0; i < length; i++)
            {
                int x = isHorizontal ? startX + i : startX;
                int y = isHorizontal ? startY : startY + i;

                if (x >= _size || y >= _size || _board[x, y] != ' ')
                    return false;

                positions.Add((x, y));
            }

            // Place the ship
            foreach (var pos in positions)
            {
                _board[pos.X, pos.Y] = 'S';
            }
            _shipPositions[shipType] = positions;
            _shipHits[shipType] = 0;

            return true;
        }

        public bool Attack(int x, int y)
        {
            if (x < 0 || x >= _size || y < 0 || y >= _size || _board[x, y] == 'H' || _board[x, y] == 'M')
                return false;

            bool isHit = _board[x, y] == 'S';
            _board[x, y] = isHit ? 'H' : 'M';

            if (isHit)
            {
                // Record hit on ship
                foreach (var ship in _shipPositions)
                {
                    if (ship.Value.Any(p => p.X == x && p.Y == y))
                    {
                        _shipHits[ship.Key]++;
                        break;
                    }
                }
            }

            return isHit;
        }

        public string GetSunkShip(int x, int y)
        {
            foreach (var ship in _shipPositions)
            {
                if (ship.Value.Any(p => p.X == x && p.Y == y))
                {
                    if (_shipHits.TryGetValue(ship.Key, out int hits) && _shipSizes.TryGetValue(ship.Key, out int size))
                    {
                        if (hits == size)
                        {
                            return ship.Key;
                        }
                    }
                }
            }
            return string.Empty;
        }

        public bool AllShipsPlaced()
        {
            return _shipPositions.Count == _shipTypes.Count;
        }

        public bool AllShipsSunk()
        {
            return _shipPositions.Keys.All(ship => _shipHits[ship] == _shipSizes[ship]);
        }

        public string Serialize()
        {
            // Convert board to a serializable format if needed
            return JsonConvert.SerializeObject(new
            {
                Size = _size,
                ShipPositions = _shipPositions,
                ShipHits = _shipHits
            });
        }
    }
}
