namespace OV_DB.Models
{
    /// <summary>
    /// Represents a single tag mapping from a Traewelling tag to a custom OVDB tag
    /// </summary>
    public class TraewellingTagMappingDTO
    {
        /// <summary>
        /// The original Traewelling tag key (e.g., "trwl:vehicle_number")
        /// </summary>
        public string FromTag { get; set; }

        /// <summary>
        /// The custom OVDB tag key to map to (e.g., "registration")
        /// </summary>
        public string ToTag { get; set; }
    }
}
