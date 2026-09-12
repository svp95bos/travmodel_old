using Microsoft.EntityFrameworkCore;

namespace TravModel.Infrastructure;

public static class DatabaseBootstrap
{
    public static TravDbContext CreateContext(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("TRAVMODEL_SQL_CONNECTION is required.");
        var options = new DbContextOptionsBuilder<TravDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        return new TravDbContext(options);
    }

    public static async Task InitializeAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
