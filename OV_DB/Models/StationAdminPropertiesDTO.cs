﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class StationAdminPropertiesDTO
    {
        public int id { get; set; }
        public string name { get; set; }
        public string network { get; set; }
        public string operatingCompany { get; set; }
        public double? elevation { get; set; }
        public bool hidden { get; set; }
        public bool special { get; set; }
    }
}
