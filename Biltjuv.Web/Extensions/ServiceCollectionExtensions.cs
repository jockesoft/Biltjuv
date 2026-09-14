using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Quartz;
using Serilog;
using Biltjuv.Web.Infrastructure.Authentication;
using Biltjuv.Web.Infrastructure.Caching;
using Biltjuv.Web.Infrastructure.Crimes;
using Biltjuv.Web.Infrastructure.Game;
using Biltjuv.Web.Infrastructure.Mail;
using Biltjuv.Web.Infrastructure.Persistence;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Infrastructure.Timers;
using Biltjuv.Web.Infrastructure.Warehouses;
using Biltjuv.Web.Services;
using Biltjuv.Web.Services.Authentication;
using Biltjuv.Web.Services.Crimes;
using Biltjuv.Web.Services.Game;
using Biltjuv.Web.Services.Shop;

namespace Biltjuv.Web.Extensions;

/// <summary>Composition-root wiring for auth, persistence, mail, and background jobs.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCookieAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/Login";
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.Cookie.Name = "Biltjuv.Auth";
                options.Cookie.HttpOnly = true;
                // Lax (not Strict) so the cookie survives the top-level GET navigation
                // from the emailed sign-in link.
                options.Cookie.SameSite = SameSiteMode.Lax;
#if !DEBUG
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#endif
            });

        return services;
    }

    /// <summary>
    /// The Postgres-backed <see cref="AppDbContext"/>: a singleton-configured scoped
    /// context for per-request repositories, plus a factory for the startup
    /// migration runner and any future parallel-fanout work.
    /// </summary>
    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The DefaultConnection connection string is not configured.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        var dataSource = dataSourceBuilder.Build();

        void ConfigureAppDbContext(DbContextOptionsBuilder options)
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

            options.ConfigureWarnings(w =>
                w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
        }

        // optionsLifetime must be singleton so the factory below can share the options.
        services.AddDbContext<AppDbContext>(ConfigureAppDbContext, optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<AppDbContext>(ConfigureAppDbContext);

        return services;
    }

    /// <summary>
    /// The distributed cache (<see cref="IDistributedCacheJson"/>) backed by Redis, used to
    /// cache semi-static config data such as the warehouse catalog.
    /// </summary>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        // appsettings or env var: REDIS_CONNECTION=redis:6379
        var redisConnection = configuration["REDIS_CONNECTION"]
                               ?? configuration.GetConnectionString("RedisConnection");

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            throw new InvalidOperationException(
                "Redis connection string is not configured. Set the REDIS_CONNECTION environment " +
                "variable or the ConnectionStrings:RedisConnection setting.");
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "biltjuv:";
        });

        services.AddSingleton<IDistributedCacheJson, DistributedCacheJson>();

        return services;
    }

    /// <summary>Registers the warehouse catalog (JSON file, cached in Redis via <see cref="AddRedisCache"/>).</summary>
    public static IServiceCollection AddWarehouses(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WarehouseOptions>(configuration.GetSection(WarehouseOptions.SectionName));
        services.AddScoped<IWarehouseCatalogService, WarehouseCatalogService>();
        return services;
    }

    public static IServiceCollection AddMail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.AddScoped<IEmailRepository, EmailRepository>();
        services.AddScoped<IMailService, MailService>();
        return services;
    }

    public static IServiceCollection AddPasswordlessAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LoginTokenOptions>(configuration.GetSection(LoginTokenOptions.SectionName));
        services.AddScoped<ILoginTokenRepository, LoginTokenRepository>();
        services.AddScoped<IPasswordlessAuthService, PasswordlessAuthService>();
        services.AddSingleton<ILoginAbuseGuard, LoginAbuseGuard>();
        return services;
    }

    /// <summary>
    /// Throttles the sign-in form per client IP so it can't be scripted to spray
    /// login emails. Applied via <c>[EnableRateLimiting("login-email")]</c> on LoginModel.
    /// </summary>
    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("login-email", httpContext =>
            {
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 8,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                });
            });

            // Site-wide backstop on the same endpoint: bounds total sign-in POSTs
            // regardless of how many distinct IPs they come from.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                HttpMethods.IsPost(httpContext.Request.Method) &&
                httpContext.Request.Path.StartsWithSegments("/Account/Login")
                    ? RateLimitPartition.GetFixedWindowLimiter("login-post-global", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })
                    : RateLimitPartition.GetNoLimiter("unrestricted"));

            options.OnRejected = (context, cancellationToken) =>
            {
                Log.Warning(
                    "Rate limit exceeded for {Path} from {RemoteIp}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress);
                return ValueTask.CompletedTask;
            };
        });

        return services;
    }

    public static IServiceCollection AddCrimes(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StealOptions>(configuration.GetSection(StealOptions.SectionName));
        services.AddScoped<IGameDataRepository, GameDataRepository>();
        services.AddScoped<IStealService, StealService>();
        return services;
    }

    /// <summary>Registers <see cref="HealthRegenOptions"/> and the service the regen timer drives.</summary>
    public static IServiceCollection AddHealthRegen(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HealthRegenOptions>(configuration.GetSection(HealthRegenOptions.SectionName));
        services.AddScoped<IHealthRegenService, HealthRegenService>();
        return services;
    }

    /// <summary>Registers <see cref="LevelOptions"/> and the respect-to-level calculator.</summary>
    public static IServiceCollection AddLeveling(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LevelOptions>(configuration.GetSection(LevelOptions.SectionName));
        services.AddScoped<ILevelService, LevelService>();
        return services;
    }

    /// <summary>Registers the warehouse purchase flow (Pages/Shop/Warehouse).</summary>
    public static IServiceCollection AddShop(this IServiceCollection services)
    {
        services.AddScoped<IWarehouseService, WarehouseService>();
        return services;
    }

    /// <summary>Registers the Quartz.NET jobs (mail queue drain, health regen) and the hosted service that runs them.</summary>
    public static IServiceCollection AddScheduledJobs(this IServiceCollection services, IConfiguration configuration)
    {
        // Quartz triggers are built once at startup, so the regen interval is
        // read directly from config here rather than via IOptions<T>.
        var healthRegenOptions = configuration.GetSection(HealthRegenOptions.SectionName).Get<HealthRegenOptions>()
                                  ?? new HealthRegenOptions();

        services.AddQuartz(q =>
        {
            q.ScheduleJob<MailTimer>(trigger => trigger
                .WithIdentity("MailTimer-trigger")
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever())
                .WithDescription("Drain the outbound email queue once a minute."));

            q.ScheduleJob<HealthRegenTimer>(trigger => trigger
                .WithIdentity("HealthRegenTimer-trigger")
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(30))
                .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(healthRegenOptions.IntervalMinutes)).RepeatForever())
                .WithDescription($"Restore health to players below max, every {healthRegenOptions.IntervalMinutes} minute(s)."));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}
