namespace OV_DB.Models
{
    public class StationNearbyPairDTO
    {
        public int Station1Id { get; set; }
        public string Station1Name { get; set; }
        public double Station1Lattitude { get; set; }
        public double Station1Longitude { get; set; }
        public int Station1Visits { get; set; }

        public int Station2Id { get; set; }
        public string Station2Name { get; set; }
        public double Station2Lattitude { get; set; }
        public double Station2Longitude { get; set; }
        public int Station2Visits { get; set; }

        public double DistanceMeters { get; set; }
    }
}
