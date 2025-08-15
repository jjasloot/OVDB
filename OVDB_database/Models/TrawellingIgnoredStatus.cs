using System;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
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