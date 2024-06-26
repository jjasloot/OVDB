﻿using Microsoft.EntityFrameworkCore;
using OVDB_database.Models;
using System;

namespace OVDB_database.Database
{
    public class OVDBDatabaseContext : DbContext
    {

        public OVDBDatabaseContext() : base()
        {
        }
        public OVDBDatabaseContext(DbContextOptions<OVDBDatabaseContext> dbOptions) : base(dbOptions)
        {
        }
        public DbSet<Route> Routes { get; set; }
        public DbSet<Map> Maps { get; set; }
        public DbSet<RouteMap> RoutesMaps { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<RouteCountry> RoutesCountries { get; set; }
        public DbSet<RouteType> RouteTypes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<InviteCode> InviteCodes { get; set; }
        public DbSet<RouteInstance> RouteInstances { get; set; }
        public DbSet<RouteInstanceProperty> RouteInstanceProperties { get; set; }
        public DbSet<StationCountry> StationCountries { get; set; }
        public DbSet<Station> Stations { get; set; }
        public DbSet<StationVisit> StationVisits { get; set; }
        public DbSet<StationMap> StationMaps { get; set; }
        public DbSet<StationMapCountry> StationMapCountries { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<StationGrouping> StationGroupings { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!options.IsConfigured)
            {
                throw new Exception("Not Configured");
            }
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RouteCountry>().HasOne(rc => rc.Route).WithMany(r => r.RouteCountries).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Station>().Property(s => s.Hidden).HasDefaultValue(false);
            modelBuilder.Entity<Station>().Property(s => s.Special).HasDefaultValue(false);

        }
    }
}
