using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class EditMultiple
    {
        public List<int> RouteIds { get; set; }
        public bool UpdateDate { get; set; }
        public bool UpdateType { get; set; }
        public bool UpdateCountries { get; set; }
        public bool UpdateMaps { get; set; }
        public DateTime Date { get; set; }
        public int? TypeId { get; set; }
        public List<int> Countries { get; set; }
        public List<int> Maps { get; set; }
    }

}
