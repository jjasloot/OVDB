using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NetTopologySuite;
using System.IO;

namespace OVDB_database.Database
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OVDBDatabaseContext>
    {
        public OVDBDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OVDBDatabaseContext>();
            
            // Use SQLite for design time (migrations)
            optionsBuilder.UseSqlite("Data Source=ovdb.db", x => x.UseNetTopologySuite());
            
            return new OVDBDatabaseContext(optionsBuilder.Options);
        }
    }
}