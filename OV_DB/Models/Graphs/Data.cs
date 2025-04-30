using OV_DB.Models.Graphs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models.Graphs
{
    public class Data
    {
        public List<Dataset> Datasets { get; set; }
    }

    public class GraphData
    {
        public string Type { get; set; } = "line";
        public Data Data { get; set; }
    }
}
