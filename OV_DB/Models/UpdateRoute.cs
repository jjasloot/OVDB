using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    public class UpdateRoute
    {
        public int RouteId { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string Description { get; set; }
        public string DescriptionNL { get; set; }
        public string LineNumber { get; set; }
        public string OperatingCompany { get; set; }
        public DateTime? FirstDateTime { get; set; }
        public List<int> Countries { get; set; }
        public List<int> Maps { get; set; }
        public int? RouteTypeId { get; set; }
        public string OverrideColour { get; set; }
    }
}
