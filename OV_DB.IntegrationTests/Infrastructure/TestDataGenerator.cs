using Bogus;
using OVDB_database.Models;
using System;
using System.Collections.Generic;

namespace OV_DB.IntegrationTests.Infrastructure
{
    public static class TestDataGenerator
    {
        public static Faker<User> UserFaker => new Faker<User>()
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.Password, f => BCrypt.Net.BCrypt.HashPassword("Test123!"))
            .RuleFor(u => u.Guid, f => Guid.NewGuid())
            .RuleFor(u => u.IsAdmin, f => false)
            .RuleFor(u => u.LastLogin, f => f.Date.Recent());

        public static Faker<Map> MapFaker => new Faker<Map>()
            .RuleFor(m => m.Name, f => f.Address.City())
            .RuleFor(m => m.NameNL, f => f.Address.City())
            .RuleFor(m => m.MapGuid, f => Guid.NewGuid())
            .RuleFor(m => m.Default, f => false)
            .RuleFor(m => m.OrderNr, f => f.Random.Int(1, 100));

        public static Faker<RouteType> RouteTypeFaker => new Faker<RouteType>()
            .RuleFor(rt => rt.Name, f => f.PickRandom("Train", "Bus", "Tram", "Metro", "Ferry"))
            .RuleFor(rt => rt.NameNL, f => f.PickRandom("Trein", "Bus", "Tram", "Metro", "Veerboot"))
            .RuleFor(rt => rt.Colour, f => f.Internet.Color())
            .RuleFor(rt => rt.OrderNr, f => f.Random.Int(1, 10));

        public static Faker<Route> RouteFaker(int routeTypeId) => new Faker<Route>()
            .RuleFor(r => r.Name, f => f.Address.StreetName())
            .RuleFor(r => r.NameNL, f => f.Address.StreetName())
            .RuleFor(r => r.From, f => f.Address.City())
            .RuleFor(r => r.To, f => f.Address.City())
            .RuleFor(r => r.RouteTypeId, routeTypeId)
            .RuleFor(r => r.Share, f => Guid.NewGuid())
            .RuleFor(r => r.LineNumber, f => f.Random.Int(1, 999).ToString())
            .RuleFor(r => r.CalculatedDistance, f => f.Random.Double(1, 500))
            .RuleFor(r => r.OperatingCompany, f => f.Company.CompanyName());

        public static Faker<RouteInstance> RouteInstanceFaker(int routeId) => new Faker<RouteInstance>()
            .RuleFor(ri => ri.RouteId, routeId)
            .RuleFor(ri => ri.Date, f => f.Date.Past(2))
            .RuleFor(ri => ri.StartTime, f => f.Date.Recent())
            .RuleFor(ri => ri.EndTime, (f, ri) => ri.StartTime.HasValue ? ri.StartTime.Value.AddHours(f.Random.Double(0.5, 5)) : (DateTime?)null)
            .RuleFor(ri => ri.DurationHours, (f, ri) =>
            {
                if (ri.StartTime.HasValue && ri.EndTime.HasValue)
                    return (ri.EndTime.Value - ri.StartTime.Value).TotalHours;
                return null;
            });

        public static Faker<Station> StationFaker => new Faker<Station>()
            .RuleFor(s => s.Name, f => f.Address.City() + " Station")
            .RuleFor(s => s.OsmId, f => f.Random.Long(1000000, 9999999))
            .RuleFor(s => s.Lattitude, f => f.Address.Latitude())
            .RuleFor(s => s.Longitude, f => f.Address.Longitude())
            .RuleFor(s => s.Network, f => f.Company.CompanyName())
            .RuleFor(s => s.Operator, f => f.Company.CompanyName())
            .RuleFor(s => s.Hidden, f => false)
            .RuleFor(s => s.Special, f => false);

        public static User CreateTestUser(string email = null, bool isAdmin = false)
        {
            var user = UserFaker.Generate();
            if (!string.IsNullOrEmpty(email))
                user.Email = email;
            user.IsAdmin = isAdmin;
            return user;
        }

        public static Map CreateTestMap(int userId, string name = null)
        {
            var map = MapFaker.Generate();
            map.UserId = userId;
            if (!string.IsNullOrEmpty(name))
                map.Name = name;
            return map;
        }

        public static RouteType CreateTestRouteType(int userId)
        {
            var routeType = RouteTypeFaker.Generate();
            routeType.UserId = userId;
            return routeType;
        }

        public static Route CreateTestRoute(int routeTypeId, List<int> mapIds = null)
        {
            var route = RouteFaker(routeTypeId).Generate();
            if (mapIds != null && mapIds.Count > 0)
            {
                route.RouteMaps = new List<RouteMap>();
                foreach (var mapId in mapIds)
                {
                    route.RouteMaps.Add(new RouteMap { MapId = mapId, RouteId = route.RouteId });
                }
            }
            return route;
        }

        public static RouteInstance CreateTestRouteInstance(int routeId, DateTime? date = null)
        {
            var instance = RouteInstanceFaker(routeId).Generate();
            if (date.HasValue)
                instance.Date = date.Value;
            return instance;
        }
    }
}
