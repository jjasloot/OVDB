using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class AdminUser
    {
        public string Email { get; set; }
        public DateTime LastLogin { get; set; }
        public int RouteCount { get; set; }
        public bool IsAdmin { get; set; }
        public int Id { get; set; }
    }
}
