namespace Turnbase.Server.GameLogic
{
    public interface IGameInstance
    {
        string RoomId { get; set; }
        IGameEventDispatcher EventDispatcher { get; }
        Task<bool> StartAsync();
        Task<bool> StopAsync();
        Task ProcessPlayerEventAsync(string userId, string messageJson);
    }
}
