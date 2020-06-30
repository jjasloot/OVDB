using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OVDB_database.Database
{


    class OVDBDatabaseContextFactory : IDesignTimeDbContextFactory<OVDBDatabaseContext>
    {
        public OVDBDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OVDBDatabaseContext>();
            optionsBuilder.UseMySql("Server=192.168.178.30;Port=3307;Database=ovdb;Uid=ovdb;Pwd=4R2a&bzB^JPi&A^f4XaG*EI^Rvx@#J;");

            return new OVDBDatabaseContext(optionsBuilder.Options);
        }
    }


}
