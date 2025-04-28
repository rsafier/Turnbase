using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Turnbase.Server.Models
{
    [Index(nameof(GameStateId))]
    public class PlayerMove
    {
        [Key]
        public int Id { get; set; }
        public int GameStateId { get; set; }
        public string UserId { get; set; }
        public string MoveJson { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
