using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Quartz;
using Serilog;
using Biltjuv.Web.Infrastructure.Authentication;
using Biltjuv.Web.Infrastructure.Mail;
using Biltjuv.Web.Infrastructure.Persistence;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Infrastructure.Timers;
using Biltjuv.Web.Services;
using Biltjuv.Web.Services.Authentication;

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

    /// <summary>Registers the Quartz.NET jobs (currently just the mail queue drain) and the hosted service that runs them.</summary>
    public static IServiceCollection AddScheduledJobs(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            q.ScheduleJob<MailTimer>(trigger => trigger
                .WithIdentity("MailTimer-trigger")
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever())
                .WithDescription("Drain the outbound email queue once a minute."));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}
