using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Turnbase.Server.GameLogic
{

    /// <summary>
    /// Provide a fixture for IGameInstance to send events to players and persist state, will abstract SignalR from the Game.
    /// </summary>
    public interface IGameEventDispatcher
    {
        /// <summary>
        /// Unique Room Id - Group in SignalR Hub terms
        /// </summary>
        public string RoomId { get; set; }
        
        /// <summary>
        /// Players by userId, and player state json
        /// </summary>
        ConcurrentDictionary<string,string> ConnectedPlayers { get; set; }
        
        /// <summary>
        /// Broadcasts an event to all clients connected to the specified room.
        /// </summary>
        /// <param name="eventJson">The event payload serialized as JSON.</param>
        Task<bool> BroadcastAsync(string eventJson);

        /// <summary>
        /// Sends an event to a specific client.
        /// </summary>
        /// <param name="userId">The ID of the client.</param>
        /// <param name="eventJson">The event payload serialized as JSON.</param>
        Task<bool> SendToUserAsync(string userId,string eventJson);
        
        /// <summary>
        /// Save game state
        Task<bool> SaveGameStateAsync(string stateJson);
        
        /// <summary>
        /// Load game state
        Task<bool> LoadGameStateAsync(string stateJson);
    }

    /// <summary>
    /// Represents a game instance that handles game logic, player interactions, and event dispatching.
    /// It will use the EventDispatcher to 
    /// </summary>
    public interface IGameInstance
    {
        /// <summary>
        /// The unique identifier for this game room instance 
        /// </summary>
        public string RoomId { get; }
        
        public long TurnCount { get; }

        /// <summary>
        /// Sets the event dispatcher which is used to discover and communicate back to the room / players
        /// </summary>
        public IGameEventDispatcher EventDispatcher { get; set; }

        /// <summary>
        /// Starts or initializes the game instance.
        /// </summary>
        /// <returns>Indicating whether the game was successfully resumed.</returns>
        public Task<bool> StartAsync();
 
        /// <summary>
        /// Stop the game instance, should send out final game state.
        /// </summary>
        /// <returns>Indicating whether the game was successfully resumed.</returns>
        public Task<bool> StopAsync();

        /// <summary>
        /// Processes a move or command from a given player.
        /// </summary>
        /// <param name="userId">The ID of the player making the move.</param>
        /// <param name="messageJson">The move or command in JSON format.</param>
        public Task ProcessPlayerEventAsync(string userId, string messageJson);
    }
}


namespace Turnbase.Server.Data
{
    public class GameContext : DbContext
    {
        
        public GameContext(DbContextOptions<GameContext> options) : base(options) { }
        public GameContext() : base() { }

        public DbSet<Game> Games { get; set; }
        public DbSet<GameState> GameStates { get; set; }
        public DbSet<PlayerMove> PlayerMoves { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=turnbase.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // GameState.GameId -> Game.Id
            modelBuilder.Entity<GameState>()
                .HasOne<Game>()
                .WithMany()
                .HasForeignKey(gs => gs.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            // PlayerMove.GameStateId -> GameState.Id
            modelBuilder.Entity<PlayerMove>()
                .HasOne<GameState>()
                .WithMany()
                .HasForeignKey(pm => pm.GameStateId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public override int SaveChanges()
        {
            SetTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void SetTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                var entity = entry.Entity;
                var entityType = entity.GetType();

                var now = DateTime.UtcNow;

                // Set CreatedDate if exists and Added
                if (entry.State == EntityState.Added)
                {
                    var createdProp = entityType.GetProperty("CreatedDate");
                    if (createdProp != null && createdProp.CanWrite)
                    {
                        var value = createdProp.GetValue(entity);
                        if (value == null || (DateTime)value == default)
                            createdProp.SetValue(entity, now);
                    }
                }

                // Set UpdatedDate if exists
                var updatedProp = entityType.GetProperty("UpdatedDate");
                if (updatedProp != null && updatedProp.CanWrite)
                {
                    updatedProp.SetValue(entity, now);
                }
            }
        }
    }
}

namespace Turnbase.Server.Models
{
   [Index(nameof(CreatedDate))]
   [Index(nameof(GameName))]
   [Index(nameof(GameTypeName))]
    public class Game
    {
        [Key]
        public ulong Id { get; set; }
        public required string GameName { get; set; }
        public required string GameTypeName { get; set; } 
        public DateTime CreatedDate { get; set; } 
        public DateTime? CompletedDate { get; set; }

        public ICollection<GameState>? GameStates { get; set; }
    }

    [Index(nameof(CreatedDate))]
    [Index(nameof(GameId))]
    public class GameState
    {
        [Key]
        public ulong Id { get; set; }
        public ulong GameId { get; set; }
        public required string StateJson { get; set; }
        public DateTime CreatedDate { get; set; }  
        public string? Signature { get; set; }

        public Game? Game { get; set; }
        public ICollection<PlayerMove>? PlayerMoves { get; set; }
    }

    [Index(nameof(GameStateId))] 
    public class PlayerMove
    {
        [Key]
        public ulong Id { get; set; }
        public required ulong GameStateId { get; set; }
        public DateTime CreatedDate { get; set; }  
        public required string PlayerId { get; set; } 
        public required string MoveJson { get; set; }
        public string? Signature { get; set; }

        public GameState? GameState { get; set; }
    }
}

namespace Turnbase.Server.Models
{
    [Index(nameof(CreatedDate))]
    [Index(nameof(GameName))]
    [Index(nameof(GameTypeName))]
    public class Game
    {
        [Key]
        public ulong Id { get; set; }
        public required string GameName { get; set; }
        public required string GameTypeName { get; set; } 
        public DateTime CreatedDate { get; set; } 
        public DateTime? CompletedDate { get; set; }

        public ICollection<GameState>? GameStates { get; set; }
    }

    [Index(nameof(CreatedDate))]
    [Index(nameof(GameId))]
    public class GameState
    {
        [Key]
        public ulong Id { get; set; }
        public ulong GameId { get; set; }
        public required string StateJson { get; set; }
        public DateTime CreatedDate { get; set; }  
        public string? Signature { get; set; }

        public Game? Game { get; set; }
        public ICollection<PlayerMove>? PlayerMoves { get; set; }
    }

    [Index(nameof(GameStateId))] 
    public class PlayerMove
    {
        [Key]
        public ulong Id { get; set; }
        public required ulong GameStateId { get; set; }
        public DateTime CreatedDate { get; set; }  
        public required string PlayerId { get; set; } 
        public required string MoveJson { get; set; }
        public string? Signature { get; set; }

        public GameState? GameState { get; set; }
    }
}

