using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class RouteInstanceUpdate
    {
        public int? RouteInstanceId { get; set; }
        public int RouteId { get; set; }
        public DateTime Date { get; set; }
        public List<RouteInstancePropertyUpdate> RouteInstanceProperties { get; set; }
    }
}
