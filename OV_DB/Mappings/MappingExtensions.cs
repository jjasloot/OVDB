using OV_DB.Models;
using OVDB_database.Models;
using System.Collections.Generic;
using System.Linq;

namespace OV_DB.Mappings
{
    public static class MappingExtensions
    {
        public static RouteDTO ToRouteDTO(this Route route)
        {
            if (route == null) return null;
            var routeInstances = route.RouteInstances ?? [];
            var instancesWithDuration = routeInstances.Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0).ToList();
            double? minSpeed = null;
            double? maxSpeed = null;
            if (instancesWithDuration.Count != 0)
            {
                var speeds = instancesWithDuration.Select(ri => (route.OverrideDistance ?? route.CalculatedDistance) / ri.DurationHours.Value).ToList();
                minSpeed = speeds.Min();
                maxSpeed = speeds.Max();
            }
            return new RouteDTO
            {
                RouteId = route.RouteId,
                Name = route.Name,
                NameNL = route.NameNL,
                Description = route.Description,
                DescriptionNL = route.DescriptionNL,
                LineNumber = route.LineNumber,
                OverrideColour = route.OverrideColour,
                OperatingCompany = route.OperatingCompany,
                From = route.From,
                To = route.To,
                RouteTypeId = route.RouteTypeId,
                RouteType = route.RouteType,
                OverrideDistance = route.OverrideDistance,
                CalculatedDistance = route.CalculatedDistance,
                FirstDateTime = routeInstances.OrderByDescending(d => d.Date).FirstOrDefault()?.Date,
                RouteInstancesCount = routeInstances.Count,
                MinAverageSpeedKmh = minSpeed,
                MaxAverageSpeedKmh = maxSpeed,
                OperatorIds = route.Operators?.Select(o => o.Id).ToList() ?? [],
                RouteMaps = route.RouteMaps?.Select(rm => rm.ToRouteMapDTO()).ToList() ?? []
            };
        }

        public static RouteWithInstancesDTO ToRouteWithInstancesDTO(this Route route)
        {
            if (route == null) return null;
            var dto = route.ToRouteDTO();
            return new RouteWithInstancesDTO
            {
                RouteId = dto.RouteId,
                Name = dto.Name,
                NameNL = dto.NameNL,
                Description = dto.Description,
                DescriptionNL = dto.DescriptionNL,
                LineNumber = dto.LineNumber,
                OverrideColour = dto.OverrideColour,
                OperatingCompany = dto.OperatingCompany,
                From = dto.From,
                To = dto.To,
                RouteTypeId = dto.RouteTypeId,
                RouteType = dto.RouteType,
                OverrideDistance = dto.OverrideDistance,
                CalculatedDistance = dto.CalculatedDistance,
                FirstDateTime = dto.FirstDateTime,
                RouteInstancesCount = dto.RouteInstancesCount,
                MinAverageSpeedKmh = dto.MinAverageSpeedKmh,
                MaxAverageSpeedKmh = dto.MaxAverageSpeedKmh,
                OperatorIds = dto.OperatorIds,
                RouteMaps = dto.RouteMaps,
                RouteInstances = route.RouteInstances?.Select(ri => ri.ToRouteInstanceDTO()).ToList() ?? []
            };
        }

        public static RouteInstanceDTO ToRouteInstanceDTO(this RouteInstance ri)
        {
            if (ri == null) return null;
            return new RouteInstanceDTO
            {
                RouteInstanceId = ri.RouteInstanceId,
                RouteId = ri.RouteId,
                Date = ri.Date,
                StartTime = ri.StartTime,
                EndTime = ri.EndTime,
                DurationHours = ri.DurationHours,
                AverageSpeedKmh = ri.GetAverageSpeedKmh(),
                RouteInstanceProperties = ri.RouteInstanceProperties?.Select(p => p.ToRouteInstancePropertyDTO()).ToList() ?? [],
                RouteInstanceMaps = ri.RouteInstanceMaps?.Select(m => m.ToRouteInstanceMapDTO()).ToList() ?? []
            };
        }

        public static RouteInstancePropertyDTO ToRouteInstancePropertyDTO(this RouteInstanceProperty p)
        {
            if (p == null) return null;
            return new RouteInstancePropertyDTO
            {
                RouteInstancePropertyId = (int?)p.RouteInstancePropertyId,
                Key = p.Key,
                Value = p.Value,
                Bool = p.Bool
            };
        }

        public static RouteInstanceMapDTO ToRouteInstanceMapDTO(this RouteInstanceMap rim)
        {
            if (rim == null) return null;
            return new RouteInstanceMapDTO
            {
                MapId = rim.MapId,
                Name = rim.Map?.Name,
                NameNL = rim.Map?.NameNL
            };
        }

