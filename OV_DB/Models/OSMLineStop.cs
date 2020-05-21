using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class OSMLineStop
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string? Ref { get; set; }
    }
}
