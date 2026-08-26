using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Models;

namespace OV_DB.Tests
{
    // Träwelling fires checkin_update for likes, tags, body edits and visibility changes as well as
    // for journey edits. UpstreamDiffers is what stops the first four raising a "changed on
    // Träwelling" notice whose two columns are identical, so it is worth pinning down — including
    // the tolerances, which have to match the conflict card's or the card and the flag disagree.
    public class UpstreamDiffersTests
    {
        private static RouteInstance Instance(
            string from = "Karlsruhe Hbf",
            string to = "Stuttgart Hbf",
            string start = "2026-08-01T10:05:00",
            string end = "2026-08-01T10:50:00")
        {
            return new RouteInstance
            {
                Route = new Route { From = from, To = to },
                StartTime = start == null ? null : DateTime.Parse(start),
                EndTime = end == null ? null : DateTime.Parse(end),
            };
        }

        private static TrawellingTripDto Trip(
            string originName = "Karlsruhe Hbf",
            string destinationName = "Stuttgart Hbf",
            string departure = "2026-08-01T10:05:00",
            string arrival = "2026-08-01T10:50:00",
            string departureScheduled = null,
            string arrivalScheduled = null)
        {
            return new TrawellingTripDto
            {
                Transport = new TrawellingTransportDto
                {
                    Origin = new TrawellingStopoverDto
                    {
                        Name = originName,
                        DepartureReal = departure == null ? null : DateTime.Parse(departure),
                        DepartureScheduled = departureScheduled == null ? null : DateTime.Parse(departureScheduled),
                    },
                    Destination = new TrawellingStopoverDto
                    {
                        Name = destinationName,
                        ArrivalReal = arrival == null ? null : DateTime.Parse(arrival),
                        ArrivalScheduled = arrivalScheduled == null ? null : DateTime.Parse(arrivalScheduled),
                    },
                }
            };
        }

        [Fact]
        public void UnchangedJourney_IsNotAConflict()
        {
            Assert.False(TrawellingService.UpstreamDiffers(Instance(), Trip()));
        }

        [Fact]
        public void SubMinuteDifference_IsNotAConflict()
        {
            // Serialisation round-trips can move seconds; the card compares whole minutes
            var instance = Instance(start: "2026-08-01T10:05:00");
            var trip = Trip(departure: "2026-08-01T10:05:41");

            Assert.False(TrawellingService.UpstreamDiffers(instance, trip));
        }

        [Fact]
        public void RenamedRouteEndpoint_IsNotAConflict()
        {
            // OVDB route endpoints are the user's own text: "Karlsruhe" against "Karlsruhe Hbf" is
            // the user's edit, not Träwelling's — but a blank side says nothing either way
            Assert.False(TrawellingService.UpstreamDiffers(Instance(from: null), Trip()));
            Assert.False(TrawellingService.UpstreamDiffers(Instance(), Trip(originName: null)));
        }

        [Fact]
        public void CasingAndWhitespaceOnly_IsNotAConflict()
        {
            Assert.False(TrawellingService.UpstreamDiffers(Instance(from: "karlsruhe  hbf"), Trip()));
        }

        [Fact]
        public void ChangedDeparture_IsAConflict()
        {
            Assert.True(TrawellingService.UpstreamDiffers(Instance(), Trip(departure: "2026-08-01T10:35:00")));
        }

        [Fact]
        public void ChangedArrival_IsAConflict()
        {
            Assert.True(TrawellingService.UpstreamDiffers(Instance(), Trip(arrival: "2026-08-01T11:20:00")));
        }

        [Fact]
        public void ChangedEndpoint_IsAConflict()
        {
            Assert.True(TrawellingService.UpstreamDiffers(Instance(), Trip(destinationName: "Ulm Hbf")));
        }

        [Fact]
        public void TimeDisappearingUpstream_IsAConflict()
        {
            Assert.True(TrawellingService.UpstreamDiffers(Instance(), Trip(departure: null)));
        }

        [Fact]
        public void ScheduledTimeStandsInForAMissingRealTime()
        {
            // Same fallback the import and apply-times use: real wins, scheduled fills in
            var trip = Trip(departure: null, departureScheduled: "2026-08-01T10:05:00");

            Assert.False(TrawellingService.UpstreamDiffers(Instance(), trip));
        }

        [Fact]
        public void UnmappableStatus_IsAConflict()
        {
            // Better a notice to look at than a silently swallowed upstream edit
            Assert.True(TrawellingService.UpstreamDiffers(Instance(), null));
            Assert.True(TrawellingService.UpstreamDiffers(Instance(), new TrawellingTripDto()));
        }
    }
}
