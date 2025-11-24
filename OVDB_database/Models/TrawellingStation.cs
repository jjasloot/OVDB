using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models
{
    [Table("trawelling_stations")]
    [Index(nameof(TrawellingId), IsUnique = true)]
    public class TrawellingStation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("traewelling_id")]
        [Required]
        public int TrawellingId { get; set; }

        [Column("name")]
        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        [Column("latitude")]
        public double? Latitude { get; set; }

        [Column("longitude")]
        public double? Longitude { get; set; }

        [Column("ibnr")]
        [MaxLength(50)]
        public string Ibnr { get; set; }

        [Column("ril_identifier")]
        [MaxLength(50)]
        public string RilIdentifier { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}