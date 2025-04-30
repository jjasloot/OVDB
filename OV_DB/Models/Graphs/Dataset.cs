using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OV_DB.Models.Graphs
{
    public class Dataset
    {
        public string Label { get; set; }
        public List<Point> Data { get; set; }
        public bool Stepped { get; set; } = true;
        public double CategoryPercentage = 1.0d;
        public double BarPercentage { get; set; } = 1.0d;
        public string BarThickness = "flex";
        public bool Stack { get; set; }
        public string BorderColor { get; set; }
        public bool Fill { get; set; }
        public string BackgroundColor { get; set; }
        [JsonPropertyName("yAxisID")]
        public string YAxisID = "y";
    }
}
