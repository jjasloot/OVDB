﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using OVDB_database.Database;

#nullable disable

namespace OVDB_database.Migrations
{
    [DbContext(typeof(OVDBDatabaseContext))]
    [Migration("20240322191505_Add OriginalName to region")]
    partial class AddOriginalNametoregion
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("OVDB_database.Models.Country", b =>
                {
                    b.Property<int>("CountryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("CountryId"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext");

                    b.Property<int>("OrderNr")
                        .HasColumnType("int");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("CountryId");

                    b.HasIndex("UserId");

                    b.ToTable("Countries");
                });

            modelBuilder.Entity("OVDB_database.Models.InviteCode", b =>
                {
                    b.Property<int>("InviteCodeId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("InviteCodeId"));

                    b.Property<string>("Code")
                        .HasColumnType("longtext");

                    b.Property<int?>("CreatedByUserId")
                        .HasColumnType("int");

                    b.Property<bool>("DoesNotExpire")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsUsed")
                        .HasColumnType("tinyint(1)");

                    b.Property<int?>("UserId")
                        .HasColumnType("int");

                    b.HasKey("InviteCodeId");

                    b.HasIndex("CreatedByUserId");

                    b.HasIndex("UserId");

                    b.ToTable("InviteCodes");
                });

            modelBuilder.Entity("OVDB_database.Models.Map", b =>
                {
                    b.Property<int>("MapId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("MapId"));

                    b.Property<bool>("Default")
                        .HasColumnType("tinyint(1)");

                    b.Property<Guid>("MapGuid")
                        .HasColumnType("char(36)");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext");

                    b.Property<int>("OrderNr")
                        .HasColumnType("int");

                    b.Property<string>("SharingLinkName")
                        .HasColumnType("longtext");

                    b.Property<bool>("ShowRouteInfo")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("ShowRouteOutline")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("MapId");

                    b.HasIndex("UserId");

                    b.ToTable("Maps");
                });

            modelBuilder.Entity("OVDB_database.Models.Region", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<MultiPolygon>("Geometry")
                        .HasColumnType("multipolygon");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext");

                    b.Property<string>("OriginalName")
                        .HasColumnType("longtext");

                    b.Property<long>("OsmRelationId")
                        .HasColumnType("bigint");

                    b.Property<int?>("ParentRegionId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("ParentRegionId");

                    b.ToTable("Regions");
                });

            modelBuilder.Entity("OVDB_database.Models.Route", b =>
                {
                    b.Property<int>("RouteId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("RouteId"));

                    b.Property<double>("CalculatedDistance")
                        .HasColumnType("double");

                    b.Property<string>("Coordinates")
                        .HasMaxLength(1048576000)
                        .HasColumnType("longtext");

                    b.Property<string>("Description")
                        .HasColumnType("longtext");

                    b.Property<string>("DescriptionNL")
                        .HasColumnType("longtext");

                    b.Property<DateTime?>("FirstDateTime")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("From")
                        .HasColumnType("longtext");

                    b.Property<string>("LineNumber")
                        .HasColumnType("longtext");

                    b.Property<LineString>("LineString")
                        .HasColumnType("linestring");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext");

                    b.Property<string>("OperatingCompany")
                        .HasColumnType("longtext");

                    b.Property<string>("OverrideColour")
                        .HasColumnType("longtext");

                    b.Property<double?>("OverrideDistance")
                        .HasColumnType("double");

                    b.Property<int?>("RouteTypeId")
                        .HasColumnType("int");

                    b.Property<Guid>("Share")
                        .HasColumnType("char(36)");

                    b.Property<string>("To")
                        .HasColumnType("longtext");

                    b.HasKey("RouteId");

                    b.HasIndex("RouteTypeId");

                    b.ToTable("Routes");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteCountry", b =>
                {
                    b.Property<long>("RouteCountryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("RouteCountryId"));

                    b.Property<int>("CountryId")
                        .HasColumnType("int");

                    b.Property<int>("RouteId")
                        .HasColumnType("int");

                    b.HasKey("RouteCountryId");

                    b.HasIndex("CountryId");

                    b.HasIndex("RouteId");

                    b.ToTable("RoutesCountries");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstance", b =>
                {
                    b.Property<int>("RouteInstanceId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("RouteInstanceId"));

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("RouteId")
                        .HasColumnType("int");

                    b.HasKey("RouteInstanceId");

                    b.HasIndex("Date");

                    b.HasIndex("RouteId");

                    b.ToTable("RouteInstances");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstanceMap", b =>
                {
                    b.Property<long>("RouteMapId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("RouteMapId"));

                    b.Property<int>("MapId")
                        .HasColumnType("int");

                    b.Property<int>("RouteInstanceId")
                        .HasColumnType("int");

                    b.HasKey("RouteMapId");

                    b.HasIndex("MapId");

                    b.HasIndex("RouteInstanceId");

                    b.ToTable("RouteInstanceMap");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstanceProperty", b =>
                {
                    b.Property<long>("RouteInstancePropertyId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("RouteInstancePropertyId"));

                    b.Property<bool?>("Bool")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Key")
                        .HasColumnType("longtext");

                    b.Property<int>("RouteInstanceId")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .HasColumnType("longtext");

                    b.HasKey("RouteInstancePropertyId");

                    b.HasIndex("RouteInstanceId");

                    b.ToTable("RouteInstanceProperties");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteMap", b =>
                {
                    b.Property<long>("RouteMapId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("RouteMapId"));

                    b.Property<int>("MapId")
                        .HasColumnType("int");

                    b.Property<int>("RouteId")
                        .HasColumnType("int");

                    b.HasKey("RouteMapId");

                    b.HasIndex("MapId");

                    b.HasIndex("RouteId");

                    b.ToTable("RoutesMaps");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteType", b =>
                {
                    b.Property<int>("TypeId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("TypeId"));

                    b.Property<string>("Colour")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext");

                    b.Property<int>("OrderNr")
                        .HasColumnType("int");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("TypeId");

                    b.HasIndex("UserId");

                    b.ToTable("RouteTypes");
                });

            modelBuilder.Entity("OVDB_database.Models.Station", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<double?>("Elevation")
                        .HasColumnType("double");

                    b.Property<bool>("Hidden")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(false);

                    b.Property<double>("Lattitude")
                        .HasColumnType("double");

                    b.Property<double>("Longitude")
                        .HasColumnType("double");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<string>("Network")
                        .HasColumnType("longtext");

                    b.Property<string>("Operator")
                        .HasColumnType("longtext");

                    b.Property<long>("OsmId")
                        .HasColumnType("bigint");

                    b.Property<bool>("Special")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(false);

                    b.Property<int>("StationCountryId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("StationCountryId");

                    b.ToTable("Stations");
                });

            modelBuilder.Entity("OVDB_database.Models.StationCountry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext");

                    b.Property<string>("OsmId")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("StationCountries");
                });

            modelBuilder.Entity("OVDB_database.Models.StationMap", b =>
                {
                    b.Property<int>("StationMapId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("StationMapId"));

                    b.Property<Guid>("MapGuid")
                        .HasColumnType("char(36)");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext");

                    b.Property<int>("OrderNr")
                        .HasColumnType("int");

                    b.Property<string>("SharingLinkName")
                        .HasColumnType("longtext");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("StationMapId");

                    b.HasIndex("UserId");

                    b.ToTable("StationMaps");
                });

            modelBuilder.Entity("OVDB_database.Models.StationMapCountry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<bool>("IncludeSpecials")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("StationCountryId")
                        .HasColumnType("int");

                    b.Property<int>("StationMapId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("StationCountryId");

                    b.HasIndex("StationMapId");

                    b.ToTable("StationMapCountries");
                });

            modelBuilder.Entity("OVDB_database.Models.StationVisit", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("Id"));

                    b.Property<int>("StationId")
                        .HasColumnType("int");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("StationId");

                    b.HasIndex("UserId");

                    b.ToTable("StationVisits");
                });

            modelBuilder.Entity("OVDB_database.Models.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Email")
                        .HasColumnType("longtext");

                    b.Property<Guid>("Guid")
                        .HasColumnType("char(36)");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTime>("LastLogin")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Password")
                        .HasColumnType("longtext");

                    b.Property<string>("RefreshToken")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("RegionRoute", b =>
                {
                    b.Property<int>("RegionsId")
                        .HasColumnType("int");

                    b.Property<int>("RoutesRouteId")
                        .HasColumnType("int");

                    b.HasKey("RegionsId", "RoutesRouteId");

                    b.HasIndex("RoutesRouteId");

                    b.ToTable("RegionRoute");
                });

            modelBuilder.Entity("OVDB_database.Models.Country", b =>
                {
                    b.HasOne("OVDB_database.Models.User", null)
                        .WithMany("Countries")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("OVDB_database.Models.InviteCode", b =>
                {
                    b.HasOne("OVDB_database.Models.User", "CreatedByUser")
                        .WithMany()
                        .HasForeignKey("CreatedByUserId");

                    b.HasOne("OVDB_database.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId");

                    b.Navigation("CreatedByUser");

                    b.Navigation("User");
                });

            modelBuilder.Entity("OVDB_database.Models.Map", b =>
                {
                    b.HasOne("OVDB_database.Models.User", "User")
                        .WithMany("Maps")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("OVDB_database.Models.Region", b =>
                {
                    b.HasOne("OVDB_database.Models.Region", "ParentRegion")
                        .WithMany("SubRegions")
                        .HasForeignKey("ParentRegionId");

                    b.Navigation("ParentRegion");
                });

            modelBuilder.Entity("OVDB_database.Models.Route", b =>
                {
                    b.HasOne("OVDB_database.Models.RouteType", "RouteType")
                        .WithMany("Routes")
                        .HasForeignKey("RouteTypeId");

                    b.Navigation("RouteType");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteCountry", b =>
                {
                    b.HasOne("OVDB_database.Models.Country", "Country")
                        .WithMany("RouteCountries")
                        .HasForeignKey("CountryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OVDB_database.Models.Route", "Route")
                        .WithMany("RouteCountries")
                        .HasForeignKey("RouteId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Country");

                    b.Navigation("Route");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstance", b =>
                {
                    b.HasOne("OVDB_database.Models.Route", "Route")
                        .WithMany("RouteInstances")
                        .HasForeignKey("RouteId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Route");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstanceMap", b =>
                {
                    b.HasOne("OVDB_database.Models.Map", "Map")
                        .WithMany()
                        .HasForeignKey("MapId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OVDB_database.Models.RouteInstance", "RouteInstance")
                        .WithMany("RouteInstanceMaps")
                        .HasForeignKey("RouteInstanceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Map");

                    b.Navigation("RouteInstance");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstanceProperty", b =>
                {
                    b.HasOne("OVDB_database.Models.RouteInstance", "RouteInstance")
                        .WithMany("RouteInstanceProperties")
                        .HasForeignKey("RouteInstanceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("RouteInstance");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteMap", b =>
                {
                    b.HasOne("OVDB_database.Models.Map", "Map")
                        .WithMany("RouteMaps")
                        .HasForeignKey("MapId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OVDB_database.Models.Route", "Route")
                        .WithMany("RouteMaps")
                        .HasForeignKey("RouteId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Map");

                    b.Navigation("Route");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteType", b =>
                {
                    b.HasOne("OVDB_database.Models.User", null)
                        .WithMany("RouteTypes")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("OVDB_database.Models.Station", b =>
                {
                    b.HasOne("OVDB_database.Models.StationCountry", "StationCountry")
                        .WithMany("Stations")
                        .HasForeignKey("StationCountryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("StationCountry");
                });

            modelBuilder.Entity("OVDB_database.Models.StationMap", b =>
                {
                    b.HasOne("OVDB_database.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("OVDB_database.Models.StationMapCountry", b =>
                {
                    b.HasOne("OVDB_database.Models.StationCountry", "StationCountry")
                        .WithMany("StationMapCountries")
                        .HasForeignKey("StationCountryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OVDB_database.Models.StationMap", "StationMap")
                        .WithMany("StationMapCountries")
                        .HasForeignKey("StationMapId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("StationCountry");

                    b.Navigation("StationMap");
                });

            modelBuilder.Entity("OVDB_database.Models.StationVisit", b =>
                {
                    b.HasOne("OVDB_database.Models.Station", "Station")
                        .WithMany("StationVisits")
                        .HasForeignKey("StationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OVDB_database.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Station");

                    b.Navigation("User");
                });

            modelBuilder.Entity("RegionRoute", b =>
                {
                    b.HasOne("OVDB_database.Models.Region", null)
                        .WithMany()
                        .HasForeignKey("RegionsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OVDB_database.Models.Route", null)
                        .WithMany()
                        .HasForeignKey("RoutesRouteId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("OVDB_database.Models.Country", b =>
                {
                    b.Navigation("RouteCountries");
                });

            modelBuilder.Entity("OVDB_database.Models.Map", b =>
                {
                    b.Navigation("RouteMaps");
                });

            modelBuilder.Entity("OVDB_database.Models.Region", b =>
                {
                    b.Navigation("SubRegions");
                });

            modelBuilder.Entity("OVDB_database.Models.Route", b =>
                {
                    b.Navigation("RouteCountries");

                    b.Navigation("RouteInstances");

                    b.Navigation("RouteMaps");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstance", b =>
                {
                    b.Navigation("RouteInstanceMaps");

                    b.Navigation("RouteInstanceProperties");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteType", b =>
                {
                    b.Navigation("Routes");
                });

            modelBuilder.Entity("OVDB_database.Models.Station", b =>
                {
                    b.Navigation("StationVisits");
                });

            modelBuilder.Entity("OVDB_database.Models.StationCountry", b =>
                {
                    b.Navigation("StationMapCountries");

                    b.Navigation("Stations");
                });

            modelBuilder.Entity("OVDB_database.Models.StationMap", b =>
                {
                    b.Navigation("StationMapCountries");
                });

            modelBuilder.Entity("OVDB_database.Models.User", b =>
                {
                    b.Navigation("Countries");

                    b.Navigation("Maps");

                    b.Navigation("RouteTypes");
                });
#pragma warning restore 612, 618
        }
    }
}
