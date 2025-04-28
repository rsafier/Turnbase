namespace Turnbase.Server.GameLogic
{
    public interface IGameInstance
    {
        Task<bool> StartAsync();
        Task<bool> StopAsync();
        Task ProcessPlayerEventAsync(string userId, string messageJson);
    }
}
