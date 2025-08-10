using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using OV_DB.Services;
using OV_DB.Extensions;
using OVDB_database.Models;

var services = new ServiceCollection();
services.AddTransient<ITimezoneService, TimezoneService>();

var serviceProvider = services.BuildServiceProvider();
var timezoneService = serviceProvider.GetRequiredService<ITimezoneService>();

Console.WriteLine("Testing OVDB Time Zone Functionality");
Console.WriteLine("=====================================");

// Test 1: Basic timezone lookup
Console.WriteLine("\nTest 1: Timezone lookup for major cities");
var amsterdamTz = timezoneService.GetTimezoneId(52.3676, 4.9041);
var newYorkTz = timezoneService.GetTimezoneId(40.7128, -74.0060);
var tokyoTz = timezoneService.GetTimezoneId(35.6762, 139.6503);

Console.WriteLine($"Amsterdam: {amsterdamTz}");
Console.WriteLine($"New York: {newYorkTz}");
Console.WriteLine($"Tokyo: {tokyoTz}");

// Test 2: Create a sample route with LineString
Console.WriteLine("\nTest 2: Sample route from Amsterdam to Paris");
var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);
var coordinates = new[]
{
    new Coordinate(4.9041, 52.3676),  // Amsterdam
    new Coordinate(4.3517, 50.8503),  // Brussels
    new Coordinate(2.3522, 48.8566)   // Paris
};
var lineString = geometryFactory.CreateLineString(coordinates);

var (startTz, endTz) = timezoneService.GetTimezones(lineString);
Console.WriteLine($"Start timezone: {startTz}");
Console.WriteLine($"End timezone: {endTz}");

// Test 3: Create a sample RouteInstance with times
Console.WriteLine("\nTest 3: Sample train trip with times and speed calculation");
var route = new Route
{
    RouteId = 1,
    Name = "Amsterdam - Paris Express",
    LineString = lineString,
    CalculatedDistance = 500, // km
    OverrideDistance = null
};

var routeInstance = new RouteInstance
{
    RouteInstanceId = 1,
    RouteId = 1,
    Route = route,
    Date = DateTime.Today,
    StartTime = new DateTime(2025, 8, 10, 10, 30, 0), // 10:30 AM local time
    EndTime = new DateTime(2025, 8, 10, 14, 15, 0)    // 2:15 PM local time
};

var averageSpeed = routeInstance.CalculateAverageSpeed(timezoneService);
var duration = routeInstance.GetDurationInHours(timezoneService);

Console.WriteLine($"Trip date: {routeInstance.Date:yyyy-MM-dd}");
Console.WriteLine($"Start time: {routeInstance.StartTime:HH:mm}");
Console.WriteLine($"End time: {routeInstance.EndTime:HH:mm}");
Console.WriteLine($"Distance: {route.CalculatedDistance} km");
Console.WriteLine($"Duration: {duration:F2} hours");
Console.WriteLine($"Average speed: {averageSpeed:F1} km/h");

// Test 4: Multi-day trip example (Trans-Siberian Express)
Console.WriteLine("\nTest 4: Multi-day trip (Moscow to Vladivostok)");
var transSiberianRoute = new Route
{
    RouteId = 2,
    Name = "Trans-Siberian Express",
    LineString = geometryFactory.CreateLineString(new[]
    {
        new Coordinate(37.6176, 55.7558),   // Moscow
        new Coordinate(131.8869, 43.1198)   // Vladivostok
    }),
    CalculatedDistance = 9289 // km - actual Trans-Siberian distance
};

var transSiberianTrip = new RouteInstance
{
    RouteInstanceId = 2,
    RouteId = 2,
    Route = transSiberianRoute,
    Date = new DateTime(2025, 8, 10),
    StartTime = new DateTime(2025, 8, 10, 21, 30, 0), // 9:30 PM Moscow time
    EndTime = new DateTime(2025, 8, 17, 16, 25, 0)    // 4:25 PM Vladivostok time (7 days later)
};

var transSiberianSpeed = transSiberianTrip.CalculateAverageSpeed(timezoneService);
var transSiberianDuration = transSiberianTrip.GetDurationInHours(timezoneService);

Console.WriteLine($"Route: {transSiberianRoute.Name}");
Console.WriteLine($"Start: {transSiberianTrip.StartTime:yyyy-MM-dd HH:mm} (Moscow time)");
Console.WriteLine($"End: {transSiberianTrip.EndTime:yyyy-MM-dd HH:mm} (Vladivostok time)");
Console.WriteLine($"Distance: {transSiberianRoute.CalculatedDistance} km");
Console.WriteLine($"Duration: {transSiberianDuration:F1} hours ({transSiberianDuration/24:F1} days)");
Console.WriteLine($"Average speed: {transSiberianSpeed:F1} km/h");

Console.WriteLine("\nTests completed successfully!");