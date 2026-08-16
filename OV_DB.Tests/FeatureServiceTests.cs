using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using OV_DB.Services;

namespace OV_DB.Tests
{
    public class FeatureServiceTests
    {
        private static FeatureService WithSetting(string value)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["Features:Achievements"] = value })
                .Build();
            return new FeatureService(configuration);
        }

        [Theory]
        [InlineData("Off", FeatureVisibility.Off)]
        [InlineData("off", FeatureVisibility.Off)]
        [InlineData("Admin", FeatureVisibility.Admin)]
        [InlineData("On", FeatureVisibility.On)]
        public void Parse_ReadsTheConfiguredVisibility(string value, FeatureVisibility expected)
        {
            Assert.Equal(expected, FeatureService.Parse(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("enabled")]
        public void Parse_FallsBackToAdminOnlyForAnythingUnrecognised(string value)
        {
            // A typo must hide the feature rather than expose it to every user.
            Assert.Equal(FeatureVisibility.Admin, FeatureService.Parse(value));
        }

        [Theory]
        [InlineData(FeatureVisibility.Off, true, false)]
        [InlineData(FeatureVisibility.Off, false, false)]
        [InlineData(FeatureVisibility.Admin, true, true)]
        [InlineData(FeatureVisibility.Admin, false, false)]
        [InlineData(FeatureVisibility.On, true, true)]
        [InlineData(FeatureVisibility.On, false, true)]
        public void IsVisible_AppliesTheThreeStates(FeatureVisibility visibility, bool isAdmin, bool expected)
        {
            Assert.Equal(expected, WithSetting("On").IsVisible(visibility, isAdmin));
        }

        [Fact]
        public void Achievements_ReadsTheConfiguredValue()
        {
            Assert.Equal(FeatureVisibility.Off, WithSetting("Off").Achievements);
        }
    }
}
