using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class RouteInstanceProperty
    {
        [Key]
        public long RouteInstancePropertyId { get; set; }
        public int RouteInstanceId { get; set; }
        public RouteInstance RouteInstance { get; set; }
        public string Key { get; set; }
        public string? Value { get; set; }
        public DateTime? Date { get; set; }
        public bool? Bool { get; set; }
    }
}
