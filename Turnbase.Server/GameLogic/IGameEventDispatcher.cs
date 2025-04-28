namespace Turnbase.Server.GameLogic
{
    public interface IGameEventDispatcher
    {
        Task<bool> BroadcastAsync(string eventJson);
        Task<bool> SendToUserAsync(string userId, string eventJson);
        Task<bool> SaveGameStateAsync(string stateJson);
        Task<bool> LoadGameStateAsync(string stateJson);
    }
}
