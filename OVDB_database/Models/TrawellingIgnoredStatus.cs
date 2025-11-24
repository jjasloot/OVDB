using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models
{
    [Index(nameof(UserId), nameof(TrawellingStatusId), IsUnique = true)]
    public class TrawellingIgnoredStatus
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int TrawellingStatusId { get; set; }
        
        public DateTime IgnoredAt { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; }
    }
}