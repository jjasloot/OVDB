using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;

namespace OV_DB.Models
{
    public class TrainlogExportRow
    {
        [Name("uid")]
        public string Uid { get; set; }
        [Name("username")]
        public string Username { get; set; }
        [Name("origin_station")]
        public string OriginStation { get; set; }
        [Name("destination_station")]
        public string DestinationStation { get; set; }
        [Name("start_datetime")]
        public string StartDatetime { get; set; }
        [Name("end_datetime")]
        public string EndDatetime { get; set; }
        [Name("estimated_trip_duration")]
        public string EstimatedTripDuration { get; set; }
        [Name("manual_trip_duration")]
        public string ManualTripDuration { get; set; }
        [Name("trip_length")]
        public string TripLength { get; set; }
        [Name("operator")]
        public string Operator { get; set; }
        [Name("countries")]
        public string Countries { get; set; }
        [Name("utc_start_datetime")]
        public string UtcStartDatetime { get; set; }
        [Name("utc_end_datetime")]
        public string UtcEndDatetime { get; set; }
        [Name("line_name")]
        public string LineName { get; set; }
        [Name("created")]
        public string Created { get; set; }
        [Name("last_modified")]
        public string LastModified { get; set; }
        [Name("type")]
        public string Type { get; set; }
        [Name("material_type")]
        public string MaterialType { get; set; }
        [Name("seat")]
        public string Seat { get; set; }
        [Name("reg")]
        public string Reg { get; set; }
        [Name("waypoints")]
        public string Waypoints { get; set; }
        [Name("notes")]
        public string Notes { get; set; }
        [Name("price")]
        public string Price { get; set; }
        [Name("currency")]
        public string Currency { get; set; }
        [Name("purchasing_date")]
        public string PurchasingDate { get; set; }
        [Name("path")]
        public string Path { get; set; }
    }

    public class ExportRequest
    {
        public List<int> RouteInstanceIds { get; set; }
        public List<int> RouteIds { get; set; }
    }
}
