using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OVDB_database.Database;
using System.Collections.Generic;

namespace OV_DB.IntegrationTests.Infrastructure
{
    public class OvdbWebApplicationFactory : WebApplicationFactory<Program>
    {
        public string ConnectionString { get; set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Set environment to Testing to prevent SPA middleware issues
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Override connection string for integration tests
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["DBCONNECTIONSTRING"] = ConnectionString,
                    ["JWTSigningKey"] = "IntegrationTestSigningKeyThatIsLongEnoughForHS256Algorithm",
                    ["Tokens:ValidityInMinutes"] = "60",
                    ["Tokens:Issuer"] = "OVDB-IntegrationTests",
                    ["Tokens:Audience"] = "OVDB",
                    ["UserAgent"] = "OVDB-IntegrationTests"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Database is configured via DBCONNECTIONSTRING in appsettings
                // No need to replace DbContext as it will use our test connection string
                
                // The database will be set up by the DatabaseFixture
                // We'll use migrations instead of EnsureCreated for proper schema
            });
        }
    }
}
