using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models.Graphs
{
    public class Dataset
    {
        public string Label { get; set; }
        public List<Point> Data { get; set; }
        public bool SteppedLine { get; set; } = true;
    }
}
