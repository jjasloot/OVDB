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

        // Only present when the authorize request carried trwl_webhook_* parameters;
        // the token exchange is the only moment Träwelling hands out the secret
        [JsonProperty("webhook")]
        public TrawellingTokenWebhook Webhook { get; set; }
    }

    public class TrawellingWebhooksResponse
    {
        [JsonProperty("data")]
        public List<TrawellingWebhook> Data { get; set; }
    }

    public class TrawellingWebhook
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("disabledAt")]
        public DateTimeOffset? DisabledAt { get; set; }
    }

    public enum TrawellingWebhookHealth
    {
        NotEnabled,
        Active,
        DisabledUpstream,
        Missing,
        Unknown,
    }

    public class TrawellingTokenWebhook
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("secret")]
        public string Secret { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
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
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("checkin")]
        public TrawellingTransport Checkin { get; set; }

        [JsonProperty("event")]
        public TrawellingEvent Event { get; set; }

        [JsonProperty("user")]
        public TrawellingLightUser User { get; set; }

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
        public long? JourneyNumber { get; set; }

        [JsonProperty("manualJourneyNumber")]
        public string ManualJourneyNumber { get; set; }

        [JsonProperty("distance")]
        public int Distance { get; set; }

        [JsonProperty("points")]
        public int Points { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("manualDeparture")]
        public DateTimeOffset? ManualDeparture { get; set; }

        [JsonProperty("manualArrival")]
        public DateTimeOffset? ManualArrival { get; set; }

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
        // Stable identifier of this stopover (the numeric "id" is deprecated upstream and
        // changes meaning after 2026-11-30, so it is intentionally not mapped here)
        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("station")]
        public TrawellingStation Station { get; set; }

        [JsonProperty("arrivalPlanned")]
        public DateTimeOffset? ArrivalPlanned { get; set; }

        [JsonProperty("arrivalReal")]
        public DateTimeOffset? ArrivalReal { get; set; }

        [JsonProperty("arrivalPlatformPlanned")]
        public string ArrivalPlatformPlanned { get; set; }

        [JsonProperty("arrivalPlatformReal")]
        public string ArrivalPlatformReal { get; set; }

        [JsonProperty("departurePlanned")]
        public DateTimeOffset? DeparturePlanned { get; set; }

        [JsonProperty("departureReal")]
        public DateTimeOffset? DepartureReal { get; set; }

        [JsonProperty("departurePlatformPlanned")]
        public string DeparturePlatformPlanned { get; set; }

        [JsonProperty("departurePlatformReal")]
        public string DeparturePlatformReal { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("cancelled")]
        public bool Cancelled { get; set; }

        // The upstream isArrivalDelayed/isDepartureDelayed flags are deprecated; delay is
        // derived from planned vs real times as the API documentation recommends
        [JsonIgnore]
        public bool IsArrivalDelayed => ArrivalReal.HasValue && ArrivalPlanned.HasValue && ArrivalReal > ArrivalPlanned;

        [JsonIgnore]
        public bool IsDepartureDelayed => DepartureReal.HasValue && DeparturePlanned.HasValue && DepartureReal > DeparturePlanned;
    }

    public class TrawellingOperator
    {
        // The numeric "id" is deprecated upstream (becomes a UUID after 2026-09-30) and the
        // legacy "identifier" is always null for new operators, so only uuid/name are mapped
        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class TrawellingAlertTranslation
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }
    }

    public class TrawellingAlert
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("active_from")]
        public DateTimeOffset? ActiveFrom { get; set; }

        [JsonProperty("active_until")]
        public DateTimeOffset? ActiveUntil { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("translations")]
        public List<TrawellingAlertTranslation> Translations { get; set; }
    }

    public class TrawellingApiAlertsResponse
    {
        [JsonProperty("data")]
        public List<TrawellingAlert> Data { get; set; }
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
        public DateTimeOffset Begin { get; set; }

        [JsonProperty("end")]
        public DateTimeOffset End { get; set; }

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
        // Deprecated upstream in favour of uuid, but still the stable key today
        [JsonProperty("id")]
        public int Id { get; set; }

        // Stable identifier; will become the primary key upstream
        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("latitude")]
        public double? Latitude { get; set; }

        [JsonProperty("longitude")]
        public double? Longitude { get; set; }

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

    // Optimized DTOs for frontend with local timing
    public class TrawellingTripDto
    {
        public int Id { get; set; }
        public string Body { get; set; }
        public TrawellingBusiness Business { get; set; }
        public TrawellingStatusVisibility Visibility { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public TrawellingTransportDto Transport { get; set; }
        public TrawellingLightUserDto UserDetails { get; set; }
        public List<TrawellingStatusTagDto> Tags { get; set; }
    }

    public class TrawellingTransportDto
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public TrawellingHafasTravelType Category { get; set; }
        public string Number { get; set; }
        public string LineName { get; set; }
        public string? JourneyNumber { get; set; }
        public int Distance { get; set; }
        public int Duration { get; set; }
        public TrawellingStopoverDto Origin { get; set; }
        public TrawellingStopoverDto Destination { get; set; }
        public TrawellingOperatorDto Operator { get; set; }
    }

    public class TrawellingStopoverDto
    {
        public string Name { get; set; }
        
        // Scheduled times (local timezone)
        public DateTime? ArrivalScheduled { get; set; }
        public DateTime? DepartureScheduled { get; set; }
        public string ArrivalPlatformPlanned { get; set; }
        public string DeparturePlatformPlanned { get; set; }
        
        // Real/actual times (local timezone) - includes manual times if available
        public DateTime? ArrivalReal { get; set; }
        public DateTime? DepartureReal { get; set; }
        public string ArrivalPlatformReal { get; set; }
        public string DeparturePlatformReal { get; set; }
        
        // Status indicators
        public bool IsArrivalDelayed { get; set; }
        public bool IsDepartureDelayed { get; set; }
        public bool Cancelled { get; set; }
        
        // Platform information
        public string Platform { get; set; }
    }

    public class TrawellingOperatorDto
    {
        public string Name { get; set; }
    }

    public class TrawellingLightUserDto
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string Username { get; set; }
        public string ProfilePicture { get; set; }
    }

    public class TrawellingStatusTagDto
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public int Visibility { get; set; }
    }

    // Optimized response for frontend
    public class TrawellingTripsResponse
    {
        public List<TrawellingTripDto> Data { get; set; }
        public TrawellingPaginationLinks Links { get; set; }
        public TrawellingPaginationMeta Meta { get; set; }
        public bool HasMorePages { get; set; }
    }

    /// <summary>
    /// An imported trip whose Träwelling status was edited or deleted upstream after import,
    /// awaiting the user's decision.
    /// </summary>
    public class TrawellingConflictDto
    {
        public int StatusId { get; set; }
        /// <summary>ChangedAfterImport or DeletedUpstream</summary>
        public string State { get; set; }
        public DateTime LastEventAt { get; set; }
        public int RouteInstanceId { get; set; }
        public string RouteName { get; set; }
        public string RouteFrom { get; set; }
        public string RouteTo { get; set; }
        public DateTime InstanceDate { get; set; }
        public DateTime? InstanceStartTime { get; set; }
        public DateTime? InstanceEndTime { get; set; }
        /// <summary>True when this is the route's only trip, so deleting it deletes the route too</summary>
        public bool IsLastInstanceOnRoute { get; set; }
        /// <summary>The upstream status as it is now (null only for unparsable payloads)</summary>
        public TrawellingTripDto NewTrip { get; set; }
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

}