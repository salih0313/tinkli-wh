using Microsoft.EntityFrameworkCore;

namespace Wros.Api.Shared.Persistence;

// Hosted services start before the HTTP server accepts requests.
public sealed class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying database migrations before accepting requests");
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<WrosDbContext>().Database;
            // Schema changes can take longer than ordinary API queries.
            database.SetCommandTimeout(TimeSpan.FromMinutes(3));
            await database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migrations completed successfully");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Do not expose connection details from provider exception messages.
            logger.LogCritical("Database migration failed; API startup stopped. Check database connectivity, schema permissions and migration history");
            throw new InvalidOperationException("Database migration failed; API startup stopped. Check database connectivity, schema permissions and migration history.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
