using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public DbSet<Operator> Operators { get; set; }
        public DbSet<TrawellingIgnoredStatus> TrawellingIgnoredStatuses { get; set; }
        public DbSet<TrawellingStation> TrawellingStations { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

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

            modelBuilder.Entity<Station>().Property(s => s.Hidden).HasDefaultValue(false);
            modelBuilder.Entity<Station>().Property(s => s.Special).HasDefaultValue(false);

            modelBuilder.Entity<Operator>(entity =>
             {
                 entity.HasKey(e => e.Id);
                 entity.Property(e => e.Names)
                     .HasConversion(
                         v => string.Join(',', v),
                         v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
                     .Metadata.SetValueComparer(
                         new ValueComparer<List<string>>(
                             (c1, c2) => c1.SequenceEqual(c2),
                             c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                             c => c.ToList()));
             });

            modelBuilder.Entity<Operator>().HasMany(o => o.RunsTrainsInRegions).WithMany(r => r.OperatorsRunningTrains);
            modelBuilder.Entity<Operator>().HasMany(o => o.RestrictToRegions).WithMany(r => r.OperatorsRestrictedToRegion);

            // RefreshToken configuration
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => e.IsRevoked);
                entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
                entity.Property(e => e.DeviceInfo).HasMaxLength(500);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Guid).IsUnique();
                entity.HasIndex(e => e.TelegramUserId).IsUnique()
                    .HasFilter("[TelegramUserId] IS NOT NULL");
            });

            // InviteCode entity configuration
            modelBuilder.Entity<InviteCode>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // StationMap entity configuration
            modelBuilder.Entity<StationMap>(entity =>
            {
                entity.HasIndex(e => e.MapGuid).IsUnique();
            });

            // StationGrouping entity configuration
            modelBuilder.Entity<StationGrouping>(entity =>
            {
                entity.HasIndex(e => e.MapGuid).IsUnique();
            });

            // RouteInstanceProperty entity configuration
            modelBuilder.Entity<RouteInstanceProperty>(entity =>
            {
                entity.HasIndex(e => new { e.RouteInstanceId, e.Key });
            });
        }


    }
}
