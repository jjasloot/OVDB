using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class AdminMap
    {
        public string MapName { get; set; }
        public string UserEmail { get; set; }
        public string ShareLink { get; set; }
        public Guid Guid { get; set; }
        public int RouteCount { get; set; }
        public int Id { get;    set; }
    }
}
