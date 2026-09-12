using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TravModel.Infrastructure;

public sealed class TravDbContextDesignFactory : IDesignTimeDbContextFactory<TravDbContext>
{
    public TravDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("TRAVMODEL_SQL_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=TravModelDesign;Trusted_Connection=True;TrustServerCertificate=True";
        return new TravDbContext(new DbContextOptionsBuilder<TravDbContext>().UseSqlServer(connection).Options);
    }
}
