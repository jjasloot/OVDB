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
        [InlineData("((Date ge 2023-01-01 and Date lt 2024-01-01) or (Date ge 2025-01-01 and Date lt 2026-01-01))")]
        [InlineData("(Date ge 2024-01-01 and Date lt 2024-06-01)  and (Route/RouteTypeId eq 1 or Route/RouteTypeId eq 2)")]
        [InlineData("(Route/RouteTypeId eq 5) and (Route/Operators/any(o: o/Id eq 3) or Route/Operators/any(o: o/Id eq 17))")]
        [InlineData("(RouteInstanceProperties/any(p: p/Key eq 'Voertuig type' and contains(p/Value,'ICE')) or RouteInstanceProperties/any(p: p/Key eq 'Conducteur gezien' and p/Bool eq true))")]
        [InlineData("RouteInstanceProperties/any(p: p/Key eq 'Wagon' and contains(p/Value,'d''Artagnan'))")]
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
        [InlineData("Route/Operators/any(o: o/Id eq 3 and o/Routes/any(r: r/Name eq 'x'))")]
        // A value that tries to close its string and append a clause of its own.
        [InlineData("RouteInstanceProperties/any(p: p/Key eq 'k' and contains(p/Value,'x')) or startswith(Route/Name,'a') or contains(p/Value,'y'))")]
        [InlineData("RouteInstanceProperties/any(p: p/Key eq 'k' and contains(p/Value,'x') or Route/RouteMaps/any(rm: rm/Map/UserId eq 1))")]
        [InlineData("RouteInstanceProperties/any(p: p/Key eq 'k' and p/RouteInstance/Route/Name eq 'x')")]
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
