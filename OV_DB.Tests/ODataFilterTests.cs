using System.Linq;
using OV_DB;
using OV_DB.Controllers;
using Xunit;

namespace OV_DB.Tests
{
    // The map GeoJSON endpoint is anonymous for shared maps. Any $filter it accepts runs against
    // the database, so it must only accept what the map page builds and the model must not
    // expose account data at all.
    public class ODataFilterTests
    {
        [Theory]
        [InlineData("(Date ge 2024-01-01 and Date lt 2025-01-01)")]
        [InlineData("(Route/Regions/any(region: region/Id eq 3) or Route/Regions/any(region: region/Id eq 12)) and (Route/RouteTypeId eq 5)")]
        [InlineData("((Date ge 2023-01-01 and Date lt 2024-01-01) or Route/FirstDateTime eq null)")]
        [InlineData("(Date ge 2024-01-01 and Date lt 2024-06-01)  and (Route/RouteTypeId eq 1 or Route/RouteTypeId eq 2)")]
        public void FiltersTheMapPageBuildsAreAllowed(string filter)
        {
            Assert.True(RoutesOdataController.IsAllowedFilter(filter));
        }

        [Theory]
        [InlineData("Route/RouteMaps/any(rm: startswith(rm/Map/User/Password,'A'))")]
        [InlineData("Route/Regions/any(region: region/Id eq 3 and region/Routes/any(r: r/Name eq 'x'))")]
        [InlineData("Route/RouteTypeId eq 5 and startswith(Route/Name,'a')")]
        [InlineData("Route/RouteTypeId eq 5 or Route/RouteTypeId gt 1")]
        [InlineData("Route/Description eq 'x'")]
        public void AnythingElseIsRefused(string filter)
        {
            Assert.False(RoutesOdataController.IsAllowedFilter(filter));
        }

        [Fact]
        public void EdmModelHasNoAccountTypes()
        {
            var model = Startup.GetEdmModel();
            var typeNames = model.SchemaElements.Select(e => e.Name).ToList();
            Assert.DoesNotContain("User", typeNames);
            Assert.DoesNotContain("RefreshToken", typeNames);
        }
    }
}
