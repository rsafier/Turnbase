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

        public CoinFlipGame(IGameEventDispatcher eventDispatcher) : base(eventDispatcher)
        {
        }

        public override async Task<bool> StartAsync()
        {
            await base.StartAsync();
            _isGameActive = true;
            
            // Notify players that the game has started
            var startEvent = new { EventType = "GameStarted", GameType = "CoinFlip" };
            await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(startEvent));
            
            return true;
        }

        public override async Task<bool> StopAsync()
        {
            _isGameActive = false;
            var endEvent = new { EventType = "GameEnded", Winner = _winner };
            await EventDispatcher.BroadcastAsync(JsonConvert.SerializeObject(endEvent));
            return await base.StopAsync();
        }

        public override async Task ProcessPlayerEventAsync(string userId, string messageJson)
        {
            if (!_isGameActive) return;

            try
            {
                dynamic? move = JsonConvert.DeserializeObject(messageJson);
                string? action = move?.Action?.ToString();

                if (action == "FlipCoin")
                {
                    if (_currentPlayer != userId && !string.IsNullOrEmpty(_currentPlayer))
                    {
                        await EventDispatcher.SendToUserAsync(userId, JsonConvert.SerializeObject(
                            new { EventType = "Error", Message = "Not your turn" }));
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
                    await StopAsync();
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
}
