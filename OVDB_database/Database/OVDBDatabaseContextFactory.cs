using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OVDB_database.Database
{


    class OVDBDatabaseContextFactory : IDesignTimeDbContextFactory<OVDBDatabaseContext>
    {
        private const string ConnectionString = "Server=192.168.178.30;Port=3307;Database=ovdb;Uid=ovdb;Pwd=4R2a&bzB^JPi&A^f4XaG*EI^Rvx@#J;";

        public OVDBDatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OVDBDatabaseContext>();
            optionsBuilder.UseMySql(ConnectionString,
                ServerVersion.AutoDetect(ConnectionString),
                options => options.UseNetTopologySuite());

            return new OVDBDatabaseContext(optionsBuilder.Options);
        }
    }


}
