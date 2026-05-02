using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    [Index(nameof(Station1Id), nameof(Station2Id), IsUnique = true)]
    public class StationMergeIgnore
    {
        [Key]
        public long Id { get; set; }
        public int Station1Id { get; set; }
        public Station Station1 { get; set; }
        public int Station2Id { get; set; }
        public Station Station2 { get; set; }
    }
}
