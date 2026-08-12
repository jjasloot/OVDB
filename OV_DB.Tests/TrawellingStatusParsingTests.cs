using Newtonsoft.Json;
using OV_DB.Models;

namespace OV_DB.Tests
{
    public class TrawellingStatusParsingTests
    {
        // Representative payload in the post-2026-07-19 API shape: stopovers carry a nested
        // "station" object; the deprecated top-level id/name fields are absent
        private const string StatusJson = """
        {
            "id": 123456,
            "body": "Test trip",
            "business": 0,
            "visibility": 0,
            "createdAt": "2026-08-01T10:00:00+02:00",
            "user": {
                "id": 42,
                "displayName": "Test User",
                "username": "testuser"
            },
            "checkin": {
                "trip": 987,
                "category": "regional",
                "number": "RE 1234",
                "lineName": "RE 5",
                "distance": 52000,
                "duration": 45,
                "operator": {
                    "id": 1,
                    "uuid": "b2e9a1a0-0000-0000-0000-000000000000",
                    "identifier": null,
                    "name": "DB Regio AG Nord"
                },
                "origin": {
                    "id": 999999,
                    "uuid": "11111111-0000-0000-0000-000000000000",
                    "name": "Legacy name should be ignored",
                    "station": {
                        "id": 4711,
                        "uuid": "22222222-0000-0000-0000-000000000000",
                        "name": "Karlsruhe Hbf",
                        "latitude": 48.993207,
                        "longitude": 8.400977
                    },
                    "departurePlanned": "2026-08-01T10:05:00+02:00",
                    "departureReal": "2026-08-01T10:12:00+02:00",
                    "cancelled": false
                },
                "destination": {
                    "id": 888888,
                    "uuid": "33333333-0000-0000-0000-000000000000",
                    "station": {
                        "id": 4712,
                        "uuid": "44444444-0000-0000-0000-000000000000",
                        "name": "Stuttgart Hbf",
                        "latitude": 48.784084,
                        "longitude": 9.181635
                    },
                    "arrivalPlanned": "2026-08-01T10:50:00+02:00",
                    "arrivalReal": "2026-08-01T10:50:00+02:00",
                    "cancelled": false
                }
            }
        }
        """;

        [Fact]
        public void Deserialize_NewApiShape_ReadsStationFromStopover()
        {
            var status = JsonConvert.DeserializeObject<TrawellingStatus>(StatusJson);

            Assert.NotNull(status?.Checkin);
            Assert.Equal("Karlsruhe Hbf", status.Checkin.Origin.Station.Name);
            Assert.Equal("Stuttgart Hbf", status.Checkin.Destination.Station.Name);
            Assert.Equal(48.993207, status.Checkin.Origin.Station.Latitude);
            Assert.Equal(8.400977, status.Checkin.Origin.Station.Longitude);
            Assert.Equal("11111111-0000-0000-0000-000000000000", status.Checkin.Origin.Uuid);
        }

        [Fact]
        public void Deserialize_NewApiShape_ReadsUserAndOperator()
        {
            var status = JsonConvert.DeserializeObject<TrawellingStatus>(StatusJson);

            Assert.Equal("testuser", status.User.Username);
            Assert.Equal("DB Regio AG Nord", status.Checkin.Operator.Name);
            Assert.Equal("b2e9a1a0-0000-0000-0000-000000000000", status.Checkin.Operator.Uuid);
        }

        [Fact]
        public void DelayFlags_AreDerivedFromPlannedVersusRealTimes()
        {
            var status = JsonConvert.DeserializeObject<TrawellingStatus>(StatusJson);

            // Origin departed 7 minutes late, destination arrived exactly on time
            Assert.True(status.Checkin.Origin.IsDepartureDelayed);
            Assert.False(status.Checkin.Destination.IsArrivalDelayed);
        }

        [Fact]
        public void DelayFlags_AreFalseWhenTimesAreMissing()
        {
            var stopover = JsonConvert.DeserializeObject<TrawellingStopover>("""{"cancelled": false}""");

            Assert.False(stopover.IsArrivalDelayed);
            Assert.False(stopover.IsDepartureDelayed);
        }
    }
}
