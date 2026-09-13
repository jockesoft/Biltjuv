using Microsoft.EntityFrameworkCore;

namespace Biltjuv.Web.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies any pending EF Core migrations before the app starts serving.
    /// Only forward migrations that ship in the assembly are run, so existing
    /// data is preserved — this never drops or recreates the database.
    ///
    /// Set <c>Database:MigrateOnStartup=false</c> (config or
    /// <c>Database__MigrateOnStartup</c> env var) to skip it, e.g. if you'd
    /// rather run migrations as a separate deploy step.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILogger<AppDbContext>>();

        if (!app.Configuration.GetValue("Database:MigrateOnStartup", true))
        {
            logger.LogInformation("Startup database migration is disabled (Database:MigrateOnStartup=false).");
            return;
        }

        // The DB container may still be coming up on a fresh `compose up`, even
        // with a healthcheck gate — retry a few times before giving up.
        const int maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(3);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var db = await factory.CreateDbContextAsync(cancellationToken);

                var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count == 0)
                {
                    logger.LogInformation("Database schema is up to date; no migrations to apply.");
                    return;
                }

                logger.LogInformation(
                    "Applying {Count} pending migration(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));

                await db.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("Database migrations applied successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex, "Database not ready for migration (attempt {Attempt}/{Max}); retrying in {Delay}s.",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
