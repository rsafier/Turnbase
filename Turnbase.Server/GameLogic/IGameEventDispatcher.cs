namespace Turnbase.Server.GameLogic
{
    public interface IGameEventDispatcher
    {
        string RoomId { get; set; }
        System.Collections.Concurrent.ConcurrentDictionary<string, string> ConnectedPlayers { get; set; }
        Task<bool> BroadcastAsync(string eventJson);
        Task<bool> SendToUserAsync(string userId, string eventJson);
        Task<bool> SaveGameStateAsync(string stateJson);
        Task<bool> LoadGameStateAsync(string stateJson);
    }
}
