using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.MariaDb;
using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using Microsoft.Extensions.DependencyInjection;
using Respawn;

namespace OV_DB.IntegrationTests.Infrastructure
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private MariaDbContainer _mariaDbContainer;
        private Respawner _respawner;

        public string ConnectionString => _mariaDbContainer?.GetConnectionString();

        public async Task InitializeAsync()
        {
            _mariaDbContainer = new MariaDbBuilder()
                .WithImage("mariadb:11.2")
                .WithDatabase("ovdb_integration_test")
                .WithUsername("ovdb_test_user")
                .WithPassword("test_password")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(3306))
                .Build();

            await _mariaDbContainer.StartAsync();

            // Wait for MariaDB to be fully ready
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Run migrations to create schema
            var services = new ServiceCollection();
            services.AddDbContext<OVDBDatabaseContext>(options =>
                options.UseMySql(
                    ConnectionString,
                    ServerVersion.AutoDetect(ConnectionString),
                    mysqlOptions => mysqlOptions.UseNetTopologySuite()));

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            
            // Apply all migrations
            await context.Database.MigrateAsync();

            // Initialize Respawner for database cleanup
            await using var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.MySql,
                TablesToIgnore = new Respawn.Graph.Table[] { "__EFMigrationsHistory" }
            });
        }

        public async Task ResetDatabaseAsync()
        {
            var services = new ServiceCollection();
            services.AddDbContext<OVDBDatabaseContext>(options =>
                options.UseMySql(
                    ConnectionString,
                    ServerVersion.AutoDetect(ConnectionString),
                    mysqlOptions => mysqlOptions.UseNetTopologySuite()));

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            
            await using var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        public async Task DisposeAsync()
        {
            if (_mariaDbContainer != null)
            {
                await _mariaDbContainer.DisposeAsync();
            }
        }
    }

    [CollectionDefinition("Database")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        // This class is just used to define the collection
    }
}
