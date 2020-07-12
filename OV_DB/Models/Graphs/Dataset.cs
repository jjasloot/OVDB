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
        public double CategoryPercentage = 0.7d;
        public double BarPercentage { get; set; } = 1.0d;
        public string BarThickness = "flex";
        public string Stack { get; set; }
        public string BorderColor { get; set; }
        public string PointBackgroundColor { get; set; }
    }
}
