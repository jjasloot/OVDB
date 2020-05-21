using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class FileUpload
    {
        public string Filename { get; set; }
        public int RouteId { get; set; }
        public bool Failed { get; set; } = false;
    }
}
