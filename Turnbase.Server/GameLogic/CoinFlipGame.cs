using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Turnbase.Server.GameLogic
{
    public class CoinFlipGame : BaseGameInstance
    {
        private string _currentPlayer = string.Empty;
        private string _winner = string.Empty;
        private bool _isGameActive = false;

        public CoinFlipGame(IGameEventDispatcher eventDispatcher, ILogger<BaseGameInstance> logger) : base(eventDispatcher, logger)
        {
        }

        public override async Task<bool> StartAsync()
        {
            try
            {
                await base.StartAsync();
                _isGameActive = true;
                
                // Notify players that the game has started
                var startEvent = new { EventType = "GameStarted", GameType = "CoinFlip" };
                await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(startEvent));
                
                _logger.LogInformation("CoinFlip game started in room {RoomId}", RoomId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting CoinFlip game in room {RoomId}", RoomId);
                throw;
            }
        }

        public override async Task<bool> StopAsync()
        {
            try
            {
                _isGameActive = false;
                var endEvent = new { EventType = "GameEnded", Winner = _winner };
                await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(endEvent));
                
                // Save the final game state
                var finalState = new { GameType = "CoinFlip", Winner = _winner, IsActive = _isGameActive, TurnCount = TurnCount };
                await EventDispatcher.SaveGameStateAsync(JsonConvert.SerializeObject(finalState));
                
                _logger.LogInformation("CoinFlip game stopped in room {RoomId}. Winner: {Winner}", RoomId, _winner);
                return await base.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping CoinFlip game in room {RoomId}", RoomId);
                throw;
            }
        }

        public override async Task ProcessPlayerEventAsync(string userId, string messageJson)
        {
            if (!_isGameActive)
            {
                _logger.LogWarning("User {UserId} attempted to process event in inactive CoinFlip game in room {RoomId}", userId, RoomId);
                return;
            }

            try
            {
                _logger.LogInformation("Processing event for user {UserId} in room {RoomId}", userId, RoomId);
                dynamic? move = JsonConvert.DeserializeObject(messageJson);
                string? action = move?.Action?.ToString();

                if (action == "FlipCoin")
                {
                    if (_currentPlayer != userId && !string.IsNullOrEmpty(_currentPlayer))
                    {
                        await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                            new { EventType = "Error", Message = "Not your turn" }));
                        _logger.LogWarning("User {UserId} attempted coin flip out of turn in room {RoomId}", userId, RoomId);
                        return;
                    }

                    TurnCount++;
                    _currentPlayer = userId;

                    // Simulate coin flip
                    Random rand = new Random();
                    bool isHeads = rand.Next(2) == 0;
                    _winner = isHeads ? userId : GetOpponent(userId);

                    var flipResult = new 
                    { 
                        EventType = "CoinFlipResult", 
                        IsHeads = isHeads, 
                        Winner = _winner,
                        TurnCount = TurnCount
                    };
                    
                    await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(flipResult));
                    _logger.LogInformation("Coin flip by {UserId} in room {RoomId}. Result: {IsHeads}, Winner: {Winner}", userId, RoomId, isHeads, _winner);
                    await StopAsync();
                }
            }
            catch (Exception ex)
            {
                await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                    new { EventType = "Error", Message = ex.Message }));
                _logger.LogError(ex, "Error processing event for user {UserId} in room {RoomId}", userId, RoomId);
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
}
