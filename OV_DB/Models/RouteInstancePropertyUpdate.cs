using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class RouteInstancePropertyUpdate
    {
        public long? RouteInstancePropertyId { get; set; }
        public int? RouteInstanceId { get; set; }
        public string? Key { get; set; }
        public string? Value { get; set; }
        public DateTime? Date { get; set; }
        public bool? Bool { get; set; }
    }
}
