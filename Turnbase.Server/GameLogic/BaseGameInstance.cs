using System.Threading.Tasks;

namespace Turnbase.Server.GameLogic
{
    public abstract class BaseGameInstance : IGameInstance
    {
        public string RoomId { get; set; } = string.Empty;
        public long TurnCount { get; protected set; } = 0;
        public IGameEventDispatcher EventDispatcher { get; set; } = null!;

        public BaseGameInstance(IGameEventDispatcher eventDispatcher)
        {
            EventDispatcher = eventDispatcher;
        }

        public virtual async Task<bool> StartAsync()
        {
            TurnCount = 0;
            return await Task.FromResult(true);
        }

        public virtual async Task<bool> StopAsync()
        {
            // Implementation for stopping the game
            return await Task.FromResult(true);
        }

        public abstract Task ProcessPlayerEventAsync(string userId, string messageJson);
    }
}
