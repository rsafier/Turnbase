using Microsoft.EntityFrameworkCore;
using Turnbase.Server.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Turnbase.Server.Data
{
    public class GameContext : DbContext
    {
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

namespace Turnbase.Server.GameLogic
{
    public interface IGameRule
    {
        bool ValidateMove(string stateJson, string moveJson, out string? error);
        string ApplyMove(string stateJson, string moveJson);
        int CalculateScore(string stateJson, string playerId); 
    }
}


namespace Turnbase.Server.Services
{
    public class FairnessService
    {
        public (string Hash, string Seed) GenerateCommitment()
        {
            var seed = Guid.NewGuid().ToString();
            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
            return (hash, seed);
        }
    }
}