        public static RouteMapDTO ToRouteMapDTO(this RouteMap rm)
        {
            if (rm == null) return null;
            return new RouteMapDTO
            {
                MapId = rm.MapId,
                Name = rm.Map?.Name,
                NameNL = rm.Map?.NameNL
            };
        }

        public static StationMapDTO ToStationMapDTO(this StationGrouping sg)
        {
            if (sg == null) return null;
            return new StationMapDTO
            {
                Id = sg.Id,
                Name = sg.Name,
                NameNL = sg.NameNL,
                SharingLinkName = sg.SharingLinkName,
                MapGuid = sg.MapGuid.ToString(),
                RegionIds = sg.Regions?.Select(r => r.Id).ToList() ?? []
            };
        }

        public static RegionIntermediate ToRegionIntermediate(this Region r)
        {
            if (r == null) return null;
            return new RegionIntermediate
            {
                Id = r.Id,
                Name = r.Name,
                NameNL = r.NameNL,
                OriginalName = r.OriginalName,
                OsmRelationId = r.OsmRelationId,
                ParentRegionId = r.ParentRegionId
            };
        }

        public static RequestForUserDTO ToRequestForUserDTO(this Request r)
        {
            if (r == null) return null;
            return new RequestForUserDTO
            {
                Id = r.Id,
                Message = r.Message,
                Created = r.Created,
                Response = r.Response,
                Responded = r.Responded
            };
        }

        public static RequestForAdminDTO ToRequestForAdminDTO(this Request r)
        {
            if (r == null) return null;
            return new RequestForAdminDTO
            {
                Id = r.Id,
                Message = r.Message,
                Created = r.Created,
                Response = r.Response,
                Responded = r.Responded,
                UserEmail = r.User?.Email
            };
        }

        public static OperatorDTO ToOperatorDTO(this Operator op)
        {
            if (op == null) return null;
            return new OperatorDTO
            {
                Id = op.Id,
                Names = op.Names
            };
        }

        public static IQueryable<RouteDTO> ToRouteDTOs(this IQueryable<Route> query)
        {
            return query.Select(route => new RouteDTO
            {
                RouteId = route.RouteId,
                Name = route.Name,
                NameNL = route.NameNL,
                Description = route.Description,
                DescriptionNL = route.DescriptionNL,
                LineNumber = route.LineNumber,
                OverrideColour = route.OverrideColour,
                OperatingCompany = route.OperatingCompany,
                From = route.From,
                To = route.To,
                RouteTypeId = route.RouteTypeId,
                RouteType = route.RouteType,
                OverrideDistance = route.OverrideDistance,
                CalculatedDistance = route.CalculatedDistance,
                FirstDateTime = route.RouteInstances.OrderByDescending(d => d.Date).FirstOrDefault().Date,
                RouteInstancesCount = route.RouteInstances.Count,
                MinAverageSpeedKmh = route.RouteInstances.Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0).Any()
                    ? route.RouteInstances.Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0)
                        .Select(ri => (route.OverrideDistance ?? route.CalculatedDistance) / ri.DurationHours).Min()
                    : (double?)null,
                MaxAverageSpeedKmh = route.RouteInstances.Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0).Any()
                    ? route.RouteInstances.Where(ri => ri.DurationHours.HasValue && ri.DurationHours > 0)
                        .Select(ri => (route.OverrideDistance ?? route.CalculatedDistance) / ri.DurationHours).Max()
                    : (double?)null,
                OperatorIds = route.Operators.Select(o => o.Id).ToList(),
                RouteMaps = route.RouteMaps.Select(rm => new RouteMapDTO
                {
                    MapId = rm.MapId,
                    Name = rm.Map.Name,
                    NameNL = rm.Map.NameNL
                }).ToList()
            });
        }

        public static IQueryable<RegionIntermediate> ToRegionIntermediates(this IQueryable<Region> query)
        {
            return query.Select(r => new RegionIntermediate
            {
                Id = r.Id,
                Name = r.Name,
                NameNL = r.NameNL,
                OriginalName = r.OriginalName,
                OsmRelationId = r.OsmRelationId,
                ParentRegionId = r.ParentRegionId
            });
        }

        public static IQueryable<OperatorDTO> ToOperatorDTOs(this IQueryable<Operator> query)
        {
            return query.Select(op => new OperatorDTO
            {
                Id = op.Id,
                Names = op.Names
            });
        }

        public static IQueryable<StationMapDTO> ToStationMapDTOs(this IQueryable<StationGrouping> query)
        {
            return query.Select(sg => new StationMapDTO
            {
                Id = sg.Id,
                Name = sg.Name,
                NameNL = sg.NameNL,
                SharingLinkName = sg.SharingLinkName,
                MapGuid = sg.MapGuid.ToString(),
                RegionIds = sg.Regions.Select(r => r.Id).ToList()
            });
        }
    }
}
