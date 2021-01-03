﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OVDB_database.Database;

namespace OVDB_database.Migrations
{
    [DbContext(typeof(OVDBDatabaseContext))]
    [Migration("20201106221557_station listings")]
    partial class stationlistings
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("OVDB_database.Models.Country", b =>
                {
                    b.Property<int>("CountryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

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

                    b.Property<string>("Code")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

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

                    b.Property<bool>("Default")
                        .HasColumnType("tinyint(1)");

                    b.Property<Guid>("MapGuid")
                        .HasColumnType("char(36)");

                    b.Property<string>("Name")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<int>("OrderNr")
                        .HasColumnType("int");

                    b.Property<string>("SharingLinkName")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

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

            modelBuilder.Entity("OVDB_database.Models.Route", b =>
                {
                    b.Property<int>("RouteId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<double>("CalculatedDistance")
                        .HasColumnType("double");

                    b.Property<string>("Coordinates")
                        .HasColumnType("longtext CHARACTER SET utf8mb4")
                        .HasMaxLength(1048576000);

                    b.Property<string>("Description")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("DescriptionNL")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<DateTime?>("FirstDateTime")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("LineNumber")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("OperatingCompany")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("OverrideColour")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<double?>("OverrideDistance")
                        .HasColumnType("double");

                    b.Property<int?>("RouteTypeId")
                        .HasColumnType("int");

                    b.Property<Guid>("Share")
                        .HasColumnType("char(36)");

                    b.HasKey("RouteId");

                    b.HasIndex("RouteTypeId");

                    b.ToTable("Routes");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteCountry", b =>
                {
                    b.Property<long>("RouteCountryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

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

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("RouteId")
                        .HasColumnType("int");

                    b.HasKey("RouteInstanceId");

                    b.HasIndex("RouteId");

                    b.ToTable("RouteInstances");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstanceProperty", b =>
                {
                    b.Property<long>("RouteInstancePropertyId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    b.Property<bool?>("Bool")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTime?>("Date")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Key")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<int>("RouteInstanceId")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.HasKey("RouteInstancePropertyId");

                    b.HasIndex("RouteInstanceId");

                    b.ToTable("RouteInstanceProperties");
                });

            modelBuilder.Entity("OVDB_database.Models.RouteMap", b =>
                {
                    b.Property<long>("RouteMapId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

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

                    b.Property<string>("Colour")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

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

                    b.Property<double?>("Elevation")
                        .HasColumnType("double");

                    b.Property<double>("Lattitude")
                        .HasColumnType("double");

                    b.Property<double>("Longitude")
                        .HasColumnType("double");

                    b.Property<string>("Name")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Network")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Operator")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

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

                    b.Property<string>("Name")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("NameNL")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.HasKey("Id");

                    b.ToTable("StationCountries");
                });

            modelBuilder.Entity("OVDB_database.Models.StationVisit", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

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

                    b.Property<string>("Email")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<Guid>("Guid")
                        .HasColumnType("char(36)");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTime>("LastLogin")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Password")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("RefreshToken")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.HasKey("Id");

                    b.ToTable("Users");
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
                });

            modelBuilder.Entity("OVDB_database.Models.Map", b =>
                {
                    b.HasOne("OVDB_database.Models.User", "User")
                        .WithMany("Maps")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("OVDB_database.Models.Route", b =>
                {
                    b.HasOne("OVDB_database.Models.RouteType", "RouteType")
                        .WithMany("Routes")
                        .HasForeignKey("RouteTypeId");
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
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstance", b =>
                {
                    b.HasOne("OVDB_database.Models.Route", "Route")
                        .WithMany("RouteInstances")
                        .HasForeignKey("RouteId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("OVDB_database.Models.RouteInstanceProperty", b =>
                {
                    b.HasOne("OVDB_database.Models.RouteInstance", "RouteInstance")
                        .WithMany("RouteInstanceProperties")
                        .HasForeignKey("RouteInstanceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
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
                        .WithMany()
                        .HasForeignKey("StationCountryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("OVDB_database.Models.StationVisit", b =>
                {
                    b.HasOne("OVDB_database.Models.Station", "Station")
                        .WithMany()
                        .HasForeignKey("StationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OVDB_database.Models.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}