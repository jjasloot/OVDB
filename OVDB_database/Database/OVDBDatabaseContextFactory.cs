using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace OVDB_database.Database
{


    class OVDBDatabaseContextFactory : IDesignTimeDbContextFactory<OVDBDatabaseContext>
    {
        // Using a simpler connection string that doesn't require remote database access
        // This is only used for generating migrations at design time
        private const string ConnectionString = "Server=localhost;Database=ovdb;Uid=root;";

        public OVDBDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OVDBDatabaseContext>();
            optionsBuilder.UseMySql(ConnectionString,
                new MySqlServerVersion(new Version(8, 0, 21)),
                options => options.UseNetTopologySuite());

            return new OVDBDatabaseContext(optionsBuilder.Options);
        }
    }


}
