using Newtonsoft.Json;
using OV_DB.Models;
using OV_DB.Services;

namespace OV_DB.Tests
{
    // ConflictFingerprint decides whether an upstream change to a Träwelling status re-flags a
    // conflict the user already dismissed (via DismissedFingerprintMatches). It must depend only
    // on the journey facts (line + both stations/times) and ignore social fields, or a dismissed
    // conflict re-appears on every like/tag/body edit.
    public class ConflictFingerprintTests
    {
        private static string Payload(
            string lineName = "RE 5",
            string originStation = "Karlsruhe Hbf",
            string destinationStation = "Stuttgart Hbf",
            string departurePlanned = "2026-08-01T10:05:00+02:00",
            string arrivalPlanned = "2026-08-01T10:50:00+02:00",
            string body = "Test trip")
        {
            return $$"""
            {
                "id": 123456,
                "body": "{{body}}",
                "checkin": {
                    "lineName": "{{lineName}}",
                    "origin": {
                        "station": { "id": 4711, "name": "{{originStation}}" },
                        "departurePlanned": "{{departurePlanned}}"
                    },
                    "destination": {
                        "station": { "id": 4712, "name": "{{destinationStation}}" },
                        "arrivalPlanned": "{{arrivalPlanned}}"
                    }
                }
            }
            """;
        }

        private static TrawellingStatus Parse(string json) => JsonConvert.DeserializeObject<TrawellingStatus>(json);

        [Fact]
        public void IdenticalJourneyFacts_ProduceEqualFingerprints()
        {
            var a = TrawellingService.ConflictFingerprint(Parse(Payload()));
            var b = TrawellingService.ConflictFingerprint(Parse(Payload()));

            Assert.Equal(a, b);
        }

        [Fact]
        public void SocialFieldChange_DoesNotChangeFingerprint()
        {
            var original = TrawellingService.ConflictFingerprint(Parse(Payload(body: "Nice trip")));
            var edited = TrawellingService.ConflictFingerprint(Parse(Payload(body: "Edited body with #hashtag")));

            Assert.Equal(original, edited);
        }

        [Theory]
        [InlineData("RE 99", "Karlsruhe Hbf", "Stuttgart Hbf", "2026-08-01T10:05:00+02:00")]
        [InlineData("RE 5", "Mannheim Hbf", "Stuttgart Hbf", "2026-08-01T10:05:00+02:00")]
        [InlineData("RE 5", "Karlsruhe Hbf", "München Hbf", "2026-08-01T10:05:00+02:00")]
        [InlineData("RE 5", "Karlsruhe Hbf", "Stuttgart Hbf", "2026-08-01T10:15:00+02:00")]
        public void JourneyFactChange_ChangesFingerprint(string line, string origin, string destination, string departure)
        {
            var baseline = TrawellingService.ConflictFingerprint(Parse(Payload()));
            var changed = TrawellingService.ConflictFingerprint(
                Parse(Payload(lineName: line, originStation: origin, destinationStation: destination, departurePlanned: departure)));

            Assert.NotEqual(baseline, changed);
        }

        [Fact]
        public void DepartureTimes_AreComparedAsInstants_NotStrings()
        {
            // Same instant expressed in two offsets must fingerprint identically (the method
            // normalises to UTC via .UtcDateTime.ToString("o")).
            var cet = TrawellingService.ConflictFingerprint(Parse(Payload(departurePlanned: "2026-08-01T10:05:00+02:00")));
            var utc = TrawellingService.ConflictFingerprint(Parse(Payload(departurePlanned: "2026-08-01T08:05:00Z")));

            Assert.Equal(cet, utc);
        }

        [Fact]
        public void NullStatusAndNullCheckin_DoNotThrow()
        {
            var nullStatus = TrawellingService.ConflictFingerprint(null);
            var noCheckin = TrawellingService.ConflictFingerprint(new TrawellingStatus());

            Assert.NotNull(nullStatus);
            Assert.Equal(nullStatus, noCheckin);
        }
    }
}
