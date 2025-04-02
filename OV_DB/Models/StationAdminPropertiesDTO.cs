namespace OV_DB.Models
{
    public class StationAdminPropertiesDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
        public bool Hidden { get; set; }
        public bool Special { get; set; }
        public int StationVisits { get; set; }
    }
}
