using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Turnbase.Server.Models
{
    [Index(nameof(CreatedDate))]
    [Index(nameof(GameId))]
    public class GameState
    {
        [Key]
        public int Id { get; set; }
        public int GameId { get; set; }
        public string StateJson { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
