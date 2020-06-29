using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OVDB_database.Database
{


    class OVDBDatabaseContextFactory : IDesignTimeDbContextFactory<OVDBDatabaseContext>
    {
        public OVDBDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OVDBDatabaseContext>();
            optionsBuilder.UseMySql("ADDHERE");

            return new OVDBDatabaseContext(optionsBuilder.Options);
        }
    }


}
