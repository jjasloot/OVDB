using OV_DB.Models;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OV_DB.Mappings
{
    public static class MappingExtensions
    {
        public static IQueryable<RouteDTO> SelectToRouteDTO(this IQueryable<OVDB_database.Models.Route> query)
        {
            return query.Select(r => new RouteDTO
            {
                RouteId = r.RouteId,
                Name = r.Name,
                Description = r.Description,
                NameNL = r.NameNL,
                DescriptionNL = r.DescriptionNL,
                From = r.From,
                To = r.To,
                OverrideColour = r.OverrideColour,
                LineNumber = r.LineNumber,
                OperatingCompany = r.OperatingCompany,
                RouteTypeId = r.RouteTypeId,
                RouteType = r.RouteType,
                CalculatedDistance = r.CalculatedDistance,
                OverrideDistance = r.OverrideDistance,
                Share = r.Share,
                FirstDateTime = r.RouteInstances.Max(d => (DateTime?)d.Date),
                RouteInstancesCount = r.RouteInstances.Count,
                MinAverageSpeedKmh = r.RouteInstances
                    .Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0)
                    .Select(ri => (double?)((ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance) / ri.DurationHours))
                    .Min(),
                MaxAverageSpeedKmh = r.RouteInstances
                    .Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0)
                    .Select(ri => (double?)((ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance) / ri.DurationHours))
                    .Max(),
                RouteMaps = r.RouteMaps.Select(rm => new RouteMapDTO
                {
                    RouteMapId = rm.RouteMapId,
                    MapId = rm.MapId,
                    Name = rm.Map.Name,
                    NameNL = rm.Map.NameNL
                }).ToList(),
                Regions = r.Regions.Select(rg => new RegionMinimalDTO
                {
                    Id = rg.Id,
                    Name = rg.Name,
                    NameNL = rg.NameNL,
                    OriginalName = rg.OriginalName
                }).ToList(),
                OperatorIds = r.Operators.Select(o => o.Id).ToList()
            });
        }

        public static IQueryable<RouteInstanceListDTO> SelectToRouteInstanceListDTO(this IQueryable<RouteInstance> query)
        {
            return query.Select(ri => new RouteInstanceListDTO
            {
                RouteInstanceId = ri.RouteInstanceId,
                RouteId = ri.RouteId,
                Date = ri.Date,
                StartTime = ri.StartTime,
                EndTime = ri.EndTime,
                ScheduledStartTime = ri.ScheduledStartTime,
                ScheduledEndTime = ri.ScheduledEndTime,
                DepartureDelayMinutes = null, // computed in C# after materialization via ComputeDelayFields()
                ArrivalDelayMinutes = null,    // computed in C# after materialization via ComputeDelayFields()
                DurationHours = ri.DurationHours,
                AverageSpeedKmh = ri.DurationHours.HasValue && ri.DurationHours > 0
                    ? (ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance) / ri.DurationHours.Value
                    : (double?)null,
                RouteInstanceProperties = ri.RouteInstanceProperties.Select(p => new RouteInstancePropertyDTO
                {
                    RouteInstancePropertyId = p.RouteInstancePropertyId,
                    Key = p.Key,
                    Value = p.Value,
                    Bool = p.Bool
                }).ToList(),
                RouteInstanceMaps = ri.RouteInstanceMaps.Select(rim => new RouteInstanceMapDTO
                {
                    MapId = rim.MapId,
                    Name = rim.Map.Name,
                    NameNL = rim.Map.NameNL
                }).ToList(),
                RouteName = ri.Route.Name,
                RouteDescription = ri.Route.Description,
                RouteType = ri.Route.RouteType != null ? new RouteTypeDTO
                {
                    Name = ri.Route.RouteType.Name,
                    NameNL = ri.Route.RouteType.NameNL
                } : null,
                RouteTypeColour = ri.Route.RouteType != null ? ri.Route.RouteType.Colour : null,
                From = ri.Route.From,
                To = ri.Route.To,
                Distance = ri.Route.OverrideDistance ?? ri.Route.CalculatedDistance,
                RouteOverrideColour = ri.Route.OverrideColour,
                Share = ri.Route.Share
            });
        }

        public static List<RouteInstanceListDTO> ComputeDelayFields(this List<RouteInstanceListDTO> dtos)
        {
            foreach (var dto in dtos)
            {
                dto.DepartureDelayMinutes = dto.StartTime.HasValue && dto.ScheduledStartTime.HasValue
                    ? (double?)(dto.StartTime.Value - dto.ScheduledStartTime.Value).TotalMinutes
                    : null;
                dto.ArrivalDelayMinutes = dto.EndTime.HasValue && dto.ScheduledEndTime.HasValue
                    ? (double?)(dto.EndTime.Value - dto.ScheduledEndTime.Value).TotalMinutes
                    : null;
            }
            return dtos;
        }

        public static RouteWithInstancesDTO MapToRouteWithInstancesDTO(OVDB_database.Models.Route route)
        {
            return new RouteWithInstancesDTO
            {
                RouteId = route.RouteId,
                Name = route.Name,
                Description = route.Description,
                NameNL = route.NameNL,
                DescriptionNL = route.DescriptionNL,
                From = route.From,
                To = route.To,
                OverrideColour = route.OverrideColour,
                LineNumber = route.LineNumber,
                OperatingCompany = route.OperatingCompany,
                RouteTypeId = route.RouteTypeId,
                RouteType = route.RouteType,
                CalculatedDistance = route.CalculatedDistance,
                OverrideDistance = route.OverrideDistance,
                Share = route.Share,
                FirstDateTime = route.RouteInstances?.Max(d => (DateTime?)d.Date),
                RouteInstancesCount = route.RouteInstances?.Count ?? 0,
                MinAverageSpeedKmh = (route.RouteInstances ?? [])
                    .Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0)
                    .Select(ri => (double?)((route.OverrideDistance ?? route.CalculatedDistance) / ri.DurationHours!.Value))
                    .DefaultIfEmpty()
                    .Min(),
                MaxAverageSpeedKmh = (route.RouteInstances ?? [])
                    .Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0)
                    .Select(ri => (double?)((route.OverrideDistance ?? route.CalculatedDistance) / ri.DurationHours!.Value))
                    .DefaultIfEmpty()
                    .Max(),
                RouteMaps = route.RouteMaps?.Select(rm => new RouteMapDTO
                {
                    RouteMapId = rm.RouteMapId,
                    MapId = rm.MapId,
                    Name = rm.Map?.Name,
                    NameNL = rm.Map?.NameNL
                }).ToList(),
                Regions = route.Regions?.Select(rg => new RegionMinimalDTO
                {
                    Id = rg.Id,
                    Name = rg.Name,
                    NameNL = rg.NameNL,
                    OriginalName = rg.OriginalName
                }).ToList(),
                OperatorIds = route.Operators?.Select(o => o.Id).ToList(),
                RouteInstances = route.RouteInstances?.Select(ri => new RouteInstanceDTO
                {
                    RouteInstanceId = ri.RouteInstanceId,
                    RouteId = ri.RouteId,
                    Date = ri.Date,
                    StartTime = ri.StartTime,
                    EndTime = ri.EndTime,
                    ScheduledStartTime = ri.ScheduledStartTime,
                    ScheduledEndTime = ri.ScheduledEndTime,
                    DepartureDelayMinutes = ri.DepartureDelayMinutes,
                    ArrivalDelayMinutes = ri.ArrivalDelayMinutes,
                    DurationHours = ri.DurationHours,
                    AverageSpeedKmh = ri.GetAverageSpeedKmh(),
                    RouteInstanceProperties = ri.RouteInstanceProperties?.Select(p => new RouteInstancePropertyDTO
                    {
                        RouteInstancePropertyId = p.RouteInstancePropertyId,
                        Key = p.Key,
                        Value = p.Value,
                        Bool = p.Bool
                    }).ToList(),
                    RouteInstanceMaps = ri.RouteInstanceMaps?.Select(rim => new RouteInstanceMapDTO
                    {
                        MapId = rim.MapId,
                        Name = rim.Map?.Name,
                        NameNL = rim.Map?.NameNL
                    }).ToList()
                }).ToList()
            };
        }

        public static IQueryable<StationMapDTO> SelectToStationMapDTO(this IQueryable<StationGrouping> query)
        {
            return query.Select(m => new StationMapDTO
            {
                Id = m.Id,
                Name = m.Name,
                NameNL = m.NameNL,
                MapGuid = m.MapGuid.ToString(),
                SharingLinkName = m.SharingLinkName,
                RegionIds = m.Regions.Select(r => r.Id).ToList()
            });
        }

        public static IQueryable<OperatorDTO> SelectToOperatorDTO(this IQueryable<Operator> query)
        {
            return query.Select(o => new OperatorDTO
            {
                Id = o.Id,
                Names = o.Names,
                RunsTrainsInRegions = o.RunsTrainsInRegions.Select(r => new RegionMinimalDTO
                {
                    Id = r.Id,
                    Name = r.Name,
                    NameNL = r.NameNL,
                    OriginalName = r.OriginalName
                }).ToList(),
                RestrictToRegions = o.RestrictToRegions.Select(r => new RegionMinimalDTO
                {
                    Id = r.Id,
                    Name = r.Name,
                    NameNL = r.NameNL,
                    OriginalName = r.OriginalName
                }).ToList(),
                LogoFilePath = o.LogoFilePath
            });
        }

        public static IQueryable<RegionIntermediate> SelectToRegionIntermediate(this IQueryable<Region> query)
        {
            return query.Select(r => new RegionIntermediate
            {
                Id = r.Id,
                Name = r.Name,
                NameNL = r.NameNL,
                OriginalName = r.OriginalName,
                OsmRelationId = r.OsmRelationId,
                IsoCode = r.IsoCode,
                ParentRegionId = r.ParentRegionId
            });
        }
    }
}
