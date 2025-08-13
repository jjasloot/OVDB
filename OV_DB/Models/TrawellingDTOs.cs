using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.ComponentModel;
using Newtonsoft.Json.Converters;

namespace OV_DB.Models
{
    // Enums based on Träwelling API documentation
    public enum TrawellingHafasTravelType
    {
        [Description("nationalExpress")]
        NationalExpress,
        [Description("national")]
        National,
        [Description("regionalExp")]
        RegionalExp,
        [Description("regional")]
        Regional,
        [Description("suburban")]
        Suburban,
        [Description("bus")]
        Bus,
        [Description("ferry")]
        Ferry,
        [Description("subway")]
        Subway,
        [Description("tram")]
        Tram,
        [Description("taxi")]
        Taxi,
        [Description("plane")]
        Plane
    }

    public enum TrawellingBusiness
    {
        Private = 0,
        Business = 1
    }

    public enum TrawellingStatusVisibility
    {
        Public = 0,
        Unlisted = 1,
        Followers = 2,
        Private = 3
    }

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
        public TrawellingUserAuthData Data { get; set; }
    }

    public class TrawellingUserAuthData
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

        [JsonProperty("mastodonUrl")]
        public string MastodonUrl { get; set; }

        [JsonProperty("privateProfile")]
        public bool PrivateProfile { get; set; }

        [JsonProperty("preventIndex")]
        public bool PreventIndex { get; set; }

        [JsonProperty("likes_enabled")]
        public bool LikesEnabled { get; set; }

        [JsonProperty("mapProvider")]
        public string MapProvider { get; set; }

        [JsonProperty("home")]
        public TrawellingStation Home { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("defaultStatusVisibility")]
        public int DefaultStatusVisibility { get; set; }

        [JsonProperty("roles")]
        public List<string> Roles { get; set; }
    }

    // Status Response for Träwelling check-ins
    public class TrawellingStatusesResponse
    {
        [JsonProperty("data")]
        public List<TrawellingStatus> Data { get; set; }

        [JsonProperty("links")]
        public TrawellingPaginationLinks Links { get; set; }

        [JsonProperty("meta")]
        public TrawellingPaginationMeta Meta { get; set; }
    }

    public class TrawellingStatus
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("bodyMentions")]
        public List<TrawellingMention> BodyMentions { get; set; }

        [JsonProperty("business")]
        public TrawellingBusiness Business { get; set; }

        [JsonProperty("visibility")]
        public TrawellingStatusVisibility Visibility { get; set; }

        [JsonProperty("likes")]
        public int Likes { get; set; }

        [JsonProperty("liked")]
        public bool Liked { get; set; }

        [JsonProperty("isLikable")]
        public bool IsLikable { get; set; }

        [JsonProperty("client")]
        public TrawellingClient Client { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("train")]
        public TrawellingTransport Train { get; set; }

        [JsonProperty("event")]
        public TrawellingEvent Event { get; set; }

        [JsonProperty("userDetails")]
        public TrawellingLightUser UserDetails { get; set; }

        [JsonProperty("tags")]
        public List<TrawellingStatusTag> Tags { get; set; }
    }

    public class TrawellingMention
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }

    public class TrawellingClient
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("privacyPolicyUrl")]
        public string PrivacyPolicyUrl { get; set; }
    }

    public class TrawellingTransport
    {
        [JsonProperty("trip")]
        public int Trip { get; set; }

        [JsonProperty("hafasId")]
        public string HafasId { get; set; }

        [JsonProperty("category")]
        [JsonConverter(typeof(StringEnumConverter))]
        public TrawellingHafasTravelType Category { get; set; }

        [JsonProperty("number")]
        public string Number { get; set; }

        [JsonProperty("lineName")]
        public string LineName { get; set; }

        [JsonProperty("journeyNumber")]
        public int? JourneyNumber { get; set; }

        [JsonProperty("manualJourneyNumber")]
        public string ManualJourneyNumber { get; set; }

        [JsonProperty("distance")]
        public int Distance { get; set; }

        [JsonProperty("points")]
        public int Points { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("manualDeparture")]
        public DateTime? ManualDeparture { get; set; }

        [JsonProperty("manualArrival")]
        public DateTime? ManualArrival { get; set; }

        [JsonProperty("origin")]
        public TrawellingStopover Origin { get; set; }

        [JsonProperty("destination")]
        public TrawellingStopover Destination { get; set; }

        [JsonProperty("operator")]
        public TrawellingOperator Operator { get; set; }

        [JsonProperty("dataSource")]
        public TrawellingDataSource DataSource { get; set; }
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

        [JsonProperty("arrivalPlanned")]
        public DateTime? ArrivalPlanned { get; set; }

        [JsonProperty("arrivalReal")]
        public DateTime? ArrivalReal { get; set; }

        [JsonProperty("arrivalPlatformPlanned")]
        public string ArrivalPlatformPlanned { get; set; }

        [JsonProperty("arrivalPlatformReal")]
        public string ArrivalPlatformReal { get; set; }

        [JsonProperty("departure")]
        public DateTime? Departure { get; set; }

        [JsonProperty("departurePlanned")]
        public DateTime? DeparturePlanned { get; set; }

        [JsonProperty("departureReal")]
        public DateTime? DepartureReal { get; set; }

        [JsonProperty("departurePlatformPlanned")]
        public string DeparturePlatformPlanned { get; set; }

        [JsonProperty("departurePlatformReal")]
        public string DeparturePlatformReal { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("isArrivalDelayed")]
        public bool IsArrivalDelayed { get; set; }

        [JsonProperty("isDepartureDelayed")]
        public bool IsDepartureDelayed { get; set; }

        [JsonProperty("cancelled")]
        public bool Cancelled { get; set; }
    }

    public class TrawellingOperator
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("identifier")]
        public string Identifier { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class TrawellingDataSource
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("attribution")]
        public string Attribution { get; set; }
    }

    public class TrawellingEvent
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("hashtag")]
        public string Hashtag { get; set; }

        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("begin")]
        public DateTime Begin { get; set; }

        [JsonProperty("end")]
        public DateTime End { get; set; }

        [JsonProperty("station")]
        public TrawellingStation Station { get; set; }

        [JsonProperty("isPride")]
        public bool IsPride { get; set; }
    }

    public class TrawellingLightUser
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("profilePicture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("mastodonUrl")]
        public string MastodonUrl { get; set; }

        [JsonProperty("preventIndex")]
        public bool PreventIndex { get; set; }
    }

    public class TrawellingStatusTag
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("visibility")]
        public int Visibility { get; set; }
    }

    public class TrawellingStation
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("latitude")]
        public double? Latitude { get; set; }

        [JsonProperty("longitude")]
        public double? Longitude { get; set; }

        [JsonProperty("ibnr")]
        public string Ibnr { get; set; }

        [JsonProperty("rilIdentifier")]
        public string RilIdentifier { get; set; }

        [JsonProperty("areas")]
        public List<TrawellingArea> Areas { get; set; }
    }

    public class TrawellingArea
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("default")]
        public bool Default { get; set; }

        [JsonProperty("adminLevel")]
        public int AdminLevel { get; set; }
    }

    public class TrawellingPaginationLinks
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

    public class TrawellingPaginationMeta
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

        [JsonProperty("total")]
        public int? Total { get; set; }
    }

    // Single Status Response
    public class TrawellingStatusResponse
    {
        [JsonProperty("data")]
        public TrawellingStatus Data { get; set; }
    }

    // Request/Response DTOs for OVDB functionality
    public class TrawellingOAuthRequest
    {
        public string Code { get; set; }
        public string State { get; set; }
    }

    public class LinkToRouteInstanceRequest
    {
        public int StatusId { get; set; }
        public int RouteInstanceId { get; set; }
    }

    public class LinkToRouteInstanceResponse
    {
        public bool Success { get; set; }
        public RouteInstanceSearchResult RouteInstance { get; set; }
        public string Message { get; set; }
    }

    public class RouteInstanceSearchResult
    {
        public int Id { get; set; }
        public int RouteId { get; set; }
        public string RouteName { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Date { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public double? DurationHours { get; set; }
        public int? TrawellingStatusId { get; set; }
        public bool HasTraewellingLink { get; set; }
    }

    public class TrawellingIgnoreRequest
    {
        public int StatusId { get; set; }
    }

    public class TrawellingIgnoreResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class TrawellingStats
    {
        public int TotalTrips { get; set; }
        public int ImportedTrips { get; set; }
        public int UnimportedTrips { get; set; }
        public int EnhancedInstances { get; set; }
        public string ConnectedSince { get; set; }
    }

    // Legacy aliases for backward compatibility (will be removed)
    public class TrawellingUserData : TrawellingUserAuthData { }
}