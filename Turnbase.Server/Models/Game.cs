using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Turnbase.Server.Models
{
    [Index(nameof(CreatedDate))]
    public class Game
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}
