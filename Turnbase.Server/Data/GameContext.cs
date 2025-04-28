using Microsoft.EntityFrameworkCore;
using Turnbase.Server.Models;

namespace Turnbase.Server.Data
{
    public class GameContext : DbContext
    {
        public DbSet<Game> Games { get; set; }
        public DbSet<GameState> GameStates { get; set; }
        public DbSet<PlayerMove> PlayerMoves { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite("Data Source=game.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure indexes if needed
            modelBuilder.Entity<Game>()
                .HasIndex(g => g.CreatedDate);

            modelBuilder.Entity<GameState>()
                .HasIndex(gs => gs.CreatedDate)
                .HasIndex(gs => gs.GameId);

            modelBuilder.Entity<PlayerMove>()
                .HasIndex(pm => pm.GameStateId);
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
                .Where(e => e.Entity is Game || e.Entity is GameState || e.Entity is PlayerMove)
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity is Game game)
                        game.CreatedDate = DateTime.UtcNow;
                    else if (entry.Entity is GameState gameState)
                        gameState.CreatedDate = DateTime.UtcNow;
                    else if (entry.Entity is PlayerMove playerMove)
                        playerMove.CreatedDate = DateTime.UtcNow;
                }
            }
        }
    }
}
