using System;
using System.Threading;
using EnvisionAnalytics.Data;
using EnvisionAnalytics.Models;
using EnvisionAnalytics.Services;
using EnvisionAnalytics.Hubs;
using Microsoft.AspNetCore.Identity;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/envision-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var conn = builder.Configuration.GetConnectionString("Default") ?? Environment.GetEnvironmentVariable("CONNECTIONSTRING") ?? "Host=db;Database=envision;Username=postgres;Password=postgres";
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(conn));

builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
}).AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.AddRazorPages();

builder.Services.AddSignalR();

builder.Services.AddScoped<DataSeeder>();
builder.Services.AddHostedService<EnvisionAnalytics.Services.OrdersMonitorService>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EnvisionAnalytics.Services.EmailSender>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

var app = builder.Build();

// Configure supported cultures and localization
var supportedCultures = new[] { "pt-BR", "en-US", "es-ES" };
var localizationOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var maxAttempts = 12;
    var delayMs = 1000;
    var migrated = false;
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            if (db.Database.CanConnect())
            {
                db.Database.Migrate();
                migrated = true;
                break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
        }

        Thread.Sleep(delayMs);
        delayMs = Math.Min(delayMs * 2, 10000);
    }

    if (!migrated)
    {
        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to apply migrations after retries: {Message}", ex.Message);
        }
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    try
    {
        seeder.SeedIfEmptyAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Log.Error("Data seeding failed: {Message}", ex.Message);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
