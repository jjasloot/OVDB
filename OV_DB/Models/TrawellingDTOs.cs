using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace OV_DB.Models
{
    // OAuth2 Token Response
    public class TrawellingTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }
    }

    // User Authentication Response
    public class TrawellingUserAuthResponse
    {
        [JsonProperty("data")]
        public TrawellingUserData Data { get; set; }
    }

    public class TrawellingUserData
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("profilePicture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("totalDistance")]
        public int TotalDistance { get; set; }

        [JsonProperty("totalDuration")]
        public int TotalDuration { get; set; }

        [JsonProperty("points")]
        public int Points { get; set; }
    }

    // Status Response for Träwelling check-ins
    public class TrawellingStatusesResponse
    {
        [JsonProperty("data")]
        public List<TrawellingStatus> Data { get; set; }

        [JsonProperty("links")]
        public TrawellingLinks Links { get; set; }

        [JsonProperty("meta")]
        public TrawellingMeta Meta { get; set; }
    }

    public class TrawellingStatus
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("train")]
        public TrawellingTransport Train { get; set; }

        [JsonProperty("userDetails")]
        public TrawellingUserDetails UserDetails { get; set; }
    }

    public class TrawellingTransport
    {
        [JsonProperty("trip")]
        public int Trip { get; set; }

        [JsonProperty("hafasId")]
        public string HafasId { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("number")]
        public string Number { get; set; }

        [JsonProperty("lineName")]
        public string LineName { get; set; }

        [JsonProperty("journeyNumber")]
        public int? JourneyNumber { get; set; }
        [JsonProperty("manualJourneyNumber")]
        public int? ManuelJourneyNumber { get; set; }

        [JsonProperty("distance")]
        public int Distance { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("origin")]
        public TrawellingStopover Origin { get; set; }

        [JsonProperty("destination")]
        public TrawellingStopover Destination { get; set; }
    }

    public class TrawellingStopover
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("rilIdentifier")]
        public string RilIdentifier { get; set; }

        [JsonProperty("evaIdentifier")]
        public string EvaIdentifier { get; set; }

        [JsonProperty("arrival")]
        public DateTime? Arrival { get; set; }

        [JsonProperty("departure")]
        public DateTime? Departure { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }
    }

    public class TrawellingUserDetails
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }
    }

    public class TrawellingLinks
    {
        [JsonProperty("first")]
        public string First { get; set; }

        [JsonProperty("last")]
        public string Last { get; set; }

        [JsonProperty("prev")]
        public string Prev { get; set; }

        [JsonProperty("next")]
        public string Next { get; set; }
    }

    public class TrawellingMeta
    {
        [JsonProperty("current_page")]
        public int CurrentPage { get; set; }

        [JsonProperty("from")]
        public int From { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("per_page")]
        public int PerPage { get; set; }

        [JsonProperty("to")]
        public int To { get; set; }
    }

    // Request DTOs
    public class TrawellingImportRequest
    {
        public int StatusId { get; set; }
        public bool ImportMetadata { get; set; } = true;
        public bool ImportTags { get; set; } = true;
    }

    public class TrawellingOAuthRequest
    {
        public string Code { get; set; }
        public string State { get; set; }
    }

    // Single Status Response for import
    public class TrawellingStatusResponse
    {
        [JsonProperty("data")]
        public TrawellingStatus Data { get; set; }
    }

    // Link to existing RouteInstance request
    public class LinkToRouteInstanceRequest
    {
        public int StatusId { get; set; }
        public int RouteInstanceId { get; set; }
    }
}