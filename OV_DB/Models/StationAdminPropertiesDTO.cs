namespace OV_DB.Models
{
    public class StationAdminPropertiesDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Network { get; set; }
        public string OperatingCompany { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
        public double? Elevation { get; set; }
        public bool Hidden { get; set; }
        public bool Special { get; set; }
    }
}
