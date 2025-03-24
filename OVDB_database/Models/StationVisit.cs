using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    [Index(nameof(StationId), nameof(UserId), IsUnique = true)]
    public class StationVisit
    {
        [Key]
        public long Id { get; set; }
        public int StationId { get; set; }
        public Station Station { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
    }
}
