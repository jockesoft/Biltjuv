using Biltjuv.Web.Extensions;
using Biltjuv.Web.Infrastructure.Persistence;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;
using Biltjuv.Web.Middleware;
using Biltjuv.Web.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCookieAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddPostgres(builder.Configuration);

builder.Services.AddMail(builder.Configuration);
builder.Services.AddPasswordlessAuth(builder.Configuration);
builder.Services.AddLoginRateLimiting();
builder.Services.AddCrimes(builder.Configuration);
builder.Services.AddHealthRegen(builder.Configuration);

builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScheduledJobs(builder.Configuration);

var app = builder.Build();

// Bring the database schema up to the current model before serving traffic.
await app.MigrateDatabaseAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

#if DEBUG
app.UseMiddleware<DevAuthMiddleware>();
#endif

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
