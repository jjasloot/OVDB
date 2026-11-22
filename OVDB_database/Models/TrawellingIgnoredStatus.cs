using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models
{
    [Index(nameof(UserId), nameof(TrawellingStatusId), IsUnique = true)]
    [Index(nameof(UserId), nameof(TrawellingStatusUuid), IsUnique = true)]
    public class TrawellingIgnoredStatus
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        /// <summary>Träwelling's numeric id; null for a status that came with only its UUID.</summary>
        public int? TrawellingStatusId { get; set; }

        /// <summary>Träwelling's UUID for the status (see RouteInstance.TrawellingStatusUuid).</summary>
        [MaxLength(36)]
        public string? TrawellingStatusUuid { get; set; }

        public DateTime IgnoredAt { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; }
    }
}