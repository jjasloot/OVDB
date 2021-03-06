﻿using AutoMapper;
using Microsoft.AspNetCore.Routing;
using OV_DB.Models;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<OVDB_database.Models.Route, RouteDTO>()
                .ForMember(dest => dest.FirstDateTime, ops => ops.MapFrom(r => r.RouteInstances.OrderByDescending(d => d.Date).FirstOrDefault().Date))
                .ForMember(dest => dest.RouteInstancesCount, ops => ops.MapFrom(r => r.RouteInstances.Count));

            CreateMap<RouteMap, RouteMapDTO>()
                .ForMember(dest => dest.Name, ops => ops.MapFrom(rm => rm.Map.Name))
                .ForMember(dest => dest.NameNL, ops => ops.MapFrom(rm => rm.Map.NameNL));
            CreateMap<StationMap, StationMapDTO>()
                .ForMember(dest => dest.StationMapCountries, ops => ops.MapFrom(src => src.StationMapCountries));
            CreateMap<StationMapCountry, StationMapCountryDTO>();
        }
    }
}
