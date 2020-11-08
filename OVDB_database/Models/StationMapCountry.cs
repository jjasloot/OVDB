using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class StationMapCountry
    {
        [Key]
        public int Id { get; set; }
        public int StationMapId { get; set; }
        public StationMap StationMap { get; set; }
        public int StationCountryId { get; set; }
        public StationCountry StationCountry { get; set; }
        public bool IncludeSpecials { get; set; }
    }
}
