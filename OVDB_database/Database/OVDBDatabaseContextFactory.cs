using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OVDB_database.Database
{


    class OVDBDatabaseContextFactory : IDesignTimeDbContextFactory<OVDBDatabaseContext>
    {
        public OVDBDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OVDBDatabaseContext>();
            
            // Use a dummy connection string for migrations - actual connection is configured in appsettings.json
            var connectionString = "Server=localhost;Port=3306;Database=ovdb;Uid=ovdb;Pwd=ovdb;";
            optionsBuilder.UseMySql(connectionString,
                new MySqlServerVersion(new Version(8, 0, 21)),
                options => options.UseNetTopologySuite());

            return new OVDBDatabaseContext(optionsBuilder.Options);
        }
    }


}
