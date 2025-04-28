using System.Threading.Tasks;

namespace Turnbase.Server.GameLogic
{
    public abstract class BaseGameInstance : IGameInstance
    {
        public string RoomId { get; set; } = string.Empty;
        public long TurnCount { get; protected set; } = 0;
        public IGameEventDispatcher EventDispatcher { get; set; } = null!;
        protected readonly ILogger<BaseGameInstance> _logger;

        public BaseGameInstance(IGameEventDispatcher eventDispatcher, ILogger<BaseGameInstance> logger)
        {
            EventDispatcher = eventDispatcher;
            _logger = logger;
        }

        public virtual async Task<bool> StartAsync()
        {
            try
            {
                TurnCount = 0;
                _logger.LogInformation("Game started in room {RoomId}", RoomId);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting game in room {RoomId}", RoomId);
                throw;
            }
        }

        public virtual async Task<bool> StopAsync()
        {
            try
            {
                _logger.LogInformation("Game stopped in room {RoomId}", RoomId);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping game in room {RoomId}", RoomId);
                throw;
            }
        }

        public abstract Task ProcessPlayerEventAsync(string userId, string messageJson);
    }
}
