using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class StationCountry
    {
        [Key]
        public int Id { get; set; }
        public string OsmId { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }

    }
}
